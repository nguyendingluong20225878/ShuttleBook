using ShuttleBook.Infrastructure.Bookings;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Operations;

public static class CourtOperationsEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { PropertyNameCaseInsensitive = false };

    private sealed record HourInput(int DayOfWeek, string? OpensAt, string? ClosesAt);
    private sealed record BasePriceInput(int DayOfWeek, string? StartsAt, string? EndsAt, long PricePerSlot);
    private sealed record ScheduleInput(List<HourInput>? Hours, List<BasePriceInput>? Prices);
    private sealed record RuleInput(DateOnly? StartsOn, DateOnly? EndsOn, int DayOfWeek,
        string? StartsAt, string? EndsAt, long PricePerSlot, int Priority);
    private sealed record RulesInput(List<RuleInput>? Rules);
    private sealed record MaintenanceInput(DateOnly? Date, string? StartsAt, string? EndsAt, string? Reason);
    private sealed record BookingPolicyInput(int BookingBlockMinutes, int MinimumBookingMinutes, int HoldMinutes);
    private sealed record HourSlice(int Day, TimeOnly Start, TimeOnly End);
    private sealed record PriceSlice(int Day, TimeOnly Start, TimeOnly End, long Price);
    private sealed record OverrideSlice(DateOnly StartsOn, DateOnly EndsOn, int Day,
        TimeOnly Start, TimeOnly End, long Price, int Priority);

    public static void MapCourtOperationsEndpoints(this WebApplication app)
    {
        var courts = app.MapGroup("/api/v1/operator/courts").RequireAuthorization();
        courts.MapGet("/{courtId:guid}/operations", GetOperations);
        courts.MapPut("/{courtId:guid}/schedule", SetSchedule);
        courts.MapPut("/{courtId:guid}/pricing-rules", SetPricingRules);
        courts.MapPut("/{courtId:guid}/booking-policy", SetBookingPolicy);
        courts.MapGet("/{courtId:guid}/price-preview", PricePreview);
        courts.MapGet("/{courtId:guid}/maintenance", ListMaintenance);
        courts.MapPost("/{courtId:guid}/maintenance", CreateMaintenance);
        courts.MapPost("/{courtId:guid}/maintenance/{maintenanceId:guid}/cancel", CancelMaintenance);
    }

    private static async Task<IResult> GetOperations(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var (court, venue, _, error) = await ScopedCourt(http, db, courtId, false, ct);
        if (error is not null) return error;
        var hours = await db.CourtOperatingHours.Where(x => x.CourtId == courtId)
            .OrderBy(x => x.DayOfWeek).ToListAsync(ct);
        var prices = await db.PricingRules.Where(x => x.CourtId == courtId)
            .OrderBy(x => x.Priority).ThenBy(x => x.DayOfWeek).ThenBy(x => x.StartsOn)
            .ThenBy(x => x.StartsAt).ToListAsync(ct);
        return Ok(http, new { court!.Id, court.Name, court.Status, court.Version, venueId = venue!.Id,
            venue.Timezone, court.BookingBlockMinutes, court.MinimumBookingMinutes, court.HoldMinutes,
            hours = hours.Select(x => new { x.DayOfWeek, x.OpensAt, x.ClosesAt }),
            basePrices = prices.Where(x => x.Priority == 0).Select(x =>
                new { x.DayOfWeek, x.StartsAt, x.EndsAt, x.PricePerSlot }),
            rules = prices.Where(x => x.Priority > 0).Select(x =>
                new { x.Id, x.StartsOn, x.EndsOn, x.DayOfWeek, x.StartsAt, x.EndsAt,
                    x.PricePerSlot, x.Priority }) });
    }

    private static async Task<IResult> SetBookingPolicy(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var (input, readError) = await Read<BookingPolicyInput>(http,
            ["bookingBlockMinutes", "minimumBookingMinutes", "holdMinutes"], null, null, null, ct);
        if (readError is not null) return readError;
        if (input!.BookingBlockMinutes is not (30 or 60 or 90) ||
            input.MinimumBookingMinutes < input.BookingBlockMinutes || input.MinimumBookingMinutes > 480 ||
            input.MinimumBookingMinutes % input.BookingBlockMinutes != 0 ||
            input.HoldMinutes is < 5 or > 60)
            return Problem(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (court, _, user, error) = await ScopedCourt(http, db, courtId, true, ct);
        if (error is not null) return error;
        if (court!.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, court.Version);
        if (versionError is not null) return versionError;
        court.BookingBlockMinutes = input.BookingBlockMinutes;
        court.MinimumBookingMinutes = input.MinimumBookingMinutes;
        court.HoldMinutes = input.HoldMinutes;
        court.Version++;
        Audit(db, user!.Id, "court.booking_policy_updated", courtId, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { court.Id, court.Version, court.BookingBlockMinutes,
            court.MinimumBookingMinutes, court.HoldMinutes });
    }

    private static async Task<IResult> SetSchedule(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var (input, readError) = await Read<ScheduleInput>(http, ["hours", "prices"],
            ["hours", "prices"], ["dayOfWeek", "opensAt", "closesAt"],
            ["dayOfWeek", "startsAt", "endsAt", "pricePerSlot"], ct);
        if (readError is not null) return readError;
        if (!TrySchedule(input!, out var hours, out var prices)) return Problem(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (court, _, user, error) = await ScopedCourt(http, db, courtId, true, ct);
        if (error is not null) return error;
        if (court!.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, court.Version);
        if (versionError is not null) return versionError;
        var overrides = await db.PricingRules.Where(x => x.CourtId == courtId && x.Priority > 0).ToListAsync(ct);
        if (overrides.Any(x => !InsideHours(x.DayOfWeek, x.StartsAt, x.EndsAt, hours)))
            return Problem(http, 409, "STATE_CONFLICT");
        db.CourtOperatingHours.RemoveRange(await db.CourtOperatingHours.Where(x => x.CourtId == courtId).ToListAsync(ct));
        db.PricingRules.RemoveRange(await db.PricingRules.Where(x => x.CourtId == courtId && x.Priority == 0).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.CourtOperatingHours.AddRange(hours.Select(x => new CourtOperatingHour
            { CourtId = courtId, DayOfWeek = x.Day, OpensAt = x.Start, ClosesAt = x.End }));
        db.PricingRules.AddRange(prices.Select(x => new PricingRule
            { CourtId = courtId, DayOfWeek = x.Day, StartsAt = x.Start, EndsAt = x.End,
                PricePerSlot = x.Price }));
        court.Version++;
        Audit(db, user!.Id, "court.operating_schedule_updated", courtId, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { court.Id, court.Version, hours = hours.Count, prices = prices.Count });
    }

    private static async Task<IResult> SetPricingRules(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var (input, readError) = await Read<RulesInput>(http, ["rules"],
            ["rules"], ["startsOn", "endsOn", "dayOfWeek", "startsAt", "endsAt", "pricePerSlot", "priority"],
            null, ct);
        if (readError is not null) return readError;
        if (!TryRules(input!, out var rules)) return Problem(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (court, _, user, error) = await ScopedCourt(http, db, courtId, true, ct);
        if (error is not null) return error;
        if (court!.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, court.Version);
        if (versionError is not null) return versionError;
        var hours = (await db.CourtOperatingHours.Where(x => x.CourtId == courtId).ToListAsync(ct))
            .Select(x => new HourSlice(x.DayOfWeek, x.OpensAt, x.ClosesAt)).ToList();
        if (rules.Any(x => !InsideHours(x.Day, x.Start, x.End, hours)))
            return Problem(http, 400, "VALIDATION_FAILED");
        db.PricingRules.RemoveRange(await db.PricingRules.Where(x => x.CourtId == courtId && x.Priority > 0).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.PricingRules.AddRange(rules.Select(x => new PricingRule { CourtId = courtId,
            StartsOn = x.StartsOn, EndsOn = x.EndsOn, DayOfWeek = x.Day,
            StartsAt = x.Start, EndsAt = x.End, PricePerSlot = x.Price, Priority = x.Priority }));
        court.Version++;
        Audit(db, user!.Id, "court.pricing_rules_updated", courtId, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { court.Id, court.Version, rules = rules.Count });
    }

    private static async Task<IResult> PricePreview(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var query = http.Request.Query;
        if (query.Count != 3 || query.Keys.Any(x => x is not ("date" or "startsAt" or "endsAt")) ||
            query.Any(x => x.Value.Count != 1) ||
            !DateOnly.TryParseExact(query["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ||
            !SlotTime(query["startsAt"], out var start) || !SlotTime(query["endsAt"], out var end) || start >= end)
            return Problem(http, 400, "VALIDATION_FAILED");
        var (court, venue, _, error) = await ScopedCourt(http, db, courtId, false, ct);
        if (error is not null) return error;
        if (court!.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        var minutes = (end - start).TotalMinutes;
        if (minutes < court.MinimumBookingMinutes || minutes % court.BookingBlockMinutes != 0)
            return Problem(http, 400, "VALIDATION_FAILED");
        var day = (int)date.DayOfWeek;
        var opening = await db.CourtOperatingHours.SingleOrDefaultAsync(x => x.CourtId == courtId && x.DayOfWeek == day, ct);
        if (opening is null || start < opening.OpensAt || end > opening.ClosesAt)
            return Problem(http, 409, "PRICE_UNAVAILABLE");
        var rules = await db.PricingRules.Where(x => x.CourtId == courtId && x.DayOfWeek == day &&
            x.StartsOn <= date && x.EndsOn >= date).ToListAsync(ct);
        var slots = new List<object>();
        decimal total = 0;
        for (var cursor = start; cursor < end; cursor = cursor.AddMinutes(30))
        {
            var next = cursor.AddMinutes(30);
            var rule = CourtPricing.Select(rules, cursor, next);
            if (rule is null) return Problem(http, 409, "PRICE_UNAVAILABLE");
            total += rule.PricePerSlot;
            slots.Add(new { startsAt = cursor.ToString("HH:mm", CultureInfo.InvariantCulture),
                endsAt = next.ToString("HH:mm", CultureInfo.InvariantCulture),
                ruleId = rule.Id, rule.Priority, rule.PricePerSlot });
        }
        return Ok(http, new { court.Id, date, venue!.Timezone, slots, totalPrice = total });
    }

    private static async Task<IResult> ListMaintenance(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, Guid courtId, CancellationToken ct)
    {
        var (_, _, _, error) = await ScopedCourt(http, db, courtId, false, ct);
        if (error is not null) return error;
        var now = clock.GetUtcNow();
        var rows = await (from m in db.CourtMaintenance
            join a in db.CourtAllocations on m.AllocationId equals a.Id
            where m.CourtId == courtId && m.Status == "ACTIVE" && a.EndsAt > now
            orderby a.StartsAt
            select new { m.Id, m.Reason, m.Status, a.StartsAt, a.EndsAt })
            .Take(100).ToListAsync(ct);
        return Ok(http, rows);
    }

    private static async Task<IResult> CreateMaintenance(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, Guid courtId, CancellationToken ct)
    {
        var (input, readError) = await Read<MaintenanceInput>(http,
            ["date", "startsAt", "endsAt", "reason"], null, null, null, ct);
        if (readError is not null) return readError;
        if (input!.Date is null || !SlotTime(input.StartsAt, out var start) ||
            !SlotTime(input.EndsAt, out var end) || start >= end ||
            !TextOk(input.Reason, 5, 500)) return Problem(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (court, venue, user, error) = await ScopedCourt(http, db, courtId, true, ct);
        if (error is not null) return error;
        if (court!.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(venue!.Timezone);
        var localStart = input.Date.Value.ToDateTime(start, DateTimeKind.Unspecified);
        var localEnd = input.Date.Value.ToDateTime(end, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd) ||
            zone.IsAmbiguousTime(localStart) || zone.IsAmbiguousTime(localEnd))
            return Problem(http, 400, "VALIDATION_FAILED");
        var startsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, zone), TimeSpan.Zero);
        var endsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, zone), TimeSpan.Zero);
        if (startsAt <= clock.GetUtcNow() || endsAt <= startsAt)
            return Problem(http, 400, "VALIDATION_FAILED");
        await QuoteReservations.CleanupCourt(db, courtId, clock.GetUtcNow(), ct);
        if (await db.CourtAllocations.AnyAsync(x => x.CourtId == courtId && x.Status == "RESERVED" &&
            x.StartsAt < endsAt && x.EndsAt > startsAt, ct))
            return Problem(http, 409, "SLOT_CONFLICT");
        var now = clock.GetUtcNow();
        var allocation = new CourtAllocation { CourtId = courtId, StartsAt = startsAt,
            EndsAt = endsAt, CreatedAt = now };
        var maintenance = new CourtMaintenance { CourtId = courtId, AllocationId = allocation.Id,
            Reason = input.Reason!.Trim(), CreatedBy = user!.Id, CreatedAt = now };
        db.CourtAllocations.Add(allocation);
        db.CourtMaintenance.Add(maintenance);
        Audit(db, user.Id, "court.maintenance_created", maintenance.Id, http);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23P01" })
        { return Problem(http, 409, "SLOT_CONFLICT"); }
        return Results.Json(Envelope(new { maintenance.Id, maintenance.CourtId, maintenance.Status,
            maintenance.Reason, allocation.StartsAt, allocation.EndsAt }, http), statusCode: 201);
    }

    private static async Task<IResult> CancelMaintenance(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, Guid courtId, Guid maintenanceId, CancellationToken ct)
    {
        if (http.Request.ContentLength is > 0) return Problem(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (_, _, user, error) = await ScopedCourt(http, db, courtId, true, ct);
        if (error is not null) return error;
        var maintenance = await db.CourtMaintenance.FromSqlInterpolated(
            $"SELECT * FROM court_maintenance WHERE id = {maintenanceId} AND court_id = {courtId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (maintenance is null) return Problem(http, 404, "NOT_FOUND");
        if (maintenance.Status == "CANCELLED") return Ok(http, new { maintenance.Id, maintenance.Status });
        var allocation = await db.CourtAllocations.SingleAsync(x => x.Id == maintenance.AllocationId, ct);
        if (allocation.EndsAt <= clock.GetUtcNow()) return Problem(http, 409, "STATE_CONFLICT");
        var now = clock.GetUtcNow();
        maintenance.Status = "CANCELLED"; maintenance.CancelledBy = user!.Id; maintenance.CancelledAt = now;
        allocation.Status = "RELEASED"; allocation.ReleasedAt = now;
        Audit(db, user.Id, "court.maintenance_cancelled", maintenance.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { maintenance.Id, maintenance.Status });
    }

    private static async Task<(Court?, Venue?, User?, IResult?)> ScopedCourt(HttpContext http,
        ShuttleBookDbContext db, Guid courtId, bool lockRow, CancellationToken ct)
    {
        var user = await UserAsync(http, db, ct);
        if (user?.AccountType != AccountType.VenueOperator || user.Status != UserStatus.Active)
            return (null, null, null, Problem(http, 403, "FORBIDDEN"));
        var court = lockRow
            ? await db.Courts.FromSqlInterpolated($"SELECT * FROM courts WHERE id = {courtId} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.Courts.SingleOrDefaultAsync(x => x.Id == courtId, ct);
        if (court is null) return (null, null, null, Problem(http, 404, "NOT_FOUND"));
        var venue = await db.Venues.SingleAsync(x => x.Id == court.VenueId, ct);
        if (!await db.BusinessMemberships.AnyAsync(x => x.BusinessId == venue.BusinessId &&
            x.UserId == user.Id && x.Role == "OWNER" && x.Status == "ACTIVE", ct))
            return (null, null, null, Problem(http, 404, "NOT_FOUND"));
        var business = await db.Businesses.SingleAsync(x => x.Id == venue.BusinessId, ct);
        if (business.Status != "ACTIVE" || venue.Status != "PUBLISHED")
            return (null, null, null, Problem(http, 409, "STATE_CONFLICT"));
        return (court, venue, user, null);
    }

    private static async Task<User?> UserAsync(HttpContext http, ShuttleBookDbContext db, CancellationToken ct) =>
        Guid.TryParse(http.User.FindFirstValue("sub"), out var id)
            ? await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) : null;

    private static bool TrySchedule(ScheduleInput input, out List<HourSlice> hours, out List<PriceSlice> prices)
    {
        hours = []; prices = [];
        if (input.Hours is not { Count: > 0 and <= 7 } || input.Prices is not { Count: > 0 and <= 70 }) return false;
        foreach (var x in input.Hours)
        {
            if (x.DayOfWeek is < 0 or > 6 || !SlotTime(x.OpensAt, out var start) ||
                !SlotTime(x.ClosesAt, out var end) || start >= end || hours.Any(h => h.Day == x.DayOfWeek)) return false;
            hours.Add(new HourSlice(x.DayOfWeek, start, end));
        }
        foreach (var x in input.Prices)
        {
            if (x.DayOfWeek is < 0 or > 6 || x.PricePerSlot <= 0 ||
                !SlotTime(x.StartsAt, out var start) || !SlotTime(x.EndsAt, out var end) || start >= end) return false;
            prices.Add(new PriceSlice(x.DayOfWeek, start, end, x.PricePerSlot));
        }
        var openings = hours;
        if (prices.Any(x => !InsideHours(x.Day, x.Start, x.End, openings))) return false;
        foreach (var opening in hours)
        {
            var cursor = opening.Start;
            foreach (var price in prices.Where(x => x.Day == opening.Day).OrderBy(x => x.Start))
            {
                if (price.Start != cursor) return false;
                cursor = price.End;
            }
            if (cursor != opening.End) return false;
        }
        return true;
    }

    private static bool TryRules(RulesInput input, out List<OverrideSlice> rules)
    {
        rules = [];
        if (input.Rules is not { Count: <= 100 }) return false;
        foreach (var x in input.Rules)
        {
            if (x.StartsOn is null || x.EndsOn is null || x.StartsOn > x.EndsOn ||
                x.DayOfWeek is < 0 or > 6 || x.Priority is < 1 or > 1000 || x.PricePerSlot <= 0 ||
                !SlotTime(x.StartsAt, out var start) || !SlotTime(x.EndsAt, out var end) || start >= end) return false;
            var rule = new OverrideSlice(x.StartsOn.Value, x.EndsOn.Value, x.DayOfWeek,
                start, end, x.PricePerSlot, x.Priority);
            if (rules.Any(previous => previous.Day == rule.Day && previous.Priority == rule.Priority &&
                previous.StartsOn <= rule.EndsOn && previous.EndsOn >= rule.StartsOn &&
                previous.Start < rule.End && previous.End > rule.Start)) return false;
            rules.Add(rule);
        }
        return true;
    }

    private static bool InsideHours(int day, TimeOnly start, TimeOnly end, List<HourSlice> hours) =>
        hours.Any(x => x.Day == day && x.Start <= start && x.End >= end);
    private static bool SlotTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", out time) && time.Minute is 0 or 30;
    private static bool TextOk(string? value, int min, int max) => value is not null &&
        value.Trim().Length >= min && value.Trim().Length <= max;
    private static IResult? CheckVersion(HttpContext http, long version)
    {
        var match = http.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(match)) return Problem(http, 428, "PRECONDITION_REQUIRED");
        return match == $"\"{version}\"" ? null : Problem(http, 412, "PRECONDITION_FAILED");
    }
    private static void Audit(ShuttleBookDbContext db, Guid actor, string action, Guid entityId, HttpContext http) =>
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor, Action = action, EntityType = "court",
            EntityId = entityId, CorrelationId = http.TraceIdentifier, CreatedAt = DateTimeOffset.UtcNow });
    private static object Envelope(object data, HttpContext http) => new { data, traceId = http.TraceIdentifier };
    private static IResult Ok(HttpContext http, object data) => Results.Ok(Envelope(data, http));
    private static IResult Problem(HttpContext http, int status, string code) =>
        CustomerRegistrationEndpoints.Problem(http, status, code);

    private static async Task<(T?, IResult?)> Read<T>(HttpContext http, string[] fields,
        string[]? arrayFields, string[]? firstItemFields, string[]? secondItemFields, CancellationToken ct)
    {
        if (http.Request.ContentLength is > 65_536) return (default, Problem(http, 400, "VALIDATION_FAILED"));
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > 65_536) return (default, Problem(http, 400, "VALIDATION_FAILED"));
                buffer.Write(chunk, 0, count);
            }
            buffer.Position = 0;
            using var document = await JsonDocument.ParseAsync(buffer, new JsonDocumentOptions { MaxDepth = 8 }, ct);
            if (!ExactFields(document.RootElement, fields, out var unsupported))
                return (default, Problem(http, 400, unsupported ? "UNSUPPORTED_FIELD" : "VALIDATION_FAILED"));
            if (arrayFields is not null)
            {
                for (var index = 0; index < arrayFields.Length; index++)
                {
                    var items = document.RootElement.GetProperty(arrayFields[index]);
                    if (items.ValueKind != JsonValueKind.Array) return (default, Problem(http, 400, "VALIDATION_FAILED"));
                    var itemFields = index == 0 ? firstItemFields : secondItemFields;
                    if (itemFields is null) continue;
                    foreach (var item in items.EnumerateArray())
                        if (!ExactFields(item, itemFields, out unsupported))
                            return (default, Problem(http, 400, unsupported ? "UNSUPPORTED_FIELD" : "VALIDATION_FAILED"));
                }
            }
            return (document.RootElement.Deserialize<T>(Json), null);
        }
        catch (JsonException) { return (default, Problem(http, 400, "VALIDATION_FAILED")); }
    }
    private static bool ExactFields(JsonElement value, string[] fields, out bool unsupported)
    {
        unsupported = false;
        if (value.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject())
        {
            if (!fields.Contains(field.Name, StringComparer.Ordinal)) { unsupported = true; return false; }
            if (!seen.Add(field.Name)) return false;
        }
        return seen.Count == fields.Length;
    }
}
