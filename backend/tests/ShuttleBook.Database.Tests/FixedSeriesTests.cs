using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

/// <summary>Independent F07 acceptance on disposable real PostgreSQL/PostGIS.</summary>
public sealed partial class FixedSeriesTests
{
    [Fact]
    public async Task Quote_checks_roles_closed_weekday_calendar_month_horizon_grid_and_court_minimum()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        using var guest = f.Factory.CreateClient(); using var owner = await f.Client("owner");
        await Code(await guest.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f)), 401, "UNAUTHORIZED");
        await Code(await owner.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f)), 403, "FORBIDDEN");
        foreach (var input in new[] { Input(f, weekday: "2"), Input(f, weekday: "FUNDAY"), Input(f, duration: 90),
            Input(f, duration: 135), Input(f, start: "18:15"),
            Input(f, end: DateOnly.Parse(f.Date).AddDays(27)), Input(f, end: DateOnly.Parse(f.Date).AddDays(70)) })
            await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", input), 400, "VALIDATION_FAILED");
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", new { courtId = f.CourtId,
            dayOfWeek = Weekday(f), localStartTime = "18:00", durationMinutes = 120, startsOn = f.Date,
            endsOn = DateOnly.Parse(f.Date).AddMonths(1).ToString("yyyy-MM-dd"), amount = 1 }), 400, "UNSUPPORTED_FIELD");
        // F05 shared pricing contract classifies a closed-hour interval as unavailable pricing, not malformed input.
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f, start: "21:00")), 409, "PRICE_UNAVAILABLE");
        await using (var db = f.Context()) { (await db.Courts.SingleAsync()).MinimumBookingMinutes = 180; await db.SaveChangesAsync(); }
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f)), 400, "VALIDATION_FAILED");
        await using (var db = f.Context()) { var court = await db.Courts.SingleAsync(); court.MinimumBookingMinutes = 120; court.BookingBlockMinutes = 60; await db.SaveChangesAsync(); }
        var q = await QuoteSeries(f, customer, duration: 150);
        Assert.True(q.GetProperty("canCreate").GetBoolean());
        Assert.All(q.GetProperty("occurrences").EnumerateArray(), x => Assert.Equal(5, x.GetProperty("slots").GetArrayLength()));
        await using var check = f.Context(); Assert.Equal(q.GetProperty("occurrenceCount").GetInt32(), await check.CourtAllocations.CountAsync(x => x.Status == "RESERVED")); Assert.Empty(await check.Bookings.ToArrayAsync());
    }

    [Fact]
    public async Task Prices_are_snapshotted_per_occurrence_and_one_payment_group_matches_late_date_filters()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var special = DateOnly.Parse(f.Date).AddDays(21);
        await using (var db = f.Context()) { db.PricingRules.Add(new PricingRule { CourtId = f.CourtId, DayOfWeek = (int)special.DayOfWeek,
            StartsOn = special, EndsOn = special, StartsAt = new(18, 0), EndsAt = new(20, 0), PricePerSlot = 250000, Priority = 10 }); await db.SaveChangesAsync(); }
        var q = await QuoteSeries(f, customer); var count = q.GetProperty("occurrenceCount").GetInt32(); var total = count * 400000L + 600000L;
        Assert.Equal(total.ToString(CultureInfo.InvariantCulture), q.GetProperty("amountExact").GetString());
        Assert.DoesNotContain("accountNumber", q.GetRawText()); Assert.DoesNotContain("objectKey", q.GetRawText());
        var created = await CreateSeries(customer, q, "snapshot"); var anchor = Id(created); var series = created.GetProperty("series");
        var seriesId = series.GetProperty("seriesId").GetGuid();
        Assert.Equal("FULL_SERIES", series.GetProperty("paymentPlan").GetString()); Assert.Equal("RECURRING_OCCURRENCE", created.GetProperty("bookingType").GetString());
        Assert.Equal(total, created.GetProperty("amount").GetInt64()); Assert.Equal(total, created.GetProperty("payment").GetProperty("expectedAmount").GetInt64());
        Assert.Equal(series.GetProperty("seriesNo").GetString(), created.GetProperty("payment").GetProperty("transferContent").GetString());
        await using (var db = f.Context()) {
            var bookings = await db.Bookings.OrderBy(x => x.LocalDate).ToArrayAsync(); Assert.Equal(count, bookings.Length);
            Assert.All(bookings, x => { Assert.Equal(seriesId, x.SeriesId); Assert.Equal(seriesId, x.PaymentScopeId); });
            Assert.Equal(total, bookings.Sum(x => x.Amount)); Assert.Equal(400000L, bookings[0].Amount); Assert.Equal(1000000L, bookings[3].Amount);
            var payment = Assert.Single(await db.BookingPayments.ToArrayAsync()); Assert.Equal(anchor, payment.BookingId); Assert.Equal(seriesId, payment.PaymentScopeId);
            Assert.Single(await db.BookingSeries.ToArrayAsync()); Assert.Single(await db.BookingIdempotency.ToArrayAsync()); Assert.Single(await db.OutboxMessages.ToArrayAsync());
            (await db.VenuePaymentAccounts.SingleAsync()).AccountName = "CHANGED RECIPIENT";
            (await db.PricingRules.OrderBy(x => x.Priority).FirstAsync()).PricePerSlot = 350000; await db.SaveChangesAsync();
        }
        var member = Id(Occurrences(created).Last());
        var detail = await Data(await customer.GetAsync($"/api/v1/bookings/{member}")); Assert.Equal(anchor, Id(detail)); Assert.Equal(total, detail.GetProperty("amount").GetInt64());
        Assert.Equal("TEST CLUB", detail.GetProperty("payment").GetProperty("accountName").GetString());
        using (var qr = await customer.GetAsync($"/api/v1/bookings/{member}/qr")) { Assert.Equal(HttpStatusCode.OK, qr.StatusCode); Assert.Equal(f.Bytes, await qr.Content.ReadAsByteArrayAsync()); }
        var list = await Data(await customer.GetAsync("/api/v1/me/bookings")); Assert.Single(list.GetProperty("items").EnumerateArray());
        var date = Occurrences(created).Last().GetProperty("date").GetString();
        var filtered = await Data(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?dateFrom={date}&dateTo={date}"));
        Assert.Single(filtered.GetProperty("items").EnumerateArray()); Assert.Equal(anchor, Id(filtered.GetProperty("items")[0]));
        Assert.Equal(total, filtered.GetProperty("items")[0].GetProperty("amount").GetInt64()); Assert.DoesNotContain("evidence", filtered.GetRawText());
        foreach (var occurrence in Occurrences(created)) await Availability(f, customer, occurrence.GetProperty("date").GetString()!, "RESERVED");
    }

    [Fact]
    public async Task Fourth_week_maintenance_conflict_has_priced_preview_and_no_partial_reservation()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var date = DateOnly.Parse(f.Date).AddDays(21).ToString("yyyy-MM-dd");
        await Data(await owner.PostAsJsonAsync($"/api/v1/operator/courts/{f.CourtId}/maintenance", new { date, startsAt = "18:30", endsAt = "19:00", reason = "Test maintenance" }), 201);
        var preview = await QuoteSeries(f, customer);
        Assert.False(preview.GetProperty("canCreate").GetBoolean()); Assert.Equal(JsonValueKind.Null, preview.GetProperty("quoteId").ValueKind);
        Assert.All(preview.GetProperty("occurrences").EnumerateArray(), x => Assert.Equal(400000L, x.GetProperty("amount").GetInt64()));
        Assert.Contains(date, preview.GetProperty("conflicts").GetRawText());
        await using var db = f.Context(); Assert.Empty(await db.BookingSeries.ToArrayAsync()); Assert.Empty(await db.Bookings.ToArrayAsync());
        Assert.Empty(await db.BookingPayments.ToArrayAsync()); Assert.Empty(await db.BookingIdempotency.ToArrayAsync()); Assert.Empty(await db.OutboxMessages.ToArrayAsync());
        Assert.Empty(await db.QuoteReservations.ToArrayAsync()); Assert.Single(await db.CourtAllocations.ToArrayAsync()); Assert.Single(await db.CourtMaintenance.ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_same_key_create_and_cross_occurrence_report_replay_are_single_group_intents()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        using var secondHost = new ApiFactory(f.Connection, f.Clock); using var second = secondHost.CreateClient(); await f.Login(second, "customer");
        var q = await QuoteSeries(f, customer);
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => CreateResponse(i % 2 == 0 ? customer : second, q, "same-series")));
        var groups = new List<JsonElement>(); foreach (var reply in replies) groups.Add(await Data(reply, 201));
        var created = groups[0]; Assert.All(groups, x => Assert.Equal(Id(created), Id(x)));
        f.Clock.Set(f.Clock.GetUtcNow().AddMinutes(3)); Assert.Equal(Id(created), Id(await CreateSeries(customer, q, "same-series")));
        await Code(await Send(customer, "/api/v1/booking-series", "same-series", null, new { quoteId = Guid.NewGuid() }), 409, "IDEMPOTENCY_KEY_REUSED");
        var member = Id(Occurrences(created).Last());
        Assert.Equal(Id(created), Id(await Data(await Send(customer, ReportRoute(member), "same-evidence", 1, new { }))));
        var replay = await Data(await Send(customer, ReportRoute(Id(created)), "same-evidence", 1, new { })); Assert.Equal(2, replay.GetProperty("version").GetInt64());
        await Code(await Send(customer, ReportRoute(member), "same-evidence", 1, new { note = "different" }), 409, "IDEMPOTENCY_KEY_REUSED");
        await using var db = f.Context(); Assert.Single(await db.BookingSeries.ToArrayAsync()); Assert.Single(await db.BookingPayments.ToArrayAsync());
        Assert.Equal(2, await db.BookingIdempotency.CountAsync()); Assert.Single(await db.PaymentEvidence.ToArrayAsync());
        Assert.Single(await db.OutboxMessages.Where(x => x.EventType == "PAYMENT_TRANSFER_REPORTED").ToArrayAsync());
        await GroupState(f, created, "AWAITING_OWNER_CONFIRMATION", 2, "RESERVED");
    }

    [Theory]
    [InlineData("CASUAL")]
    [InlineData("MAINTENANCE")]
    public async Task Reserved_series_create_and_competing_casual_or_maintenance_preserve_one_complete_group(string kind)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var other = await f.Client("other"); using var owner = await f.Client("owner");
        var q = await QuoteSeries(f, customer); var date = q.GetProperty("occurrences")[3].GetProperty("date").GetString();
        var first = CreateResponse(customer, q, "series-race");
        var second = kind == "CASUAL" ? other.PostAsJsonAsync("/api/v1/availability/quote", new { courtId = f.CourtId, date, startsAt = "18:30", endsAt = "19:00" }) :
            owner.PostAsJsonAsync($"/api/v1/operator/courts/{f.CourtId}/maintenance", new { date, startsAt = "18:30", endsAt = "19:00", reason = "Concurrent maintenance" });
        var results = await Task.WhenAll(first, second);
        await Data(results[0], 201); await Code(results[1], 409, kind == "CASUAL" ? "SLOT_UNAVAILABLE" : "SLOT_CONFLICT");
        var count = q.GetProperty("occurrenceCount").GetInt32();
        await using var db = f.Context(); Assert.Equal(1, await db.BookingSeries.CountAsync());
        Assert.Equal(count, await db.Bookings.CountAsync()); Assert.Equal(count, await db.CourtAllocations.CountAsync()); Assert.Equal(1, await db.BookingPayments.CountAsync());
    }

    [Fact]
    public async Task Expired_or_changed_quote_and_overflow_never_create_a_group()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var expired = await QuoteSeries(f, customer); f.Clock.Set(expired.GetProperty("expiresAt").GetDateTimeOffset());
        await Code(await CreateResponse(customer, expired, "expired"), 409, "QUOTE_EXPIRED");
        foreach (var change in new[] { "price", "hold", "minimum", "block", "recipient", "timezone" }) {
            var q = await QuoteSeries(f, customer);
            await using (var db = f.Context()) {
                var court = await db.Courts.SingleAsync();
                switch (change) {
                    case "price": (await db.PricingRules.FirstAsync(x => x.DayOfWeek == (int)DateOnly.Parse(f.Date).DayOfWeek)).PricePerSlot += 1000; break;
                    case "hold": court.HoldMinutes = 25; break;
                    case "minimum": court.MinimumBookingMinutes = 60; break;
                    case "block": court.BookingBlockMinutes = 60; break;
                    case "recipient": (await db.VenuePaymentAccounts.SingleAsync()).AccountName = "CHANGED RECIPIENT"; break;
                    case "timezone": (await db.Venues.SingleAsync()).Timezone = "Asia/Bangkok"; break;
                }
                await db.SaveChangesAsync();
            }
            await Code(await CreateResponse(customer, q, $"changed-{change}"), 409, "QUOTE_CHANGED");
            // A rejected create leaves its quote hold in place. Advance the test clock
            // past the hold before asking for the next independent quote.
            f.Clock.Set(q.GetProperty("expiresAt").GetDateTimeOffset());
        }
        await using (var db = f.Context()) { foreach (var rule in await db.PricingRules.ToArrayAsync()) rule.PricePerSlot = 100_000_000_000_000_000; await db.SaveChangesAsync(); }
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f)), 409, "AMOUNT_LIMIT_EXCEEDED");
        await using var check = f.Context(); Assert.Empty(await check.Bookings.ToArrayAsync()); Assert.Empty(await check.BookingSeries.ToArrayAsync());
        Assert.All(await check.CourtAllocations.Where(x => x.Status == "RESERVED").ToArrayAsync(), x => Assert.Equal("QUOTE_HOLD", x.Kind)); Assert.Empty(await check.BookingIdempotency.ToArrayAsync()); Assert.Empty(await check.OutboxMessages.ToArrayAsync());
    }

    [Fact]
    public async Task Missing_late_price_or_QR_does_not_invent_zero_price_or_save_a_usable_quote()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); var missing = DateOnly.Parse(f.Date).AddDays(21);
        await using (var db = f.Context()) { (await db.PricingRules.SingleAsync(x => x.DayOfWeek == (int)missing.DayOfWeek)).EndsOn = missing.AddDays(-1); await db.SaveChangesAsync(); }
        using (var response = await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f))) {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); var body = await response.Content.ReadAsStringAsync(); Assert.Contains("PRICE_UNAVAILABLE", body); Assert.Contains(missing.ToString("yyyy-MM-dd"), body);
        }
        await using (var db = f.Context()) { (await db.PricingRules.SingleAsync(x => x.DayOfWeek == (int)missing.DayOfWeek)).EndsOn = DateOnly.MaxValue;
            db.VenuePaymentAccounts.Remove(await db.VenuePaymentAccounts.SingleAsync()); await db.SaveChangesAsync(); }
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f)), 409, "PAYMENT_SETUP_UNAVAILABLE");
        await using var check = f.Context(); Assert.Empty(await check.BookingSeriesQuotes.ToArrayAsync()); Assert.Empty(await check.Bookings.ToArrayAsync());
    }

    [Fact]
    public async Task Occurrence_ids_normalize_proof_and_every_payment_transition_to_the_whole_period()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        using var other = await f.Client("other"); using var ownerB = await f.Client("owner-b"); using var admin = await f.Client("admin");
        var created = await CreateSeries(customer, await QuoteSeries(f, customer), "payments"); var anchor = Id(created); var member = Id(Occurrences(created).Last());
        await Code(await other.GetAsync($"/api/v1/bookings/{member}"), 404, "NOT_FOUND"); await Code(await ownerB.GetAsync($"/api/v1/operator/bookings/{member}"), 404, "NOT_FOUND");
        await Code(await Send(admin, ConfirmRoute(member), "admin", 1, new { confirmedAmount = created.GetProperty("amount").GetInt64() }), 403, "FORBIDDEN");
        var pending = await f.PresignProof(customer, member); var pendingId = pending.GetProperty("id").GetGuid();
        await using (var db = f.Context()) Assert.Equal(anchor, (await db.MediaUploads.SingleAsync(x => x.Id == pendingId)).BookingId);
        await Code(await Send(customer, ReportRoute(member), "pending", 1, new { proofUploadId = pendingId }), 409, "UPLOAD_NOT_READY");
        var proof = await f.UploadProof(customer, member); await Code(await owner.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        await Code(await admin.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        var reported = await Data(await Send(customer, ReportRoute(member), "report", 1, new { proofUploadId = proof }));
        var firstReported = reported.GetProperty("payment").GetProperty("firstReportedAt").GetDateTimeOffset(); await GroupState(f, created, "AWAITING_OWNER_CONFIRMATION", 2, "RESERVED");
        using (var image = await owner.GetAsync($"/api/v1/uploads/{proof}/view")) { Assert.Equal(HttpStatusCode.OK, image.StatusCode); Assert.Equal(f.Bytes, await image.Content.ReadAsByteArrayAsync()); }
        await Code(await ownerB.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        await Data(await Send(owner, RejectRoute(member), "review", 2, Decision("NEEDS_REVIEW", "Please supplement test evidence")));
        // Keep real-time-valid JWTs; re-login after jumping the business test clock would issue a future nbf.
        f.Clock.Set(created.GetProperty("paymentDeadline").GetDateTimeOffset().AddMinutes(1));
        await using (var worker = f.Context()) Assert.False(await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None));
        var supplemented = await Data(await Send(customer, ReportRoute(anchor), "supplement", 3, new { note = "Supplement without mandatory bank reference" }));
        Assert.Equal(firstReported, supplemented.GetProperty("payment").GetProperty("firstReportedAt").GetDateTimeOffset()); Assert.Equal(2, supplemented.GetProperty("evidence").GetArrayLength());
        await Code(await Send(owner, ConfirmRoute(member), "wrong-amount", 4, new { confirmedAmount = Occurrences(created)[0].GetProperty("amount").GetInt64() }), 409, "PAYMENT_AMOUNT_MISMATCH");
        await GroupState(f, created, "AWAITING_OWNER_CONFIRMATION", 4, "RESERVED");
        var confirmed = await Data(await Send(owner, ConfirmRoute(member), "confirm", 4, new { confirmedAmount = created.GetProperty("amount").GetInt64() }));
        Assert.Equal(anchor, Id(confirmed)); Assert.Equal("PAID", confirmed.GetProperty("payment").GetProperty("status").GetString()); await GroupState(f, created, "CONFIRMED", 5, "RESERVED");
        await using var check = f.Context(); Assert.Equal(2, await check.PaymentEvidence.CountAsync()); Assert.Equal(2, await check.PaymentDecisions.CountAsync());
        Assert.Single(await check.BookingPayments.ToArrayAsync()); Assert.Equal(created.GetProperty("amount").GetInt64(), (await check.BookingPayments.SingleAsync()).ConfirmedAmount);
    }

    [Fact]
    public async Task Expiry_releases_unreported_period_but_review_holds_all_dates_and_SLA_is_one_logical_event()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var expires = await CreateSeries(customer, await QuoteSeries(f, customer), "expires");
        var retained = await CreateSeries(customer, await QuoteSeries(f, customer, start: "20:00"), "retained");
        await Data(await Send(customer, ReportRoute(Id(Occurrences(retained).Last())), "report", 1, new { }));
        await Data(await Send(owner, RejectRoute(Id(retained)), "review", 2, Decision("NEEDS_REVIEW", "Owner needs test evidence")));
        f.Clock.Set(expires.GetProperty("paymentDeadline").GetDateTimeOffset());
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => { await using var worker = f.Context(); return await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None); }));
        Assert.Single(results, x => x); await GroupState(f, expires, "EXPIRED", 2, "RELEASED"); await GroupState(f, retained, "NEEDS_REVIEW", 3, "RESERVED");
        foreach (var occurrence in Occurrences(expires)) await Availability(f, customer, occurrence.GetProperty("date").GetString()!, "AVAILABLE");
        f.Clock.Set(f.Clock.GetUtcNow().AddMinutes(15));
        var alerts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => { await using var worker = f.Context(); return await ConfirmationAlerts.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None); }));
        Assert.Single(alerts, x => x); await DrainOutbox(f);
        await using var check = f.Context(); var overdue = await check.OutboxMessages.SingleAsync(x => x.EventType == "PAYMENT_CONFIRMATION_OVERDUE");
        var notices = await check.Notifications.Where(x => x.OutboxMessageId == overdue.Id).ToArrayAsync(); Assert.Equal(2, notices.Length);
        Assert.Contains(notices, x => x.UserId == f.OwnerId); var adminId = (await check.Users.SingleAsync(x => x.AccountType == AccountType.Admin)).Id;
        Assert.Contains(notices, x => x.UserId == adminId); Assert.DoesNotContain(notices, x => x.UserId == f.CustomerId || x.UserId == f.OwnerBId);
        Assert.All(notices, x => Assert.Contains(retained.GetProperty("series").GetProperty("seriesNo").GetString()!, x.Body));
        Assert.All(notices, x => Assert.DoesNotContain("1234567890", x.Body)); await GroupState(f, retained, "NEEDS_REVIEW", 3, "RESERVED");
    }

    [Fact]
    public async Task Final_rejection_releases_whole_period_and_confirm_reject_race_has_one_atomic_winner()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var rejected = await CreateSeries(customer, await QuoteSeries(f, customer), "reject"); var member = Id(Occurrences(rejected).Last());
        await Data(await Send(customer, ReportRoute(member), "report-reject", 1, new { }));
        await Data(await Send(owner, RejectRoute(member), "reject-final", 2, Decision("FINAL_REJECTION", "Test transaction not found", "TRANSACTION_NOT_FOUND")));
        await GroupState(f, rejected, "PAYMENT_REJECTED", 3, "RELEASED");
        var replay = await Data(await Send(owner, RejectRoute(Id(rejected)), "reject-final", 2, Decision("FINAL_REJECTION", "Test transaction not found", "TRANSACTION_NOT_FOUND")));
        Assert.Equal(3, replay.GetProperty("version").GetInt64());
        var racing = await CreateSeries(customer, await QuoteSeries(f, customer), "race-after-release");
        await Data(await Send(customer, ReportRoute(Id(racing)), "report-race", 1, new { }));
        var responses = await Task.WhenAll(Send(owner, ConfirmRoute(Id(Occurrences(racing).Last())), "confirm-race", 2, new { confirmedAmount = racing.GetProperty("amount").GetInt64() }),
            Send(owner, RejectRoute(Id(racing)), "reject-race", 2, Decision("FINAL_REJECTION", "Concurrent decision test", "OTHER")));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.PreconditionFailed);
        var success = await Data(responses.Single(x => x.IsSuccessStatusCode)); var paid = success.GetProperty("status").GetString() == "CONFIRMED";
        await GroupState(f, racing, paid ? "CONFIRMED" : "PAYMENT_REJECTED", 3, paid ? "RESERVED" : "RELEASED");
        foreach (var response in responses.Where(x => !x.IsSuccessStatusCode)) response.Dispose();
        await using var check = f.Context(); Assert.Equal(2, await check.PaymentDecisions.CountAsync());
    }

    [Fact]
    public async Task Replay_rechecks_active_customer_owner_membership_business_and_database_scope_constraints()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var q = await QuoteSeries(f, customer); var created = await CreateSeries(customer, q, "revoke-replay"); var anchor = Id(created); var member = Id(Occurrences(created).Last());
        await Data(await Send(customer, ReportRoute(member), "report-scope", 1, new { }));
        await Data(await Send(owner, RejectRoute(member), "review-scope", 2, Decision("NEEDS_REVIEW", "Scope test review")));
        await using (var db = f.Context()) { (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "REVOKED"; await db.SaveChangesAsync(); }
        await Code(await Send(owner, RejectRoute(anchor), "review-scope", 2, Decision("NEEDS_REVIEW", "Scope test review")), 404, "NOT_FOUND");
        await using (var db = f.Context()) { (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "ACTIVE"; (await db.Businesses.SingleAsync(x => x.Id == f.BusinessId)).Status = "SUSPENDED"; await db.SaveChangesAsync(); }
        await Code(await Send(owner, RejectRoute(member), "review-scope", 2, Decision("NEEDS_REVIEW", "Scope test review")), 404, "NOT_FOUND");
        await using (var db = f.Context()) { (await db.Users.SingleAsync(x => x.Id == f.CustomerId)).Status = UserStatus.Suspended; await db.SaveChangesAsync(); }
        await Code(await CreateResponse(customer, q, "revoke-replay"), 401, "UNAUTHORIZED");
        await Code(await Send(customer, ReportRoute(anchor), "report-scope", 1, new { }), 401, "UNAUTHORIZED");
        await GroupState(f, created, "NEEDS_REVIEW", 3, "RESERVED");
        foreach (var sql in new[] { $"UPDATE bookings SET series_id=NULL WHERE id='{member}'",
            $"UPDATE bookings SET payment_scope_id=id WHERE id='{member}'",
            $"UPDATE bookings SET customer_id='{f.OtherCustomerId}' WHERE id='{member}'",
            $"UPDATE payments SET payment_scope_id='{Guid.NewGuid()}' WHERE booking_id='{anchor}'" }) {
            await using var db = f.Context(); await using var tx = await db.Database.BeginTransactionAsync();
            var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql)); Assert.Contains(ex.SqlState, new[] { "23503", "23514" }); await tx.RollbackAsync();
        }
    }

    [Fact]
    public async Task Upgrade_from_final_F06_backfills_casual_scope_and_preserves_payment_evidence_QR_and_hashes()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await f.Book(customer, "18:00", "20:00", "old-casual"); var anchor = Id(created);
        await Data(await Send(customer, ReportRoute(anchor), "old-report", 1, new { note = "Original F06 evidence" }));
        string[] hashes; string snapshot; Guid evidenceId;
        await using (var db = f.Context()) {
            hashes = await db.BookingIdempotency.OrderBy(x => x.Operation).Select(x => x.RequestHash).ToArrayAsync();
            snapshot = (await db.BookingPayments.SingleAsync()).RecipientSnapshot; evidenceId = (await db.PaymentEvidence.SingleAsync()).Id;
            await db.GetService<IMigrator>().MigrateAsync("20261007114512_F06OptionalBankReference");
            await using (var connection = new NpgsqlConnection(f.Connection)) {
                await connection.OpenAsync(); await using var legacy = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_name='bookings' AND column_name='payment_scope_id'", connection);
                Assert.Equal(0L, (long)(await legacy.ExecuteScalarAsync())!);
            }
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        }
        var replay = await Data(await f.ReplayCreate(customer), 201); Assert.Equal(anchor, Id(replay)); Assert.Equal("AWAITING_OWNER_CONFIRMATION", replay.GetProperty("status").GetString());
        await Data(await Send(customer, ReportRoute(anchor), "old-report", 1, new { note = "Original F06 evidence" }));
        using (var qr = await customer.GetAsync($"/api/v1/bookings/{anchor}/qr")) { Assert.Equal(HttpStatusCode.OK, qr.StatusCode); Assert.Equal(f.Bytes, await qr.Content.ReadAsByteArrayAsync()); }
        await using var check = f.Context(); var booking = await check.Bookings.SingleAsync(); Assert.Null(booking.SeriesId); Assert.Equal(anchor, booking.PaymentScopeId);
        var payment = await check.BookingPayments.SingleAsync(); Assert.Equal(anchor, payment.PaymentScopeId); Assert.Equal(snapshot, payment.RecipientSnapshot);
        Assert.Equal(evidenceId, (await check.PaymentEvidence.SingleAsync()).Id); Assert.Equal(hashes, await check.BookingIdempotency.OrderBy(x => x.Operation).Select(x => x.RequestHash).ToArrayAsync());
        Assert.Empty(await check.BookingSeries.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Locked_earliest_series_does_not_starve_other_expiry_or_SLA_groups(bool alerts)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var first = await CreateSeries(customer, await QuoteSeries(f, customer), "locked-first");
        f.Clock.Set(f.Clock.GetUtcNow().AddSeconds(1));
        var second = await CreateSeries(customer, await QuoteSeries(f, customer, start: "20:00"), "unlocked-next");
        if (alerts)
        {
            await Data(await Send(customer, ReportRoute(Id(first)), "report-first", 1, new { }));
            await Data(await Send(customer, ReportRoute(Id(second)), "report-second", 1, new { }));
            f.Clock.Set(f.Clock.GetUtcNow().AddMinutes(31));
        }
        else f.Clock.Set(second.GetProperty("paymentDeadline").GetDateTimeOffset());
        await using var blocker = f.Context(); await using var barrier = await blocker.Database.BeginTransactionAsync();
        var firstSeries = first.GetProperty("series").GetProperty("seriesId").GetGuid();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM booking_series WHERE id={firstSeries} FOR UPDATE");
        await using (var worker = f.Context())
        {
            var processed = alerts ? await ConfirmationAlerts.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None)
                : await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None);
            Assert.True(processed);
        }
        await using (var check = f.Context())
        {
            var firstPayment = await check.BookingPayments.SingleAsync(x => x.BookingId == Id(first));
            var secondPayment = await check.BookingPayments.SingleAsync(x => x.BookingId == Id(second));
            if (alerts) { Assert.Null(firstPayment.ConfirmationAlertedAt); Assert.NotNull(secondPayment.ConfirmationAlertedAt); }
            else { Assert.Equal("AWAITING_TRANSFER", firstPayment.Status); Assert.Equal("EXPIRED", secondPayment.Status); }
        }
        await barrier.RollbackAsync();
        await using (var worker = f.Context())
            Assert.True(alerts ? await ConfirmationAlerts.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None)
                : await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None));
    }

    [Fact]
    public async Task Competing_different_customer_series_have_one_complete_winner()
    {
        await using var f = await Fixture.Create(); using var first = await f.Client("customer"); using var second = await f.Client("other");
        var quotes = await Task.WhenAll(QuoteSeries(f, first), QuoteSeries(f, second));
        var winnerIndex = Array.FindIndex(quotes, q => q.GetProperty("canCreate").GetBoolean()); Assert.InRange(winnerIndex, 0, 1);
        Assert.False(quotes[1 - winnerIndex].GetProperty("canCreate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, quotes[1 - winnerIndex].GetProperty("quoteId").ValueKind);
        var winning = await CreateSeries(winnerIndex == 0 ? first : second, quotes[winnerIndex], "winning-series");
        await using var db = f.Context(); Assert.Single(await db.BookingSeries.ToArrayAsync());
        Assert.Single(await db.BookingPayments.ToArrayAsync()); Assert.Single(await db.BookingIdempotency.ToArrayAsync());
        Assert.Equal(Occurrences(winning).Length, await db.Bookings.CountAsync()); Assert.Equal(Occurrences(winning).Length, await db.CourtAllocations.CountAsync());
    }

    [Fact]
    public async Task Series_read_has_customer_owner_scope_and_never_exposes_private_data_to_other_roles()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        using var other = await f.Client("other"); using var ownerB = await f.Client("owner-b"); using var admin = await f.Client("admin");
        using var guest = f.Factory.CreateClient();
        var created = await CreateSeries(customer, await QuoteSeries(f, customer), "series-read");
        var seriesId = created.GetProperty("series").GetProperty("seriesId").GetGuid(); var route = $"/api/v1/booking-series/{seriesId}";
        var mine = await Data(await customer.GetAsync(route)); Assert.Equal(Id(created), Id(mine));
        Assert.Equal(created.GetProperty("amountExact").GetString(), mine.GetProperty("amountExact").GetString());
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("customer").ValueKind);
        var operatorView = await Data(await owner.GetAsync(route)); Assert.Equal(Id(created), Id(operatorView));
        Assert.NotEqual(JsonValueKind.Null, operatorView.GetProperty("customer").ValueKind);
        await Code(await guest.GetAsync(route), 401, "UNAUTHORIZED"); await Code(await other.GetAsync(route), 404, "NOT_FOUND");
        await Code(await ownerB.GetAsync(route), 404, "NOT_FOUND"); await Code(await admin.GetAsync(route), 403, "FORBIDDEN");
        await using (var db = f.Context()) { (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "REVOKED"; await db.SaveChangesAsync(); }
        await Code(await owner.GetAsync(route), 404, "NOT_FOUND");
    }

    [Fact]
    public async Task Report_waiting_for_series_lock_rechecks_deadline_and_expiry_releases_every_occurrence()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await CreateSeries(customer, await QuoteSeries(f, customer), "deadline-group");
        var seriesId = created.GetProperty("series").GetProperty("seriesId").GetGuid(); var member = Id(Occurrences(created).Last());
        var deadline = created.GetProperty("paymentDeadline").GetDateTimeOffset();
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM booking_series WHERE id={seriesId} FOR UPDATE");
        var waiting = Send(customer, ReportRoute(member), "wait-report-series", 1, new { });
        await WaitForLock(f, "booking_series", "FOR UPDATE"); Assert.False(waiting.IsCompleted);
        f.Clock.Set(deadline); await tx.CommitAsync();
        await Code(await waiting, 409, "PAYMENT_DEADLINE_EXPIRED");
        await using (var worker = f.Context()) Assert.True(await BookingExpiry.ProcessOne(worker, deadline, CancellationToken.None));
        await GroupState(f, created, "EXPIRED", 2, "RELEASED");
        await Code(await Send(customer, ReportRoute(member), "expired-group-report", 2, new { }), 409, "STATE_CONFLICT");
        await using var check = f.Context(); Assert.Empty(await check.PaymentEvidence.ToArrayAsync());
        Assert.Single(await check.BookingIdempotency.ToArrayAsync());
    }

    [Fact]
    public async Task Creating_series_rechecks_suspended_customer_after_waiting_for_court_lock()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var quote = await QuoteSeries(f, customer);
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM courts WHERE id={f.CourtId} FOR UPDATE");
        var waiting = CreateResponse(customer, quote, "suspend-waiting-create");
        await WaitForLock(f, "courts", "FOR UPDATE"); Assert.False(waiting.IsCompleted);
        await using (var revoke = f.Context()) { (await revoke.Users.SingleAsync(x => x.Id == f.CustomerId)).Status = UserStatus.Suspended; await revoke.SaveChangesAsync(); }
        await tx.CommitAsync(); await Code(await waiting, 401, "UNAUTHORIZED");
        await using var check = f.Context(); Assert.Empty(await check.Bookings.ToArrayAsync()); Assert.Empty(await check.BookingSeries.ToArrayAsync());
        Assert.Empty(await check.BookingPayments.ToArrayAsync());
        var holds = await check.CourtAllocations.ToArrayAsync(); Assert.Equal(quote.GetProperty("occurrenceCount").GetInt32(), holds.Length);
        Assert.All(holds, x => { Assert.Equal("QUOTE_HOLD", x.Kind); Assert.Equal("RESERVED", x.Status); Assert.Equal(quote.GetProperty("quoteId").GetGuid(), x.QuoteReservationId); });
        Assert.Null((await check.QuoteReservations.SingleAsync()).ConsumedAt);
        Assert.Empty(await check.BookingIdempotency.ToArrayAsync()); Assert.Empty(await check.OutboxMessages.ToArrayAsync());
    }

    [Fact]
    public async Task Exclusion_constraint_quote_race_rolls_back_all_reservations_and_reads_conflict_after_aborted_transaction()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var starts = DateTimeOffset.Parse(f.Date + "T18:00:00+07:00").ToUniversalTime().AddDays(21); var ends = starts.AddHours(2);
        var date = DateOnly.Parse(f.Date).AddDays(21).ToString("yyyy-MM-dd"); var conflictId = Guid.CreateVersion7();
        await using (var seed = f.Context())
        {
            seed.CourtAllocations.Add(new CourtAllocation { Id = conflictId, CourtId = f.CourtId, Kind = "MAINTENANCE", Status = "RELEASED",
                StartsAt = starts, EndsAt = ends, CreatedAt = f.Clock.GetUtcNow(), ReleasedAt = f.Clock.GetUtcNow() }); await seed.SaveChangesAsync();
        }
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        // A raw competing transaction bypasses the application court lock to exercise PostgreSQL's
        // final exclusion protection. Its uncommitted allocation is invisible to quote computation.
        await holder.Database.ExecuteSqlInterpolatedAsync($"UPDATE court_allocations SET status='RESERVED', released_at=NULL WHERE id={conflictId}");
        var waiting = customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f));
        await WaitForLock(f, "court_allocations", "INSERT"); Assert.False(waiting.IsCompleted);
        await tx.CommitAsync(); var preview = await Data(await waiting);
        Assert.False(preview.GetProperty("canCreate").GetBoolean()); Assert.Equal(JsonValueKind.Null, preview.GetProperty("quoteId").ValueKind);
        Assert.Contains(date, preview.GetProperty("conflicts").GetRawText()); Assert.DoesNotContain("accountNumber", preview.GetRawText()); Assert.DoesNotContain("customerId", preview.GetRawText());
        await using var check = f.Context(); Assert.Empty(await check.BookingSeries.ToArrayAsync()); Assert.Empty(await check.Bookings.ToArrayAsync());
        Assert.Empty(await check.BookingPayments.ToArrayAsync()); Assert.Empty(await check.BookingIdempotency.ToArrayAsync()); Assert.Empty(await check.OutboxMessages.ToArrayAsync());
        Assert.Empty(await check.QuoteReservations.ToArrayAsync()); Assert.Single(await check.CourtAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Downgrade_refuses_to_remove_an_existing_fixed_series_and_preserves_its_payment_group()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await CreateSeries(customer, await QuoteSeries(f, customer), "preserve-series");
        await using (var db = f.Context())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261007114512_F06OptionalBankReference"));
            Assert.Contains("F07 downgrade refused", error.MessageText);
            // EF commits each migration independently. QuoteReservations.Down has already
            // completed, while FixedSeries.Down refused. Check historical tables with SQL
            // before restoring the newest schema used by the current read model.
            await using var connection = new NpgsqlConnection(f.Connection); await connection.OpenAsync();
            await using var preserved = new NpgsqlCommand("SELECT (SELECT count(*) FROM booking_series), (SELECT count(*) FROM bookings), (SELECT count(*) FROM payments), (SELECT count(*) FROM court_allocations WHERE kind='BOOKING' AND status='RESERVED')", connection);
            await using (var reader = await preserved.ExecuteReaderAsync()) {
                Assert.True(await reader.ReadAsync()); Assert.Equal(1L, reader.GetInt64(0)); Assert.Equal((long)Occurrences(created).Length, reader.GetInt64(1));
                Assert.Equal(1L, reader.GetInt64(2)); Assert.Equal((long)Occurrences(created).Length, reader.GetInt64(3));
            }
            await db.Database.MigrateAsync();
        }
        await GroupState(f, created, "AWAITING_TRANSFER", 1, "RESERVED");
        await using var check = f.Context(); Assert.Single(await check.BookingSeries.ToArrayAsync()); Assert.Single(await check.BookingPayments.ToArrayAsync());
        var history = await check.Database.GetAppliedMigrationsAsync(); Assert.Contains("20261007150835_F07FixedSeries", history);
    }

    [Fact]
    public async Task Replay_with_changed_intent_rechecks_suspended_actor_after_advisory_lock()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await CreateSeries(customer, await QuoteSeries(f, customer), "replay-revoke");
        await using var holder = f.Context(); await using var barrier = await holder.Database.BeginTransactionAsync();
        var lockKey = $"SERIES_CREATE:{f.CustomerId}:replay-revoke";
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))");
        var waiting = Send(customer, "/api/v1/booking-series", "replay-revoke", null, new { quoteId = Guid.NewGuid() });
        await WaitForLock(f, "pg_advisory_xact_lock", "SELECT"); Assert.False(waiting.IsCompleted);
        await using (var revoke = f.Context()) { (await revoke.Users.SingleAsync(x => x.Id == f.CustomerId)).Status = UserStatus.Suspended; await revoke.SaveChangesAsync(); }
        await barrier.CommitAsync(); await Code(await waiting, 401, "UNAUTHORIZED");
        await GroupState(f, created, "AWAITING_TRANSFER", 1, "RESERVED");
        await using var check = f.Context(); Assert.Single(await check.BookingIdempotency.ToArrayAsync()); Assert.Single(await check.BookingPayments.ToArrayAsync());
    }

    private static Guid Id(JsonElement value) => value.GetProperty("bookingId").GetGuid();
    private static string Weekday(Fixture f) => DateOnly.Parse(f.Date).DayOfWeek.ToString().ToUpperInvariant();
    private static object Input(Fixture f, string start = "18:00", int duration = 120, string? weekday = null, DateOnly? end = null) =>
        new { courtId = f.CourtId, dayOfWeek = weekday ?? Weekday(f), localStartTime = start, durationMinutes = duration,
            startsOn = f.Date, endsOn = (end ?? DateOnly.Parse(f.Date).AddMonths(1)).ToString("yyyy-MM-dd") };
    private static async Task<JsonElement> QuoteSeries(Fixture f, HttpClient client, string start = "18:00", int duration = 120) =>
        await Data(await client.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f, start, duration)));
    private static Task<HttpResponseMessage> CreateResponse(HttpClient client, JsonElement quote, string key) =>
        Send(client, "/api/v1/booking-series", key, null, new { quoteId = quote.GetProperty("quoteId").GetGuid() });
    private static async Task<JsonElement> CreateSeries(HttpClient client, JsonElement quote, string key) => await Data(await CreateResponse(client, quote, key), 201);
    private static JsonElement[] Occurrences(JsonElement created) => created.GetProperty("series").GetProperty("occurrences").EnumerateArray().ToArray();
    private static async Task GroupState(Fixture f, JsonElement created, string status, long version, string allocationStatus)
    {
        var seriesId = created.GetProperty("series").GetProperty("seriesId").GetGuid(); await using var db = f.Context();
        var bookings = await db.Bookings.Where(x => x.SeriesId == seriesId).ToArrayAsync(); Assert.Equal(Occurrences(created).Length, bookings.Length);
        Assert.All(bookings, x => { Assert.Equal(status, x.Status); Assert.Equal(version, x.Version); });
        var ids = bookings.Select(x => x.AllocationId).ToArray(); var allocations = await db.CourtAllocations.Where(x => ids.Contains(x.Id)).ToArrayAsync();
        Assert.Equal(bookings.Length, allocations.Length); Assert.All(allocations, x => Assert.Equal(allocationStatus, x.Status));
    }
    private static async Task Availability(Fixture f, HttpClient client, string date, string status)
    {
        var data = await Data(await client.GetAsync($"/api/v1/venues/{f.VenueId}/availability?date={date}"));
        var court = data.GetProperty("courts").EnumerateArray().Single(x => x.GetProperty("courtId").GetGuid() == f.CourtId);
        var slots = court.GetProperty("slots").EnumerateArray().Where(x => string.CompareOrdinal(x.GetProperty("startsAt").GetString(), "18:00") >= 0 &&
            string.CompareOrdinal(x.GetProperty("startsAt").GetString(), "20:00") < 0).ToArray();
        Assert.Equal(4, slots.Length); Assert.All(slots, x => Assert.Equal(status, x.GetProperty("status").GetString()));
    }
}
