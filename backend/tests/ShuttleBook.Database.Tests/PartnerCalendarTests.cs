using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

// Reuses the disposable real PostGIS/F07 fixture, including API authentication and real commands.
public sealed partial class FixedSeriesTests
{
    private static string CalendarRoute(Fixture f, string? date = null) => $"/api/v1/operator/venues/{f.VenueId}/calendar?date={date ?? f.Date}";
    private static JsonElement CalendarCourt(JsonElement calendar, Guid courtId) => calendar.GetProperty("courts")
        .EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == courtId);
    private static JsonElement CalendarSlot(JsonElement court, string start) => court.GetProperty("slots")
        .EnumerateArray().Single(x => x.GetProperty("startsAt").GetString() == start);

    [Fact]
    public async Task Calendar_validates_query_identity_scope_and_hides_private_payment_and_customer_fields()
    {
        await using var f = await Fixture.Create(); using var guest = f.Factory.CreateClient();
        using var owner = await f.Client("owner"); using var other = await f.Client("owner-b"); using var customer = await f.Client("customer");
        await Code(await guest.GetAsync(CalendarRoute(f)), 401, "UNAUTHORIZED");
        await Code(await customer.GetAsync(CalendarRoute(f)), 403, "FORBIDDEN");
        await Code(await other.GetAsync(CalendarRoute(f)), 404, "NOT_FOUND");
        await Code(await owner.GetAsync(CalendarRoute(f) + $"&courtId={Guid.NewGuid()}"), 404, "NOT_FOUND");
        foreach (var query in new[] { "", "date=2026-02-30", "date=2026-1-01", "date=0001-01-01", "date=9999-01-01",
            $"date={f.Date}&date={f.Date}", $"date={f.Date}&limit=100", $"date={f.Date}&courtId=", $"date={f.Date}&courtId={Guid.Empty}" })
            await Code(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/calendar?{query}"), 400, "VALIDATION_FAILED");
        var booking = await f.Book(customer, key: "calendar-private");
        using var response = await owner.GetAsync(CalendarRoute(f));
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var calendar = await Data(response); var raw = calendar.GetRawText();
        foreach (var field in new[] { "customerId", "contact", "accountNumber", "recipientSnapshot", "qrUploadId", "objectKey", "proofUploadId", "quoteReservationId" })
            Assert.DoesNotContain($"\"{field}\"", raw);
        Assert.DoesNotContain("1234567890", raw);
        Assert.Equal(Id(booking), CalendarCourt(calendar, f.CourtId).GetProperty("events")[0].GetProperty("paymentAnchorId").GetGuid());
        await using (var db = f.Context()) { (await db.BusinessMemberships.SingleAsync(m => m.BusinessId == f.BusinessId)).Status = "REVOKED"; await db.SaveChangesAsync(); }
        await Code(await owner.GetAsync(CalendarRoute(f)), 404, "NOT_FOUND");
    }

    [Fact]
    public async Task Calendar_quote_hold_expires_at_boundary_without_worker_and_get_never_releases_allocations()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner"); using var customer = await f.Client("customer");
        var quote = await CasualQuote(f, customer);
        var before = await Data(await owner.GetAsync(CalendarRoute(f))); var court = CalendarCourt(before, f.CourtId);
        var hold = Assert.Single(court.GetProperty("events").EnumerateArray());
        Assert.Equal("QUOTE_HOLD", hold.GetProperty("kind").GetString()); Assert.True(hold.GetProperty("blocksBooking").GetBoolean());
        Assert.Equal(JsonValueKind.Null, hold.GetProperty("paymentAnchorId").ValueKind);
        Assert.Equal("QUOTE_HOLD", CalendarSlot(court, "18:00").GetProperty("occupancy").GetString());
        f.Clock.Set(quote.GetProperty("expiresAt").GetDateTimeOffset());
        var after = await Data(await owner.GetAsync(CalendarRoute(f))); court = CalendarCourt(after, f.CourtId);
        Assert.Equal(f.Clock.GetUtcNow(), after.GetProperty("generatedAt").GetDateTimeOffset());
        Assert.Empty(court.GetProperty("events").EnumerateArray());
        Assert.Equal("FREE", CalendarSlot(court, "18:00").GetProperty("occupancy").GetString());
        Assert.True(CalendarSlot(court, "18:00").GetProperty("canRequestNewBooking").GetBoolean());
        await using var db = f.Context(); Assert.Null((await db.QuoteReservations.SingleAsync()).ReleasedAt);
        Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync()).Status);
        Assert.Empty(await db.Bookings.ToArrayAsync()); Assert.Empty(await db.OutboxMessages.ToArrayAsync());
    }

    [Fact]
    public async Task Calendar_keeps_unprocessed_booking_deadline_reserved_and_retains_released_history_without_blocking()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner"); using var customer = await f.Client("customer");
        var created = await f.Book(customer, key: "calendar-deadline");
        DateTimeOffset deadline;
        await using (var db = f.Context()) deadline = (await db.Bookings.SingleAsync()).PaymentDeadline;
        f.Clock.Set(deadline.AddSeconds(1));
        var court = CalendarCourt(await Data(await owner.GetAsync(CalendarRoute(f))), f.CourtId);
        var reserved = Assert.Single(court.GetProperty("events").EnumerateArray());
        Assert.Equal("AWAITING_TRANSFER", reserved.GetProperty("bookingStatus").GetString());
        Assert.Equal("AWAITING_TRANSFER", reserved.GetProperty("paymentStatus").GetString());
        Assert.True(reserved.GetProperty("blocksBooking").GetBoolean());
        Assert.Equal("BOOKING", CalendarSlot(court, "18:00").GetProperty("occupancy").GetString());
        await using (var worker = f.Context()) Assert.True(await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None));
        court = CalendarCourt(await Data(await owner.GetAsync(CalendarRoute(f))), f.CourtId);
        var history = Assert.Single(court.GetProperty("events").EnumerateArray());
        Assert.Equal(Id(created), history.GetProperty("occurrenceId").GetGuid());
        Assert.Equal("EXPIRED", history.GetProperty("bookingStatus").GetString()); Assert.False(history.GetProperty("blocksBooking").GetBoolean());
        Assert.Equal("FREE", CalendarSlot(court, "18:00").GetProperty("occupancy").GetString());
    }

    [Fact]
    public async Task Calendar_fixed_occurrence_uses_actual_date_anchor_payment_and_exact_occurrence_amount_outside_new_hours_and_in_past()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner"); using var customer = await f.Client("customer");
        var special = DateOnly.Parse(f.Date).AddDays(21);
        await using (var db = f.Context())
        {
            db.PricingRules.Add(new PricingRule { CourtId = f.CourtId, DayOfWeek = (int)special.DayOfWeek,
                StartsOn = special, EndsOn = special, StartsAt = new(18, 0), EndsAt = new(20, 0), PricePerSlot = 250000, Priority = 10 });
            await db.SaveChangesAsync();
        }
        var created = await CreateSeries(customer, await QuoteSeries(f, customer), "calendar-series"); var anchor = Id(created);
        await Data(await Send(customer, ReportRoute(anchor), "calendar-report", 1, Evidence()));
        await Data(await Send(owner, RejectRoute(anchor), "calendar-review", 2, Decision("NEEDS_REVIEW", "Calendar needs test evidence")));
        Guid occurrence; DateTimeOffset endsAt;
        await using (var db = f.Context())
        {
            var booking = await db.Bookings.SingleAsync(x => x.LocalDate == special); occurrence = booking.Id; endsAt = booking.EndsAt;
            (await db.CourtOperatingHours.SingleAsync(x => x.CourtId == f.CourtId && x.DayOfWeek == (int)special.DayOfWeek)).OpensAt = new(20, 0);
            (await db.Venues.SingleAsync()).Status = "DRAFT"; (await db.Courts.SingleAsync()).Status = "INACTIVE"; await db.SaveChangesAsync();
        }
        f.Clock.Set(endsAt.AddDays(1));
        var calendar = await Data(await owner.GetAsync(CalendarRoute(f, special.ToString("yyyy-MM-dd")))); var court = CalendarCourt(calendar, f.CourtId);
        var e = Assert.Single(court.GetProperty("events").EnumerateArray());
        Assert.Equal(occurrence, e.GetProperty("occurrenceId").GetGuid()); Assert.Equal(anchor, e.GetProperty("paymentAnchorId").GetGuid());
        Assert.NotEqual(occurrence, anchor); Assert.Equal("RECURRING_OCCURRENCE", e.GetProperty("bookingType").GetString());
        Assert.Equal("1000000", e.GetProperty("occurrenceAmount").GetString());
        Assert.Equal(created.GetProperty("amount").GetInt64().ToString(CultureInfo.InvariantCulture), e.GetProperty("paymentTotalAmount").GetString());
        Assert.Equal("NEEDS_REVIEW", e.GetProperty("bookingStatus").GetString()); Assert.Equal("NEEDS_REVIEW", e.GetProperty("paymentStatus").GetString());
        Assert.True(e.GetProperty("isPast").GetBoolean()); Assert.True(e.GetProperty("blocksBooking").GetBoolean());
        var slot = CalendarSlot(court, "18:00"); Assert.True(slot.GetProperty("isPast").GetBoolean());
        Assert.False(slot.GetProperty("isOpen").GetBoolean()); Assert.Equal("BOOKING", slot.GetProperty("occupancy").GetString());
        Assert.False(slot.GetProperty("canRequestNewBooking").GetBoolean());
        await using var check = f.Context(); Assert.All(await check.CourtAllocations.ToArrayAsync(), x => Assert.Equal("RESERVED", x.Status));
    }

    [Fact]
    public async Task Calendar_maintenance_cancel_keeps_history_and_missing_price_does_not_mean_bookable()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner");
        var repair = await Data(await Maintenance(f, owner, f.Date), 201);
        var court = CalendarCourt(await Data(await owner.GetAsync(CalendarRoute(f))), f.CourtId);
        Assert.Equal("MAINTENANCE", CalendarSlot(court, "18:30").GetProperty("occupancy").GetString());
        var e = Assert.Single(court.GetProperty("events").EnumerateArray()); Assert.True(e.GetProperty("blocksBooking").GetBoolean());
        var maintenanceId = e.GetProperty("maintenanceId").GetGuid();
        await Data(await owner.PostAsync($"/api/v1/operator/courts/{f.CourtId}/maintenance/{maintenanceId}/cancel", null));
        await using (var db = f.Context()) { db.PricingRules.RemoveRange(await db.PricingRules.ToArrayAsync()); await db.SaveChangesAsync(); }
        court = CalendarCourt(await Data(await owner.GetAsync(CalendarRoute(f))), f.CourtId);
        e = Assert.Single(court.GetProperty("events").EnumerateArray()); Assert.Equal("CANCELLED", e.GetProperty("maintenanceStatus").GetString());
        Assert.False(e.GetProperty("blocksBooking").GetBoolean());
        var slot = CalendarSlot(court, "18:30"); Assert.Equal("FREE", slot.GetProperty("occupancy").GetString());
        Assert.False(slot.GetProperty("canRequestNewBooking").GetBoolean()); Assert.Equal(JsonValueKind.Null, slot.GetProperty("pricePerSlotExact").ValueKind);
        Assert.False(court.GetProperty("openRanges")[0].GetProperty("isPriced").GetBoolean());
    }

    [Fact]
    public async Task Calendar_vietnam_midnight_and_dst_days_have_correct_bounds_and_numeric_court_sort()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner");
        await using (var db = f.Context())
        {
            foreach (var number in new[] { 10, 2, 1 }) db.Courts.Add(new Court { VenueId = f.VenueId, Name = CourtNumbers.Name(number), CreatedAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
        }
        var calendar = await Data(await owner.GetAsync(CalendarRoute(f)));
        Assert.Equal(new[] { "Sân 1", "Sân 2", "Sân 10" }, calendar.GetProperty("courts").EnumerateArray().Take(3).Select(x => x.GetProperty("name").GetString()));
        Assert.Equal(DateOnly.Parse(f.Date).AddDays(-1).ToDateTime(new(17, 0)), calendar.GetProperty("dayStartsAt").GetDateTimeOffset().UtcDateTime);
        Assert.Equal(48, CalendarCourt(calendar, f.CourtId).GetProperty("slots").GetArrayLength());
        Assert.Equal("24:00", CalendarCourt(calendar, f.CourtId).GetProperty("slots")[47].GetProperty("endsAt").GetString());
        await using (var db = f.Context()) { (await db.Venues.SingleAsync()).Timezone = "Europe/Berlin"; await db.SaveChangesAsync(); }
        foreach (var (date, count) in new[] { ("2026-03-29", 46), ("2026-10-25", 50) })
        {
            calendar = await Data(await owner.GetAsync(CalendarRoute(f, date)));
            var slots = CalendarCourt(calendar, f.CourtId).GetProperty("slots").EnumerateArray().ToArray(); Assert.Equal(count, slots.Length);
            Assert.Equal(TimeSpan.FromMinutes(count * 30), calendar.GetProperty("dayEndsAt").GetDateTimeOffset() - calendar.GetProperty("dayStartsAt").GetDateTimeOffset());
            for (var index = 1; index < slots.Length; index++) Assert.Equal(slots[index - 1].GetProperty("endsAtUtc").GetDateTimeOffset(), slots[index].GetProperty("startsAtUtc").GetDateTimeOffset());
        }
        await using (var db = f.Context())
        {
            var hour = await db.CourtOperatingHours.SingleAsync(x => x.CourtId == f.CourtId && x.DayOfWeek == 0);
            hour.OpensAt = new(0, 0); hour.ClosesAt = new(4, 0);
            var rule = await db.PricingRules.SingleAsync(x => x.CourtId == f.CourtId && x.DayOfWeek == 0);
            rule.StartsAt = new(0, 0); rule.EndsAt = new(4, 0); await db.SaveChangesAsync();
        }
        f.Clock.Set(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        calendar = await Data(await owner.GetAsync(CalendarRoute(f, "2026-10-25")));
        var fallCourt = CalendarCourt(calendar, f.CourtId);
        var ambiguous = fallCourt.GetProperty("slots").EnumerateArray().Where(x => x.GetProperty("startsAt").GetString() is "02:00" or "02:30").ToArray();
        Assert.Equal(4, ambiguous.Length);
        Assert.All(ambiguous, slot => Assert.False(slot.GetProperty("canRequestNewBooking").GetBoolean()));
        Assert.True(CalendarSlot(fallCourt, "03:00").GetProperty("canRequestNewBooking").GetBoolean());
    }

    [Fact]
    public async Task Calendar_payload_limit_is_explicit_and_filtering_one_court_still_returns_complete_day()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner");
        await using (var db = f.Context())
        {
            for (var index = 1; index <= 200; index++) db.Courts.Add(new Court { VenueId = f.VenueId, Name = CourtNumbers.Name(index), CreatedAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
        }
        await Code(await owner.GetAsync(CalendarRoute(f)), 409, "CALENDAR_TOO_LARGE");
        var calendar = await Data(await owner.GetAsync(CalendarRoute(f) + $"&courtId={f.CourtId}"));
        Assert.Single(calendar.GetProperty("courts").EnumerateArray()); Assert.Equal(48, CalendarCourt(calendar, f.CourtId).GetProperty("slots").GetArrayLength());
    }

    [Fact]
    public async Task Calendar_more_than_one_hundred_events_are_complete_and_event_cap_never_returns_partial_calendar()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner");
        var startsAt = new DateTimeOffset(DateOnly.Parse(f.Date).ToDateTime(new(11, 0)), TimeSpan.Zero);
        async Task SeedHistory(int count)
        {
            await using var db = f.Context();
            for (var index = 0; index < count; index++)
            {
                var allocation = new CourtAllocation { CourtId = f.CourtId, Kind = "MAINTENANCE", Status = "RELEASED",
                    StartsAt = startsAt, EndsAt = startsAt.AddMinutes(30), CreatedAt = f.Clock.GetUtcNow(), ReleasedAt = f.Clock.GetUtcNow() };
                db.CourtAllocations.Add(allocation);
                db.CourtMaintenance.Add(new CourtMaintenance { CourtId = f.CourtId, AllocationId = allocation.Id, Status = "CANCELLED",
                    Reason = "Historical maintenance test", CreatedBy = f.OwnerId, CreatedAt = f.Clock.GetUtcNow(), CancelledBy = f.OwnerId, CancelledAt = f.Clock.GetUtcNow() });
            }
            await db.SaveChangesAsync();
        }
        await SeedHistory(128);
        var calendar = await Data(await owner.GetAsync(CalendarRoute(f)));
        var events = CalendarCourt(calendar, f.CourtId).GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal(128, events.Length); Assert.All(events, e => Assert.False(e.GetProperty("blocksBooking").GetBoolean()));
        await SeedHistory(4873);
        await Code(await owner.GetAsync(CalendarRoute(f)), 409, "CALENDAR_TOO_LARGE");
    }

    [Fact]
    public async Task Calendar_read_snapshot_stays_consistent_when_payment_confirmation_commits_between_queries()
    {
        await using var f = await Fixture.Create(); using var owner = await f.Client("owner"); using var customer = await f.Client("customer");
        var created = await f.Book(customer, key: "calendar-snapshot"); var id = Id(created);
        await Data(await Send(customer, ReportRoute(id), "calendar-snapshot-report", 1, Evidence()));
        var barrier = new CalendarReadBarrier();
        using var readerFactory = f.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<ShuttleBookDbContext>((_, options) => options.AddInterceptors(barrier))));
        using var reader = readerFactory.CreateClient(); reader.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var pendingRead = reader.GetAsync(CalendarRoute(f));
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await Data(await Send(owner, ConfirmRoute(id), "calendar-snapshot-confirm", 2, Confirmation(created)));
        }
        finally { barrier.Release.TrySetResult(); }
        var calendar = await Data(await pendingRead);
        var e = Assert.Single(CalendarCourt(calendar, f.CourtId).GetProperty("events").EnumerateArray());
        Assert.Equal("AWAITING_OWNER_CONFIRMATION", e.GetProperty("bookingStatus").GetString());
        Assert.Equal("TRANSFER_REPORTED", e.GetProperty("paymentStatus").GetString());
        e = Assert.Single(CalendarCourt(await Data(await owner.GetAsync(CalendarRoute(f))), f.CourtId).GetProperty("events").EnumerateArray());
        Assert.Equal("CONFIRMED", e.GetProperty("bookingStatus").GetString()); Assert.Equal("PAID", e.GetProperty("paymentStatus").GetString());
    }

    private sealed class CalendarReadBarrier : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int entered;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM court_allocations", StringComparison.Ordinal) && Interlocked.Exchange(ref entered, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
