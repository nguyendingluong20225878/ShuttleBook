using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Bookings;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Operations;

/// <summary>Private owner calendar. Allocations, not a page of payment anchors, decide occupancy.</summary>
public static class PartnerCalendarEndpoints
{
    private const int MaximumCourts = 200;
    private const int MaximumEvents = 5000;

    public static void MapPartnerCalendarEndpoints(this WebApplication app) => app
        .MapGet("/api/v1/operator/venues/{venueId:guid}/calendar", Calendar).RequireAuthorization();

    private static async Task<IResult> Calendar(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, IConfiguration configuration, Guid venueId, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var query = http.Request.Query;
        if (query.Any(x => x.Key is not ("date" or "courtId") || x.Value.Count != 1) ||
            !DateOnly.TryParseExact(query["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) || date.Year is < 1000 or > 9998)
            return Error(http, 400, "VALIDATION_FAILED");
        Guid? courtId = null;
        if (query.ContainsKey("courtId"))
        {
            if (!Guid.TryParse(query["courtId"], out var parsed) || parsed == Guid.Empty)
                return Error(http, 400, "VALIDATION_FAILED");
            courtId = parsed;
        }
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId)) return Error(http, 401, "UNAUTHORIZED");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var now = clock.GetUtcNow();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.Status == UserStatus.Suspended) return Error(http, 401, "UNAUTHORIZED");
        if (user.AccountType != AccountType.VenueOperator || user.Status != UserStatus.Active)
            return Error(http, 403, "FORBIDDEN");
        // Venue/court publication is deliberately not a read guard: old bookings remain visible.
        if (!await PaymentEndpoints.HasVenueScope(db, userId, venueId, ct)) return Error(http, 404, "NOT_FOUND");
        var venue = await db.Venues.AsNoTracking().SingleAsync(x => x.Id == venueId, ct);
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone); }
        catch (TimeZoneNotFoundException) { return Error(http, 503, "SCHEDULE_UNAVAILABLE"); }
        catch (InvalidTimeZoneException) { return Error(http, 503, "SCHEDULE_UNAVAILABLE"); }
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var nextMidnight = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (!TryUtc(zone, midnight, out var dayStartsAt) || !TryUtc(zone, nextMidnight, out var dayEndsAt))
            return Error(http, 409, "SCHEDULE_UNAVAILABLE");
        var courts = await db.Courts.AsNoTracking().Where(x => x.VenueId == venueId &&
            (!courtId.HasValue || x.Id == courtId.Value)).OrderBy(x => x.Id).Take(MaximumCourts + 1).ToListAsync(ct);
        if (courts.Count > MaximumCourts) return Error(http, 409, "CALENDAR_TOO_LARGE");
        if (courtId.HasValue && courts.Count == 0) return Error(http, 404, "NOT_FOUND");
        courts = CourtNumbers.Order(courts).ToList();
        var ids = courts.Select(x => x.Id).ToArray();
        var active = QuoteReservations.Active(db, now);
        // Historical booking/maintenance allocations are retained; expired quote holds are omitted.
        var allocations = await db.CourtAllocations.AsNoTracking().Where(x => ids.Contains(x.CourtId) &&
            x.StartsAt < dayEndsAt && x.EndsAt > dayStartsAt &&
            (x.Kind != "QUOTE_HOLD" || active.Any(a => a.Id == x.Id)))
            .OrderBy(x => x.Id).Take(MaximumEvents + 1).ToListAsync(ct);
        if (allocations.Count > MaximumEvents) return Error(http, 409, "CALENDAR_TOO_LARGE");
        var allocationIds = allocations.Select(x => x.Id).ToArray();
        var blockingIds = await active.AsNoTracking().Where(x => allocationIds.Contains(x.Id)).Select(x => x.Id).ToArrayAsync(ct);
        var blocking = blockingIds.ToHashSet();
        var bookings = await (from b in db.Bookings.AsNoTracking()
            join p in db.BookingPayments.AsNoTracking() on b.PaymentScopeId equals p.PaymentScopeId
            join s in db.BookingSeries.AsNoTracking() on b.SeriesId equals (Guid?)s.Id into series
            from s in series.DefaultIfEmpty()
            where allocationIds.Contains(b.AllocationId)
            select new { b.AllocationId, b.Id, b.BookingNo, b.BookingType, b.Status, b.Amount,
                PaymentAnchorId = p.BookingId, PaymentStatus = p.Status, PaymentTotalAmount = p.ExpectedAmount,
                SeriesNo = s == null ? null : s.SeriesNo }).ToListAsync(ct);
        var bookingsByAllocation = bookings.ToDictionary(x => x.AllocationId);
        var maintenance = await db.CourtMaintenance.AsNoTracking().Where(x => allocationIds.Contains(x.AllocationId))
            .Select(x => new { x.AllocationId, x.Id, x.Status, x.Reason }).ToListAsync(ct);
        var maintenanceByAllocation = maintenance.ToDictionary(x => x.AllocationId);
        var day = (int)date.DayOfWeek;
        var hours = await db.CourtOperatingHours.AsNoTracking().Where(x => ids.Contains(x.CourtId) && x.DayOfWeek == day).ToListAsync(ct);
        var rules = await db.PricingRules.AsNoTracking().Where(x => ids.Contains(x.CourtId) && x.DayOfWeek == day &&
            x.StartsOn <= date && x.EndsOn >= date).ToListAsync(ct);
        var hasPaymentSetup = await (from p in db.VenuePaymentAccounts.AsNoTracking()
            join upload in db.MediaUploads.AsNoTracking() on p.QrUploadId equals upload.Id
            where p.VenueId == venueId && upload.VenueId == venueId && upload.Purpose == "QR" && upload.Status == "READY"
            select p.Id).AnyAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date);
        var horizon = Math.Clamp(configuration.GetValue("Booking:MaxAdvanceDays", 60), 1, 365);
        var inHorizon = date >= today && date.DayNumber - today.DayNumber <= horizon;
        var result = new List<object>();
        foreach (var court in courts)
        {
            var courtHours = hours.Where(x => x.CourtId == court.Id).ToList();
            var courtRules = rules.Where(x => x.CourtId == court.Id).ToList();
            var courtAllocations = allocations.Where(x => x.CourtId == court.Id).OrderBy(x => x.StartsAt).ThenBy(x => x.Id).ToList();
            var events = courtAllocations.Select(a =>
            {
                bookingsByAllocation.TryGetValue(a.Id, out var booking);
                maintenanceByAllocation.TryGetValue(a.Id, out var repair);
                var blocksBooking = blocking.Contains(a.Id);
                var dataWarning = blocksBooking && (booking?.Status is "EXPIRED" or "PAYMENT_REJECTED" || repair?.Status == "CANCELLED");
                return new { a.Id, a.Kind, a.StartsAt, a.EndsAt, isPast = a.EndsAt <= now, blocksBooking, dataWarning,
                    occurrenceId = booking?.Id, paymentAnchorId = booking?.PaymentAnchorId,
                    bookingNo = booking?.BookingNo, seriesNo = booking?.SeriesNo, bookingType = booking?.BookingType,
                    bookingStatus = booking?.Status, paymentStatus = booking?.PaymentStatus,
                    occurrenceAmount = booking?.Amount.ToString(CultureInfo.InvariantCulture),
                    paymentTotalAmount = booking?.PaymentTotalAmount.ToString(CultureInfo.InvariantCulture),
                    maintenanceId = repair?.Id, maintenanceStatus = repair?.Status, reason = repair?.Reason };
            }).ToList();
            var acceptsNew = venue.Status == "PUBLISHED" && court.Status == "ACTIVE" && hasPaymentSetup;
            var openRanges = new List<object>();
            foreach (var hour in courtHours)
            {
                if (!TryUtc(zone, date.ToDateTime(hour.OpensAt, DateTimeKind.Unspecified), out var opens) ||
                    !TryUtc(zone, date.ToDateTime(hour.ClosesAt, DateTimeKind.Unspecified), out var closes))
                    return Error(http, 409, "SCHEDULE_UNAVAILABLE");
                var isPriced = true;
                for (var cursor = hour.OpensAt; cursor < hour.ClosesAt; cursor = cursor.AddMinutes(30))
                {
                    var end = cursor.AddMinutes(30);
                    if (end <= cursor || end > hour.ClosesAt || CourtPricing.Select(courtRules, cursor, end) is null)
                    { isPriced = false; break; }
                }
                openRanges.Add(new { startsAt = opens, endsAt = closes,
                    localStart = hour.OpensAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                    localEnd = hour.ClosesAt.ToString("HH:mm", CultureInfo.InvariantCulture), isPriced });
            }
            var slots = new List<object>();
            // Step in UTC across the actual local day: DST days need not contain 48 cells.
            for (var cursor = dayStartsAt; cursor < dayEndsAt; cursor = cursor.AddMinutes(30))
            {
                var end = cursor.AddMinutes(30);
                if (end > dayEndsAt) end = dayEndsAt;
                var localStart = TimeZoneInfo.ConvertTime(cursor, zone);
                var localEnd = TimeZoneInfo.ConvertTime(end, zone);
                var startTime = TimeOnly.FromDateTime(localStart.DateTime);
                var endTime = TimeOnly.FromDateTime(localEnd.DateTime);
                var unambiguous = !zone.IsInvalidTime(localStart.DateTime) && !zone.IsAmbiguousTime(localStart.DateTime) &&
                    !zone.IsInvalidTime(localEnd.DateTime) && !zone.IsAmbiguousTime(localEnd.DateTime);
                var open = DateOnly.FromDateTime(localStart.DateTime) == date && endTime > startTime &&
                    courtHours.Any(x => x.OpensAt <= startTime && x.ClosesAt >= endTime);
                var price = open ? CourtPricing.Select(courtRules, startTime, endTime) : null;
                var allocation = courtAllocations.FirstOrDefault(x => blocking.Contains(x.Id) && x.StartsAt < end && x.EndsAt > cursor);
                var occupancy = allocation?.Kind ?? "FREE";
                var isPast = cursor <= now;
                slots.Add(new { startsAt = startTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    endsAt = end == dayEndsAt ? "24:00" : endTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    startsAtUtc = cursor, endsAtUtc = end, isPast, occupancy, isOpen = open,
                    canRequestNewBooking = acceptsNew && inHorizon && !isPast && unambiguous && allocation is null && price is not null,
                    pricePerSlotExact = price?.PricePerSlot.ToString(CultureInfo.InvariantCulture) });
            }
            result.Add(new { id = court.Id, court.Name, court.Status,
                canRequestNewBooking = acceptsNew && inHorizon,
                court.BookingBlockMinutes, court.MinimumBookingMinutes, court.HoldMinutes,
                openRanges, events, slots });
        }
        await tx.CommitAsync(ct);
        return Results.Ok(new { data = new { venueId, venueName = venue.Name, venueStatus = venue.Status, date,
            venue.Timezone, generatedAt = now, dayStartsAt, dayEndsAt, stepMinutes = 30, courts = result }, traceId = http.TraceIdentifier });
    }

    private static bool TryUtc(TimeZoneInfo zone, DateTime local, out DateTimeOffset value)
    {
        value = default;
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return false;
        value = new(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
        return true;
    }

    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
}
