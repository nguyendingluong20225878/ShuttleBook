using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Bookings;

public static class SeriesEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly string[] Weekdays = ["SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY"];
    private sealed record QuoteInput(Guid CourtId, string DayOfWeek, string LocalStartTime, int DurationMinutes, string StartsOn, string EndsOn);
    private sealed record CreateInput(Guid QuoteId);
    private sealed record Conflict(string Date, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Code);
    private sealed record Occurrence(WeeklyOccurrence Time, BookingEndpoints.Computed Price);
    private sealed record Computed(List<Occurrence> Occurrences, List<Conflict> Conflicts, long Amount, string Fingerprint);
    private sealed record ComputeFailure(string Code, IReadOnlyList<string>? Dates = null);

    public static void MapSeriesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/booking-series/quote", Quote).RequireAuthorization().RequireRateLimiting("booking-quote");
        app.MapPost("/api/v1/booking-series", Create).RequireAuthorization().RequireRateLimiting("booking-create");
        app.MapGet("/api/v1/booking-series/{id:guid}", Detail).RequireAuthorization();
    }

    private static async Task<IResult> Quote(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, IConfiguration config, CancellationToken ct)
    {
        var (user, authError) = await Customer(http, db, ct); if (authError is not null) return authError;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, bodyError) = await Read<QuoteInput>(http, ["courtId", "dayOfWeek", "localStartTime", "durationMinutes", "startsOn", "endsOn"], ct);
        if (bodyError is not null) return bodyError;
        if (!Parse(input!, out var from, out var to, out var weekday, out var start)) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await QuoteReservations.LockCourt(db, input!.CourtId, ct);
        var current = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={user!.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
        if (current.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (current.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");
        await QuoteReservations.CleanupCourt(db, input.CourtId, clock.GetUtcNow(), ct);
        var priorDeadline = await QuoteReservations.ReplaceOwn(db, user.Id, input.CourtId, clock.GetUtcNow(), ct);
        var (value, failure) = await Compute(db, input.CourtId, from, to, weekday, start, input.DurationMinutes, clock.GetUtcNow(), config, ct);
        if (failure is not null) return ComputeError(http, failure);
        var first = value!.Occurrences[0].Price; var now = clock.GetUtcNow();
        BookingSeriesQuote? quote = null;
        var rolledBack = false;
        if (value.Conflicts.Count == 0)
        {
            quote = new() { CourtId = first.Court.Id, VenueId = first.Venue.Id, StartsOn = from, EndsOn = to, DayOfWeek = weekday,
                LocalStart = start, DurationMinutes = input.DurationMinutes, Amount = value.Amount, Fingerprint = value.Fingerprint,
                Occurrences = JsonSerializer.Serialize(value.Occurrences.Select(Preview), Json), CreatedAt = now, ExpiresAt = priorDeadline ?? now.AddSeconds(120) };
            if (quote.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
            db.BookingSeriesQuotes.Add(quote);
            QuoteReservations.Reserve(db, quote.Id, user.Id, quote.CourtId, "SERIES", now, quote.ExpiresAt,
                value.Occurrences.Select(x => (x.Time.StartsAt, x.Time.EndsAt)));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23P01" })
            {
                await tx.RollbackAsync(ct);
                await using var conflictScope = http.RequestServices.CreateAsyncScope();
                var freshDb = conflictScope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>();
                value = value with { Conflicts = await Conflicts(freshDb, input.CourtId, value.Occurrences.Select(x => x.Time).ToArray(), ct, clock.GetUtcNow()) };
                quote = null; rolledBack = true;
            }
        }
        if (quote is not null) await tx.CommitAsync(ct); else if (!rolledBack) await tx.RollbackAsync(ct);
        return Ok(http, new { quoteId = quote?.Id, expiresAt = quote?.ExpiresAt, canCreate = quote is not null, input.CourtId,
            venueId = first.Venue.Id, courtName = first.Court.Name, venueName = first.Venue.Name, timezone = first.Venue.Timezone,
            input.DayOfWeek, input.StartsOn, input.EndsOn, input.LocalStartTime, input.DurationMinutes,
            occurrenceCount = value.Occurrences.Count, amount = value.Amount, amountExact = Exact(value.Amount), currency = "VND",
            first.Court.HoldMinutes, first.Court.MinimumBookingMinutes, first.Court.BookingBlockMinutes,
            occurrences = value.Occurrences.Select(Preview), conflicts = value.Conflicts });
    }

    private static async Task<IResult> Create(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, IConfiguration config, CancellationToken ct)
    {
        var (customer, authError) = await Customer(http, db, ct); if (authError is not null) return authError;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var keys = http.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrEmpty(keys[0]) || keys[0]!.Length > 128 ||
            keys[0]!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))) return Error(http, 400, "VALIDATION_FAILED");
        var (input, validationError) = await Read<CreateInput>(http, ["quoteId"], ct);
        if (validationError is not null) return validationError;
        if (input!.QuoteId == Guid.Empty) return Error(http, 400, "VALIDATION_FAILED");
        var hash = Hash(JsonSerializer.Serialize(input, Json));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var lockKey = $"SERIES_CREATE:{customer!.Id}:{keys[0]}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))", ct);
        var previous = await db.BookingIdempotency.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == customer.Id && x.Operation == "SERIES_CREATE" && x.Key == keys[0], ct);
        if (previous is not null)
        {
            var scope = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == previous.BookingId, ct);
            var group = await BookingGroup.Lock(db, scope, ct) ?? throw new InvalidOperationException("Missing series group.");
            var current = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={customer.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
            if (current.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
            if (current.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");
            if (group.Anchor.CustomerId != current.Id) return Error(http, 404, "NOT_FOUND");
            if (previous.RequestHash != hash) return Error(http, 409, "IDEMPOTENCY_KEY_REUSED");
            return Results.Json(new { data = await BookingReadModel.Data(db, group.Anchor.Id, false, ct, clock.GetUtcNow()), traceId = http.TraceIdentifier }, statusCode: 201);
        }
        var q = await db.BookingSeriesQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.QuoteId, ct);
        if (q is null) return Error(http, 400, "VALIDATION_FAILED");
        var reservationOwner = await db.QuoteReservations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == q.Id, ct);
        if (reservationOwner is not null && reservationOwner.CustomerId != customer.Id) return Error(http, 404, "NOT_FOUND");
        if (q.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT b.id FROM businesses b JOIN venues v ON v.business_id=b.id WHERE v.id={q.VenueId} FOR UPDATE OF b", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM venues WHERE id={q.VenueId} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM courts WHERE id={q.CourtId} FOR UPDATE", ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={customer.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
        if (user.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (user.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");
        if (q.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        await QuoteReservations.CleanupCourt(db, q.CourtId, clock.GetUtcNow(), ct);
        var reservation = await db.QuoteReservations.SingleOrDefaultAsync(r => r.Id == q.Id, ct);
        if (reservation is null || reservation.ReleasedAt is not null || reservation.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        if (reservation.CustomerId != customer.Id || reservation.Kind != "SERIES") return Error(http, 404, "NOT_FOUND");
        if (reservation.ConsumedAt is not null) return Error(http, 409, "QUOTE_CONSUMED");
        var held = await QuoteReservations.Allocations(db, q.Id, ct);
        var (value, failure) = await Compute(db, q.CourtId, q.StartsOn, q.EndsOn, q.DayOfWeek, q.LocalStart, q.DurationMinutes, clock.GetUtcNow(), config, ct, q.Id);
        if (failure is not null) return ComputeError(http, failure);
        if (value!.Conflicts.Count > 0) return ConflictError(http, value.Conflicts);
        if (value.Fingerprint != q.Fingerprint || value.Amount != q.Amount) return Error(http, 409, "QUOTE_CHANGED");
        if (held.Count != value.Occurrences.Count || value.Occurrences.Any(o => !held.Any(a => a.StartsAt == o.Time.StartsAt && a.EndsAt == o.Time.EndsAt))) return Error(http, 409, "QUOTE_EXPIRED");
        var first = value.Occurrences[0].Price; var now = clock.GetUtcNow();
        if (q.ExpiresAt <= now) return Error(http, 409, "QUOTE_EXPIRED");
        reservation.ConsumedAt = now;
        var series = new BookingSeries { CustomerId = customer.Id, VenueId = q.VenueId, CourtId = q.CourtId, StartsOn = q.StartsOn, EndsOn = q.EndsOn,
            DayOfWeek = q.DayOfWeek, LocalStart = q.LocalStart, DurationMinutes = q.DurationMinutes, OccurrenceCount = value.Occurrences.Count,
            Timezone = first.Venue.Timezone, Amount = value.Amount, CreatedAt = now };
        series.SeriesNo = $"SR{now:yyMMdd}{series.Id:N}"; db.BookingSeries.Add(series);
        var members = new List<Booking>();
        foreach (var occurrence in value.Occurrences)
        {
            var time = occurrence.Time; var price = occurrence.Price;
            var allocation = held.Single(a => a.StartsAt == time.StartsAt && a.EndsAt == time.EndsAt); allocation.Kind = "BOOKING";
            var booking = new Booking { SeriesId = series.Id, PaymentScopeId = series.Id, BookingType = "RECURRING_OCCURRENCE", CustomerId = customer.Id,
                CourtId = q.CourtId, VenueId = q.VenueId, AllocationId = allocation.Id, VenueName = price.Venue.Name, CourtName = price.Court.Name,
                Timezone = price.Venue.Timezone, LocalDate = time.Date, LocalStart = time.LocalStart, LocalEnd = time.LocalEnd, StartsAt = time.StartsAt, EndsAt = time.EndsAt,
                Amount = price.Amount, Slots = JsonSerializer.Serialize(price.Slots, Json), BookingBlockMinutes = price.Court.BookingBlockMinutes,
                MinimumBookingMinutes = price.Court.MinimumBookingMinutes, HoldMinutes = price.Court.HoldMinutes,
                PaymentDeadline = now.AddMinutes(price.Court.HoldMinutes), CreatedAt = now };
            booking.BookingNo = $"BK{now:yyMMdd}{booking.Id:N}"; members.Add(booking); db.Bookings.Add(booking);
        }
        var anchor = members[0];
        db.BookingPayments.Add(new BookingPayment { BookingId = anchor.Id, PaymentScopeId = series.Id, ExpectedAmount = series.Amount,
            QrUploadId = first.Recipient.QrUploadId, RecipientSnapshot = JsonSerializer.Serialize(first.Recipient, Json), CreatedAt = now });
        db.BookingIdempotency.Add(new BookingIdempotency { ActorUserId = customer.Id, Operation = "SERIES_CREATE", Key = keys[0]!, RequestHash = hash, BookingId = anchor.Id, CreatedAt = now });
        db.AuditEvents.Add(new AuditEvent { ActorUserId = customer.Id, Action = "booking_series.created", EntityType = "booking_series", EntityId = series.Id, CorrelationId = http.TraceIdentifier, CreatedAt = now });
        db.OutboxMessages.Add(new OutboxMessage { EventType = "BOOKING_CREATED", EntityId = anchor.Id, TargetUserId = customer.Id, CreatedAt = now, NextAttemptAt = now });
        try
        {
            await db.SaveChangesAsync(ct); var result = await BookingReadModel.Data(db, anchor.Id, false, ct, clock.GetUtcNow());
            await tx.CommitAsync(ct); return Results.Json(new { data = result, traceId = http.TraceIdentifier }, statusCode: 201);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23P01" })
        {
            await tx.RollbackAsync(ct);
            // A new scope provides a clean connection/context after the exclusion abort.
            await using var conflictScope = http.RequestServices.CreateAsyncScope();
            var freshDb = conflictScope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>();
            return ConflictError(http, await Conflicts(freshDb, q.CourtId, value.Occurrences.Select(x => x.Time).ToArray(), ct, clock.GetUtcNow(), q.Id));
        }
    }

    private static async Task<(Computed?, ComputeFailure?)> Compute(ShuttleBookDbContext db, Guid courtId, DateOnly from, DateOnly to, int weekday,
        TimeOnly start, int duration, DateTimeOffset now, IConfiguration config, CancellationToken ct, Guid? ownReservationId = null)
    {
        var court = await db.Courts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courtId && x.Status == "ACTIVE", ct);
        var venue = court is null ? null : await db.Venues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == court.VenueId && x.Status == "PUBLISHED" && db.Businesses.Any(b => b.Id == x.BusinessId && b.Status == "ACTIVE"), ct);
        if (court is null || venue is null) return (null, new("NOT_FOUND"));
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return (null, new("SCHEDULE_UNAVAILABLE")); }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date);
        var horizon = Math.Clamp(config.GetValue("Booking:MaxAdvanceDays", 60), 1, 60);
        if (from < today || to > today.AddDays(horizon)) return (null, new("VALIDATION_FAILED"));
        if (!WeeklyRecurrence.TryGenerate(from, to, (DayOfWeek)weekday, start, duration, court.MinimumBookingMinutes, 12, zone, out var times, out var error))
            return (null, new(error!, error == "SCHEDULE_UNAVAILABLE" ? InvalidScheduleDates(from, to, weekday, start, duration, zone) : null));
        var occurrences = new List<Occurrence>(); long amount = 0;
        foreach (var time in times)
        {
            var (price, failure) = await BookingEndpoints.Compute(db, courtId, time.Date, time.LocalStart, time.LocalEnd, now, config, ct, false);
            if (failure is not null) return (null, new(failure, [time.Date.ToString("yyyy-MM-dd")]));
            if (price!.Amount > 999_999_999_999_999_999 - amount) return (null, new("PRICE_UNAVAILABLE", [time.Date.ToString("yyyy-MM-dd")]));
            amount += price.Amount; occurrences.Add(new(time, price));
        }
        var conflicts = await Conflicts(db, courtId, times, ct, now, ownReservationId);
        var fingerprint = Hash(JsonSerializer.Serialize(new { courtId, from, to, weekday, start, duration,
            occurrences = occurrences.Select(x => new { x.Time, x.Price.Slots, x.Price.Amount, x.Price.Fingerprint }) }, Json));
        return (new(occurrences, conflicts, amount, fingerprint), null);
    }

    private static IReadOnlyList<string> InvalidScheduleDates(DateOnly from, DateOnly to, int weekday, TimeOnly start, int duration, TimeZoneInfo zone)
    {
        var dates = new List<string>();
        var first = from.DayNumber + (weekday - (int)from.DayOfWeek + 7) % 7;
        for (var day = first; day <= to.DayNumber; day += 7)
        {
            var date = DateOnly.FromDayNumber(day);
            var local = date.ToDateTime(start, DateTimeKind.Unspecified);
            var end = local.AddMinutes(duration);
            for (var cursor = local; cursor <= end; cursor = cursor.AddMinutes(30))
                if (zone.IsInvalidTime(cursor) || zone.IsAmbiguousTime(cursor))
                { dates.Add(date.ToString("yyyy-MM-dd")); break; }
        }
        return dates;
    }

    private static async Task<List<Conflict>> Conflicts(ShuttleBookDbContext db, Guid courtId, IReadOnlyList<WeeklyOccurrence> times, CancellationToken ct, DateTimeOffset now, Guid? ownReservationId = null)
    {
        var first = times[0].StartsAt; var last = times[^1].EndsAt;
        var allocations = await QuoteReservations.Active(db, now).AsNoTracking().Where(x => x.CourtId == courtId && (ownReservationId == null || x.QuoteReservationId != ownReservationId) && x.StartsAt < last && x.EndsAt > first).ToListAsync(ct);
        return times.Where(t => allocations.Any(a => a.StartsAt < t.EndsAt && a.EndsAt > t.StartsAt))
            .Select(t => new Conflict(t.Date.ToString("yyyy-MM-dd"), t.StartsAt, t.EndsAt, "SLOT_UNAVAILABLE")).ToList();
    }

    private static object Preview(Occurrence x) => new { date = x.Time.Date.ToString("yyyy-MM-dd"), localStart = x.Time.LocalStart.ToString("HH:mm"),
        localEnd = x.Time.LocalEnd.ToString("HH:mm"), x.Time.StartsAt, x.Time.EndsAt, amount = x.Price.Amount, amountExact = Exact(x.Price.Amount),
        slots = x.Price.Slots.Select(s => new { s.StartsAt, s.EndsAt, s.PricePerSlot, pricePerSlotExact = Exact(s.PricePerSlot) }) };
    private static bool Parse(QuoteInput input, out DateOnly from, out DateOnly to, out int weekday, out TimeOnly start)
    {
        weekday = Array.IndexOf(Weekdays, input.DayOfWeek); from = default; to = default; start = default;
        return input.CourtId != Guid.Empty && weekday >= 0 && DateOnly.TryParseExact(input.StartsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from) &&
            DateOnly.TryParseExact(input.EndsOn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to) &&
            TimeOnly.TryParseExact(input.LocalStartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out start);
    }
    private static async Task<(User?, IResult?)> Customer(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var id)) return (null, Error(http, 401, "UNAUTHORIZED"));
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || user.Status != UserStatus.Active) return (null, Error(http, 401, "UNAUTHORIZED"));
        return user.AccountType == AccountType.Customer ? (user, null) : (null, Error(http, 403, "FORBIDDEN"));
    }
    private static async Task<(T?, IResult?)> Read<T>(HttpContext http, string[] fields, CancellationToken ct)
    {
        try
        {
            using var buffer = new MemoryStream(); var bytes = new byte[1024]; int count;
            while ((count = await http.Request.Body.ReadAsync(bytes, ct)) > 0) { if (buffer.Length + count > 4096) return (default, Error(http, 400, "VALIDATION_FAILED")); buffer.Write(bytes, 0, count); }
            buffer.Position = 0; using var doc = await JsonDocument.ParseAsync(buffer, new JsonDocumentOptions { MaxDepth = 4 }, ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return (default, Error(http, 400, "VALIDATION_FAILED"));
            var names = new HashSet<string>();
            foreach (var field in doc.RootElement.EnumerateObject())
            { if (!fields.Contains(field.Name)) return (default, Error(http, 400, "UNSUPPORTED_FIELD")); if (!names.Add(field.Name)) return (default, Error(http, 400, "VALIDATION_FAILED")); }
            if (names.Count != fields.Length) return (default, Error(http, 400, "VALIDATION_FAILED"));
            return (doc.RootElement.Deserialize<T>(Json), null);
        }
        catch (JsonException) { return (default, Error(http, 400, "VALIDATION_FAILED")); }
    }
    private static async Task<IResult> Detail(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId)) return Error(http, 401, "UNAUTHORIZED");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (user.AccountType is not (AccountType.Customer or AccountType.VenueOperator)) return Error(http, 403, "FORBIDDEN");
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var series = await db.BookingSeries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (series is null || (user.AccountType == AccountType.Customer ? series.CustomerId != userId :
            !await PaymentEndpoints.HasVenueScope(db, userId, series.VenueId, ct))) return Error(http, 404, "NOT_FOUND");
        var anchor = await db.BookingPayments.AsNoTracking().Where(x => x.PaymentScopeId == id).Select(x => x.BookingId).SingleAsync(ct);
        return Ok(http, await BookingReadModel.Data(db, anchor, user.AccountType == AccountType.VenueOperator, ct, clock.GetUtcNow()));
    }

    private static IResult ComputeError(HttpContext http, ComputeFailure failure)
    {
        if (failure.Dates is null) return Error(http, Status(failure.Code), failure.Code);
        http.Response.Headers.CacheControl = "no-store";
        return Results.Json(new { type = "about:blank", title = "Booking series unavailable", status = Status(failure.Code),
            code = failure.Code, traceId = http.TraceIdentifier, dates = failure.Dates }, statusCode: Status(failure.Code), contentType: "application/problem+json");
    }

    private static string Exact(long amount) => amount.ToString(CultureInfo.InvariantCulture);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static int Status(string code) => code == "NOT_FOUND" ? 404 : code == "VALIDATION_FAILED" ? 400 : 409;
    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
    private static IResult ConflictError(HttpContext http, List<Conflict> conflicts) => Results.Json(new { type = "about:blank", title = "Conflict", status = 409,
        code = "SERIES_CONFLICT", traceId = http.TraceIdentifier, conflicts, conflictDates = conflicts.Select(x => x.Date) }, statusCode: 409, contentType: "application/problem+json");
    private static IResult Ok(HttpContext http, object data) { http.Response.Headers.CacheControl = "no-store"; return Results.Ok(new { data, traceId = http.TraceIdentifier }); }
}
