using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

// The shared F07 fixture creates and destroys a disposable real PostgreSQL/PostGIS database.
public sealed partial class FixedSeriesTests
{
    private static Task<HttpResponseMessage> CasualQuoteResponse(Fixture f, HttpClient client, string start = "18:00", string end = "20:00", string? date = null)
        => client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId = f.CourtId, date = date ?? f.Date, startsAt = start, endsAt = end });
    private static async Task<JsonElement> CasualQuote(Fixture f, HttpClient client, string start = "18:00", string end = "20:00", string? date = null)
        => await Data(await CasualQuoteResponse(f, client, start, end, date));
    private static Task<HttpResponseMessage> CasualCreate(HttpClient client, JsonElement quote, string key)
        => Send(client, "/api/v1/bookings", key, null, new { courtId = quote.GetProperty("courtId").GetGuid(), quoteId = quote.GetProperty("quoteId").GetGuid(),
            startsAt = quote.GetProperty("startsAt").GetDateTimeOffset(), endsAt = quote.GetProperty("endsAt").GetDateTimeOffset() });
    private static Task<HttpResponseMessage> Maintenance(Fixture f, HttpClient owner, string date, string start = "18:30", string end = "19:00")
        => owner.PostAsJsonAsync($"/api/v1/operator/courts/{f.CourtId}/maintenance", new { date, startsAt = start, endsAt = end, reason = "Reservation acceptance maintenance" });
    private static async Task NoBookingWrites(Fixture f)
    {
        await using var db = f.Context(); Assert.Empty(await db.Bookings.ToArrayAsync()); Assert.Empty(await db.BookingPayments.ToArrayAsync());
        Assert.Empty(await db.BookingSeries.ToArrayAsync()); Assert.Empty(await db.BookingIdempotency.ToArrayAsync()); Assert.Empty(await db.OutboxMessages.ToArrayAsync());
    }

    [Fact]
    public async Task Quote_hold_casual_requires_customer_owns_hold_blocks_other_customer_and_maintenance_then_consumes_same_allocation()
    {
        await using var f = await Fixture.Create(); using var guest = f.Factory.CreateClient(); using var owner = await f.Client("owner");
        using var customer = await f.Client("customer"); using var other = await f.Client("other");
        await Code(await CasualQuoteResponse(f, guest), 401, "UNAUTHORIZED"); await Code(await CasualQuoteResponse(f, owner), 403, "FORBIDDEN");
        var quote = await CasualQuote(f, customer); var quoteId = quote.GetProperty("quoteId").GetGuid(); Guid allocationId;
        await Availability(f, guest, f.Date, "RESERVED"); await NoBookingWrites(f);
        await using (var db = f.Context())
        {
            var hold = Assert.Single(await db.QuoteReservations.ToArrayAsync()); Assert.Equal(quoteId, hold.Id); Assert.Equal(f.CustomerId, hold.CustomerId);
            Assert.Equal("CASUAL", hold.Kind); Assert.Equal(quote.GetProperty("expiresAt").GetDateTimeOffset(), hold.ExpiresAt);
            var allocation = Assert.Single(await db.CourtAllocations.ToArrayAsync()); allocationId = allocation.Id;
            Assert.Equal("QUOTE_HOLD", allocation.Kind); Assert.Equal(quoteId, allocation.QuoteReservationId);
        }
        await Code(await CasualQuoteResponse(f, other), 409, "SLOT_UNAVAILABLE");
        await Code(await CasualCreate(other, quote, "stolen-quote"), 404, "NOT_FOUND");
        await Code(await Maintenance(f, owner, f.Date), 409, "SLOT_CONFLICT");
        var created = await Data(await CasualCreate(customer, quote, "consume-casual"), 201);
        Assert.Equal(Id(created), Id(await Data(await CasualCreate(customer, quote, "consume-casual"), 201)));
        await Code(await CasualCreate(customer, quote, "second-intent"), 409, "QUOTE_CONSUMED");
        await using var check = f.Context(); var booking = Assert.Single(await check.Bookings.ToArrayAsync()); Assert.Equal(allocationId, booking.AllocationId);
        var converted = Assert.Single(await check.CourtAllocations.ToArrayAsync()); Assert.Equal(allocationId, converted.Id); Assert.Equal("BOOKING", converted.Kind);
        Assert.NotNull((await check.QuoteReservations.SingleAsync()).ConsumedAt); Assert.Single(await check.BookingPayments.ToArrayAsync());
    }

    [Fact]
    public async Task Quote_hold_fixed_locks_every_date_and_conflict_has_no_partial_hold_or_booking()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var other = await f.Client("other");
        using var owner = await f.Client("owner"); using var guest = f.Factory.CreateClient();
        var quote = await QuoteSeries(f, customer); var quoteId = quote.GetProperty("quoteId").GetGuid(); var dates = quote.GetProperty("occurrences").EnumerateArray().ToArray();
        await NoBookingWrites(f); Guid[] ids;
        await using (var db = f.Context())
        {
            var allocations = await db.CourtAllocations.ToArrayAsync(); Assert.Equal(dates.Length, allocations.Length);
            Assert.All(allocations, x => { Assert.Equal("QUOTE_HOLD", x.Kind); Assert.Equal(quoteId, x.QuoteReservationId); }); ids = allocations.Select(x => x.Id).ToArray();
        }
        foreach (var occurrence in dates) await Availability(f, guest, occurrence.GetProperty("date").GetString()!, "RESERVED");
        var blockedDate = dates[3].GetProperty("date").GetString()!;
        await Code(await CasualQuoteResponse(f, other, "18:30", "19:00", blockedDate), 409, "SLOT_UNAVAILABLE");
        await Code(await Maintenance(f, owner, blockedDate), 409, "SLOT_CONFLICT");
        var conflict = await QuoteSeries(f, other); Assert.False(conflict.GetProperty("canCreate").GetBoolean()); Assert.Equal(JsonValueKind.Null, conflict.GetProperty("quoteId").ValueKind);
        await using (var db = f.Context()) { Assert.Equal(dates.Length, await db.CourtAllocations.CountAsync()); Assert.Single(await db.QuoteReservations.ToArrayAsync()); }
        await Code(await CreateResponse(other, quote, "steal-series"), 404, "NOT_FOUND");
        var created = await CreateSeries(customer, quote, "consume-series");
        await Code(await CreateResponse(customer, quote, "second-series-intent"), 409, "QUOTE_CONSUMED");
        await using var check = f.Context(); Assert.Equal(ids.Order(), (await check.Bookings.Select(x => x.AllocationId).ToArrayAsync()).Order());
        Assert.All(await check.CourtAllocations.ToArrayAsync(), x => Assert.Equal("BOOKING", x.Kind)); Assert.Single(await check.BookingPayments.ToArrayAsync());
        Assert.Equal(dates.Length, Occurrences(created).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quote_hold_expires_at_exact_boundary_public_reads_and_new_quote_work_without_worker(bool series)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var other = await f.Client("other");
        using var guest = f.Factory.CreateClient();
        var quote = series ? await QuoteSeries(f, customer) : await CasualQuote(f, customer);
        var expiry = quote.GetProperty("expiresAt").GetDateTimeOffset();
        f.Clock.Set(expiry.AddTicks(-10)); await Availability(f, guest, f.Date, "RESERVED");
        f.Clock.Set(expiry); await Availability(f, guest, f.Date, "AVAILABLE");
        await using (var db = f.Context()) Assert.All(await db.CourtAllocations.ToArrayAsync(), x => Assert.Equal("RESERVED", x.Status));
        if (series) await Code(await CreateResponse(customer, quote, "expired-series-hold"), 409, "QUOTE_EXPIRED");
        else await Code(await CasualCreate(customer, quote, "expired-casual-hold"), 409, "QUOTE_EXPIRED");
        var next = await CasualQuote(f, other); Assert.NotEqual(quote.GetProperty("quoteId").GetGuid(), next.GetProperty("quoteId").GetGuid());
        await Availability(f, guest, f.Date, "RESERVED");
        await using var check = f.Context(); var old = await check.QuoteReservations.SingleAsync(x => x.Id == quote.GetProperty("quoteId").GetGuid()); Assert.NotNull(old.ReleasedAt);
        Assert.Single(await check.CourtAllocations.Where(x => x.Status == "RESERVED").ToArrayAsync()); Assert.Empty(await check.Bookings.ToArrayAsync());
    }

    [Fact]
    public async Task Quote_hold_refresh_and_casual_fixed_replacement_preserve_original_deadline_and_failed_requote_keeps_old_hold()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var other = await f.Client("other");
        var first = await CasualQuote(f, customer); var deadline = first.GetProperty("expiresAt").GetDateTimeOffset();
        f.Clock.Set(f.Clock.GetUtcNow().AddSeconds(25)); var refresh = await CasualQuote(f, customer);
        Assert.Equal(deadline, refresh.GetProperty("expiresAt").GetDateTimeOffset());
        await Code(await CasualCreate(customer, first, "old-refresh"), 409, "QUOTE_EXPIRED");
        f.Clock.Set(f.Clock.GetUtcNow().AddSeconds(25)); var series = await QuoteSeries(f, customer);
        Assert.Equal(deadline, series.GetProperty("expiresAt").GetDateTimeOffset());
        await Code(await CasualCreate(customer, refresh, "old-casual"), 409, "QUOTE_EXPIRED");
        var count = series.GetProperty("occurrenceCount").GetInt32();
        await using (var db = f.Context()) Assert.Equal(count, await db.CourtAllocations.CountAsync(x => x.Status == "RESERVED"));
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f, duration: 90)), 400, "VALIDATION_FAILED");
        await CasualQuote(f, other, "20:00", "21:00");
        var conflict = await QuoteSeries(f, customer, "19:00"); Assert.False(conflict.GetProperty("canCreate").GetBoolean());
        await using (var db = f.Context())
        {
            var old = await db.QuoteReservations.SingleAsync(x => x.Id == series.GetProperty("quoteId").GetGuid()); Assert.Null(old.ReleasedAt); Assert.Null(old.ConsumedAt);
            Assert.Equal(count, await db.CourtAllocations.CountAsync(x => x.QuoteReservationId == old.Id && x.Status == "RESERVED"));
        }
        f.Clock.Set(f.Clock.GetUtcNow().AddSeconds(25)); var final = await CasualQuote(f, customer, "17:00", "18:00");
        Assert.Equal(deadline, final.GetProperty("expiresAt").GetDateTimeOffset());
        await Code(await CreateResponse(customer, series, "old-series"), 409, "QUOTE_EXPIRED");
        await using var check = f.Context(); Assert.Equal(2, await check.CourtAllocations.CountAsync(x => x.Status == "RESERVED"));
        Assert.Single(await check.QuoteReservations.Where(x => x.CustomerId == f.CustomerId && x.ReleasedAt == null && x.ConsumedAt == null).ToArrayAsync());
    }

    [Fact]
    public async Task Quote_hold_two_customers_concurrently_receive_one_complete_fixed_reservation()
    {
        await using var f = await Fixture.Create(); using var first = await f.Client("customer"); using var secondHost = new ApiFactory(f.Connection, f.Clock);
        using var second = secondHost.CreateClient(); await f.Login(second, "other");
        var replies = await Task.WhenAll(QuoteSeries(f, first), QuoteSeries(f, second));
        var winner = Assert.Single(replies, x => x.GetProperty("canCreate").GetBoolean());
        var loser = Assert.Single(replies, x => !x.GetProperty("canCreate").GetBoolean()); Assert.Equal(JsonValueKind.Null, loser.GetProperty("quoteId").ValueKind);
        await using var db = f.Context(); Assert.Single(await db.QuoteReservations.ToArrayAsync());
        Assert.Equal(winner.GetProperty("occurrenceCount").GetInt32(), await db.CourtAllocations.CountAsync()); Assert.Empty(await db.Bookings.ToArrayAsync());
    }

    [Fact]
    public async Task Quote_hold_database_exclusion_prevents_raw_overlapping_allocation_and_court_scope_mismatch()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); var quote = await CasualQuote(f, customer);
        await using (var db = f.Context())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO court_allocations (id,court_id,kind,starts_at,ends_at,status,created_at) VALUES ({Guid.CreateVersion7()},{f.CourtId},'MAINTENANCE',{quote.GetProperty("startsAt").GetDateTimeOffset()},{quote.GetProperty("endsAt").GetDateTimeOffset()},'RESERVED',{f.Clock.GetUtcNow()})"));
            Assert.Equal("23P01", error.SqlState); await tx.RollbackAsync();
        }
        Guid secondCourt;
        await using (var db = f.Context()) { var court = new Court { VenueId = f.VenueId, Name = "Scope test", Status = "ACTIVE", CreatedAt = f.Clock.GetUtcNow() }; db.Courts.Add(court); await db.SaveChangesAsync(); secondCourt = court.Id; }
        await using (var db = f.Context())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE court_allocations SET court_id={secondCourt} WHERE quote_reservation_id={quote.GetProperty("quoteId").GetGuid()}"));
            Assert.Equal("23503", error.SqlState); await tx.RollbackAsync();
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quote_hold_multiple_workers_release_whole_period_once_and_preserve_consumed_booking(bool consumed)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var quote = await QuoteSeries(f, customer); JsonElement? created = consumed ? await CreateSeries(customer, quote, "worker-consumed") : null;
        var later = quote.GetProperty("expiresAt").GetDateTimeOffset();
        var processed = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => { await using var db = f.Context(); return await QuoteReservations.ProcessOne(db, later, CancellationToken.None); }));
        Assert.Equal(consumed ? 0 : 1, processed.Count(x => x));
        await using var check = f.Context(); Assert.False(await QuoteReservations.ProcessOne(check, later, CancellationToken.None));
        var hold = await check.QuoteReservations.SingleAsync();
        if (consumed) {
            Assert.NotNull(hold.ConsumedAt); Assert.Null(hold.ReleasedAt); Assert.Single(await check.BookingPayments.ToArrayAsync());
            Assert.All(await check.CourtAllocations.ToArrayAsync(), x => { Assert.Equal("RESERVED", x.Status); Assert.Equal("BOOKING", x.Kind); });
            await GroupState(f, created!.Value, "AWAITING_TRANSFER", 1, "RESERVED");
        } else {
            Assert.NotNull(hold.ReleasedAt); Assert.Null(hold.ConsumedAt); Assert.All(await check.CourtAllocations.ToArrayAsync(), x => Assert.Equal("RELEASED", x.Status));
            await NoBookingWrites(f);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quote_hold_legacy_quote_without_reservation_cannot_create_or_acquire_a_slot(bool series)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var quote = series ? await QuoteSeries(f, customer) : await CasualQuote(f, customer); var id = quote.GetProperty("quoteId").GetGuid();
        // Simulate a valid historical quote persisted by the old release: same pricing snapshot,
        // but no reservation ownership. Remove its test-created hold before replaying that quote.
        await using (var db = f.Context()) {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM court_allocations WHERE quote_reservation_id={id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM quote_reservations WHERE id={id}");
        }
        if (series) await Code(await CreateResponse(customer, quote, "legacy-series-quote"), 409, "QUOTE_EXPIRED");
        else await Code(await CasualCreate(customer, quote, "legacy-casual-quote"), 409, "QUOTE_EXPIRED");
        await NoBookingWrites(f); await Availability(f, customer, f.Date, "AVAILABLE");
    }

    [Fact]
    public async Task Quote_hold_create_waiting_for_court_lock_rechecks_expiry_and_maintenance_takes_released_slot()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var quote = await QuoteSeries(f, customer); var date = quote.GetProperty("occurrences")[3].GetProperty("date").GetString()!;
        await using var holder = f.Context(); await using var barrier = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM courts WHERE id={f.CourtId} FOR UPDATE");
        var create = CreateResponse(customer, quote, "waiting-expiry"); await WaitForLock(f, "courts", "FOR UPDATE"); Assert.False(create.IsCompleted);
        f.Clock.Set(quote.GetProperty("expiresAt").GetDateTimeOffset());
        await barrier.CommitAsync(); await Code(await create, 409, "QUOTE_EXPIRED");
        await Data(await Maintenance(f, owner, date), 201);
        await NoBookingWrites(f); await using var check = f.Context();
        Assert.Single(await check.CourtAllocations.Where(x => x.Status == "RESERVED").ToArrayAsync());
        Assert.Equal("MAINTENANCE", (await check.CourtAllocations.SingleAsync(x => x.Status == "RESERVED")).Kind);
        Assert.All(await check.CourtAllocations.Where(x => x.Kind == "QUOTE_HOLD").ToArrayAsync(), x => Assert.Equal("RELEASED", x.Status));
    }

    [Fact]
    public async Task Quote_hold_migration_down_upgrade_repeat_preserves_fixed_payment_evidence_and_allocations()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var quote = await QuoteSeries(f, customer); var created = await CreateSeries(customer, quote, "migration-fixed");
        await Data(await Send(customer, ReportRoute(Id(created)), "migration-report", 1, new { note = "Preserve original evidence" }));
        await CasualQuote(f, customer, "17:00", "18:00"); Guid[] bookingIds, allocationIds; string recipient; string[] hashes;
        await using (var db = f.Context())
        {
            bookingIds = await db.Bookings.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            allocationIds = await db.Bookings.OrderBy(x => x.AllocationId).Select(x => x.AllocationId).ToArrayAsync();
            recipient = (await db.BookingPayments.SingleAsync()).RecipientSnapshot; hashes = await db.BookingIdempotency.OrderBy(x => x.Operation).Select(x => x.RequestHash).ToArrayAsync();
            await db.GetService<IMigrator>().MigrateAsync("20261007150835_F07FixedSeries");
            await using var connection = new NpgsqlConnection(f.Connection); await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT count(*) FROM court_allocations WHERE kind='BOOKING'", connection); Assert.Equal((long)bookingIds.Length, (long)(await command.ExecuteScalarAsync())!);
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        }
        var replay = await CreateSeries(customer, quote, "migration-fixed"); Assert.Equal(Id(created), Id(replay));
        await GroupState(f, replay, "AWAITING_OWNER_CONFIRMATION", 2, "RESERVED");
        await using var check = f.Context(); Assert.Equal(bookingIds, await check.Bookings.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Equal(allocationIds, await check.CourtAllocations.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Equal(recipient, (await check.BookingPayments.SingleAsync()).RecipientSnapshot); Assert.Single(await check.PaymentEvidence.ToArrayAsync());
        Assert.Equal(hashes, await check.BookingIdempotency.OrderBy(x => x.Operation).Select(x => x.RequestHash).ToArrayAsync()); Assert.Empty(await check.QuoteReservations.ToArrayAsync());
    }

    [Theory]
    [InlineData("status", 401, "UNAUTHORIZED")]
    [InlineData("role", 403, "FORBIDDEN")]
    [InlineData("ownership", 404, "NOT_FOUND")]
    public async Task Quote_hold_casual_replay_waiting_on_advisory_lock_reauthorizes_before_changed_intent(string change, int status, string code)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var quote = await CasualQuote(f, customer); var created = await Data(await CasualCreate(customer, quote, "replay-recheck"), 201);
        await using var holder = f.Context(); await using var barrier = await holder.Database.BeginTransactionAsync();
        var lockKey = $"CASUAL_CREATE:{f.CustomerId}:replay-recheck";
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))");
        var waiting = Send(customer, "/api/v1/bookings", "replay-recheck", null, new { courtId = f.CourtId, quoteId = Guid.NewGuid(),
            startsAt = quote.GetProperty("startsAt").GetDateTimeOffset(), endsAt = quote.GetProperty("endsAt").GetDateTimeOffset() });
        await WaitForLock(f, "pg_advisory_xact_lock", "SELECT"); Assert.False(waiting.IsCompleted);
        await using (var revoke = f.Context()) {
            var user = await revoke.Users.SingleAsync(x => x.Id == f.CustomerId);
            if (change == "status") user.Status = UserStatus.Suspended;
            else if (change == "role") user.AccountType = AccountType.VenueOperator;
            else await revoke.Database.ExecuteSqlInterpolatedAsync($"UPDATE bookings SET customer_id={f.OtherCustomerId} WHERE id={Id(created)}");
            await revoke.SaveChangesAsync();
        }
        await barrier.CommitAsync(); await Code(await waiting, status, code);
        await using var check = f.Context(); Assert.Single(await check.Bookings.ToArrayAsync()); Assert.Single(await check.BookingPayments.ToArrayAsync());
        Assert.Single(await check.BookingIdempotency.ToArrayAsync()); Assert.Single(await check.CourtAllocations.ToArrayAsync());
    }

}
