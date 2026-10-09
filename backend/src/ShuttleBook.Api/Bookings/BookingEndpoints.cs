using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Bookings;

public static class BookingEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record QuoteInput(Guid CourtId, string Date, string StartsAt, string EndsAt);
    private sealed record CreateInput(Guid CourtId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, Guid QuoteId);
    public sealed record PriceSlot(string StartsAt, string EndsAt, long PricePerSlot);
    public sealed record Recipient(string BankCode, string AccountName, string AccountNumber, Guid QrUploadId,
        string ObjectKey, string ContentType, long SizeBytes, string Sha256Base64);
    internal sealed record Computed(Court Court, Venue Venue, DateTimeOffset Start, DateTimeOffset End,
        List<PriceSlot> Slots, long Amount, Recipient Recipient, string Fingerprint);

    public static void MapBookingEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/availability/quote", Quote).RequireAuthorization().RequireRateLimiting("booking-quote");
        app.MapPost("/api/v1/bookings", Create).RequireAuthorization().RequireRateLimiting("booking-create");
        app.MapGet("/api/v1/me/bookings", List).RequireAuthorization();
        app.MapGet("/api/v1/bookings/{id:guid}", Detail).RequireAuthorization();
        app.MapGet("/api/v1/bookings/{id:guid}/qr", Qr).RequireAuthorization();
    }

    private static async Task<IResult> Quote(HttpContext http, ShuttleBookDbContext db, TimeProvider clock,
        IConfiguration config, CancellationToken ct)
    {
        var (customer, authError) = await Customer(http, db, ct);
        if (authError is not null) return authError;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<QuoteInput>(http, ["courtId", "date", "startsAt", "endsAt"], ct);
        if (error is not null) return error;
        if (input!.CourtId == Guid.Empty || !DateOnly.TryParseExact(input.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !Time(input.StartsAt, out var start) || !Time(input.EndsAt, out var end)) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await QuoteReservations.LockCourt(db, input.CourtId, ct);
        var current = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={customer!.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
        if (current.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (current.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");
        await QuoteReservations.CleanupCourt(db, input.CourtId, clock.GetUtcNow(), ct);
        var priorDeadline = await QuoteReservations.ReplaceOwn(db, customer.Id, input.CourtId, clock.GetUtcNow(), ct);
        var (value, failure) = await Compute(db, input.CourtId, date, start, end, clock.GetUtcNow(), config, ct);
        if (failure is not null) return Error(http, failure == "NOT_FOUND" ? 404 : failure == "VALIDATION_FAILED" ? 400 : 409, failure);
        var now = clock.GetUtcNow();
        var q = new BookingQuote { CourtId = input.CourtId, VenueId = value!.Venue.Id, LocalDate = date,
            LocalStart = start, LocalEnd = end, Timezone = value.Venue.Timezone, StartsAt = value.Start, EndsAt = value.End,
            Amount = value.Amount, Slots = JsonSerializer.Serialize(value.Slots, Json), Fingerprint = value.Fingerprint,
            BookingBlockMinutes = value.Court.BookingBlockMinutes, MinimumBookingMinutes = value.Court.MinimumBookingMinutes,
            HoldMinutes = value.Court.HoldMinutes, CreatedAt = now,
            ExpiresAt = priorDeadline ?? now.AddSeconds(Math.Clamp(config.GetValue("Booking:QuoteSeconds", 120), 30, 600)) };
        if (q.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        db.BookingQuotes.Add(q);
        QuoteReservations.Reserve(db, q.Id, customer.Id, q.CourtId, "CASUAL", now, q.ExpiresAt, [(q.StartsAt, q.EndsAt)]);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23P01" })
        { await tx.RollbackAsync(ct); return Error(http, 409, "SLOT_UNAVAILABLE"); }
        return Ok(http, new { quoteId = q.Id, q.ExpiresAt, q.CourtId, q.VenueId, courtName = value.Court.Name,
            venueName = value.Venue.Name, q.Timezone, date = date.ToString("yyyy-MM-dd"), startsAt = q.StartsAt,
            endsAt = q.EndsAt, slots = value.Slots, q.Amount, currency = "VND", q.BookingBlockMinutes,
            q.MinimumBookingMinutes, q.HoldMinutes });
    }

    private static async Task<IResult> Create(HttpContext http, ShuttleBookDbContext db, TimeProvider clock,
        IConfiguration config, CancellationToken ct)
    {
        var (customer, authError) = await Customer(http, db, ct);
        if (authError is not null) return authError;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var key = http.Request.Headers["Idempotency-Key"];
        if (key.Count != 1 || string.IsNullOrEmpty(key[0]) || key[0]!.Length > 128 ||
            key[0]!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<CreateInput>(http, ["courtId", "startsAt", "endsAt", "quoteId"], ct);
        if (error is not null) return error;
        var hash = Hash(JsonSerializer.Serialize(new { input!.CourtId, startsAt = input.StartsAt.ToUniversalTime(),
            endsAt = input.EndsAt.ToUniversalTime(), input.QuoteId }, Json));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var lockKey = $"CASUAL_CREATE:{customer!.Id}:{key[0]}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))", ct);
        var previous = await db.BookingIdempotency.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == customer.Id &&
            x.Operation == "CASUAL_CREATE" && x.Key == key[0], ct);
        if (previous is not null)
        {
            var replay = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id={previous.BookingId} FOR UPDATE").AsNoTracking().SingleAsync(ct);
            var replayUser = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={customer.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
            if (replayUser.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
            if (replayUser.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");
            if (replay.CustomerId != replayUser.Id) return Error(http, 404, "NOT_FOUND");
            if (previous.RequestHash != hash) return Error(http, 409, "IDEMPOTENCY_KEY_REUSED");
            return Results.Json(new { data = await Data(db, replay, ct, clock.GetUtcNow()), traceId = http.TraceIdentifier }, statusCode: 201);
        }
        var q = await db.BookingQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.QuoteId, ct);
        if (q is null || q.CourtId != input.CourtId || q.StartsAt != input.StartsAt || q.EndsAt != input.EndsAt)
            return Error(http, 400, "VALIDATION_FAILED");
        var reservationOwner = await db.QuoteReservations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == q.Id, ct);
        if (reservationOwner is not null && reservationOwner.CustomerId != customer.Id) return Error(http, 404, "NOT_FOUND");
        if (q.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        // Match F02 revision and F03 operation lock order: business, venue, court.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT b.id FROM businesses b JOIN venues v ON v.business_id=b.id WHERE v.id={q.VenueId} FOR UPDATE OF b", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM venues WHERE id={q.VenueId} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM courts WHERE id={q.CourtId} FOR UPDATE", ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={customer.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
        if (user.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (q.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        if (user.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");
        await QuoteReservations.CleanupCourt(db, q.CourtId, clock.GetUtcNow(), ct);
        var reservation = await db.QuoteReservations.SingleOrDefaultAsync(r => r.Id == q.Id, ct);
        if (reservation is null || reservation.ReleasedAt is not null || reservation.ExpiresAt <= clock.GetUtcNow()) return Error(http, 409, "QUOTE_EXPIRED");
        if (reservation.CustomerId != customer.Id || reservation.Kind != "CASUAL") return Error(http, 404, "NOT_FOUND");
        if (reservation.ConsumedAt is not null) return Error(http, 409, "QUOTE_CONSUMED");
        var held = await QuoteReservations.Allocations(db, q.Id, ct);
        if (held.Count != 1 || held[0].StartsAt != q.StartsAt || held[0].EndsAt != q.EndsAt) return Error(http, 409, "QUOTE_EXPIRED");
        var (value, failure) = await Compute(db, q.CourtId, q.LocalDate, q.LocalStart, q.LocalEnd, clock.GetUtcNow(), config, ct, true, q.Id);
        if (failure is not null) return Error(http, failure == "NOT_FOUND" ? 404 : failure == "VALIDATION_FAILED" ? 400 : 409, failure);
        if (value!.Fingerprint != q.Fingerprint || value.Start != q.StartsAt || value.End != q.EndsAt)
            return Error(http, 409, "QUOTE_CHANGED");
        var now = clock.GetUtcNow();
        if (q.ExpiresAt <= now) return Error(http, 409, "QUOTE_EXPIRED");
        var allocation = held[0]; allocation.Kind = "BOOKING"; reservation.ConsumedAt = now;
        var booking = new Booking { CustomerId = customer.Id, CourtId = q.CourtId, VenueId = q.VenueId,
            AllocationId = allocation.Id, VenueName = value.Venue.Name, CourtName = value.Court.Name,
            Timezone = q.Timezone, LocalDate = q.LocalDate, LocalStart = q.LocalStart, LocalEnd = q.LocalEnd,
            StartsAt = q.StartsAt, EndsAt = q.EndsAt, Amount = value.Amount, Slots = q.Slots,
            BookingBlockMinutes = value.Court.BookingBlockMinutes, MinimumBookingMinutes = value.Court.MinimumBookingMinutes,
            HoldMinutes = value.Court.HoldMinutes, PaymentDeadline = now.AddMinutes(value.Court.HoldMinutes), CreatedAt = now };
        booking.BookingNo = $"BK{now:yyMMdd}{booking.Id:N}";
        booking.PaymentScopeId = booking.Id;
        db.Bookings.Add(booking);
        db.BookingPayments.Add(new BookingPayment { BookingId = booking.Id, PaymentScopeId = booking.Id, ExpectedAmount = booking.Amount,
            QrUploadId = value.Recipient.QrUploadId, RecipientSnapshot = JsonSerializer.Serialize(value.Recipient, Json), CreatedAt = now });
        db.BookingIdempotency.Add(new BookingIdempotency { ActorUserId = customer.Id, Key = key[0]!, RequestHash = hash,
            BookingId = booking.Id, CreatedAt = now });
        db.AuditEvents.Add(new AuditEvent { ActorUserId = customer.Id, Action = "booking.created", EntityType = "booking",
            EntityId = booking.Id, CorrelationId = http.TraceIdentifier, CreatedAt = now });
        db.OutboxMessages.Add(new OutboxMessage { EventType = "BOOKING_CREATED", TargetUserId = customer.Id,
            EntityId = booking.Id, CreatedAt = now, NextAttemptAt = now });
        object responseData;
        try { await db.SaveChangesAsync(ct); responseData = await Data(db, booking, ct, clock.GetUtcNow()); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23P01" })
        { await tx.RollbackAsync(ct); return Error(http, 409, "SLOT_UNAVAILABLE"); }
        return Results.Json(new { data = responseData, traceId = http.TraceIdentifier }, statusCode: 201);
    }

    internal static async Task<(Computed?, string?)> Compute(ShuttleBookDbContext db, Guid courtId, DateOnly date,
        TimeOnly start, TimeOnly end, DateTimeOffset now, IConfiguration config, CancellationToken ct, bool checkAllocation = true, Guid? ownReservationId = null)
    {
        var court = await db.Courts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courtId && x.Status == "ACTIVE", ct);
        if (court is null) return (null, "NOT_FOUND");
        var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == court.VenueId && x.Status == "PUBLISHED" &&
            db.Businesses.Any(b => b.Id == x.BusinessId && b.Status == "ACTIVE"), ct);
        if (venue is null) return (null, "NOT_FOUND");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return (null, "SCHEDULE_UNAVAILABLE"); }
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date);
        var duration = (end - start).TotalMinutes;
        if (date < today || date > today.AddDays(Math.Clamp(config.GetValue("Booking:MaxAdvanceDays", 60), 1, 365)) ||
            end <= start || start.Minute % 30 != 0 || end.Minute % 30 != 0 || start.Second != 0 || end.Second != 0 ||
            duration < court.MinimumBookingMinutes) return (null, "VALIDATION_FAILED");
        var localStart = date.ToDateTime(start, DateTimeKind.Unspecified);
        var localEnd = date.ToDateTime(end, DateTimeKind.Unspecified);
        for (var cursor = localStart; cursor <= localEnd; cursor = cursor.AddMinutes(30))
            if (zone.IsInvalidTime(cursor) || zone.IsAmbiguousTime(cursor)) return (null, "SCHEDULE_UNAVAILABLE");
        var utcStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, zone), TimeSpan.Zero);
        var utcEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, zone), TimeSpan.Zero);
        if (utcStart <= now) return (null, "VALIDATION_FAILED");
        var hours = await db.CourtOperatingHours.AsNoTracking().SingleOrDefaultAsync(x => x.CourtId == courtId && x.DayOfWeek == (int)date.DayOfWeek, ct);
        if (hours is null || start < hours.OpensAt || end > hours.ClosesAt) return (null, "PRICE_UNAVAILABLE");
        if (checkAllocation && await QuoteReservations.Active(db, now).AnyAsync(x => x.CourtId == courtId && (ownReservationId == null || x.QuoteReservationId != ownReservationId) && x.StartsAt < utcEnd && x.EndsAt > utcStart, ct)) return (null, "SLOT_UNAVAILABLE");
        var rules = await db.PricingRules.AsNoTracking().Where(x => x.CourtId == courtId && x.DayOfWeek == (int)date.DayOfWeek && x.StartsOn <= date && x.EndsOn >= date).ToListAsync(ct);
        var slots = new List<PriceSlot>(); long total = 0;
        for (var cursor = start; cursor < end; cursor = cursor.AddMinutes(30))
        {
            var next = cursor.AddMinutes(30); var rule = CourtPricing.Select(rules, cursor, next);
            if (rule is null) return (null, "PRICE_UNAVAILABLE");
            if (rule.PricePerSlot <= 0 || rule.PricePerSlot > 999_999_999_999_999_999 - total) return (null, "PRICE_UNAVAILABLE");
            total += rule.PricePerSlot;
            slots.Add(new(cursor.ToString("HH:mm"), next.ToString("HH:mm"), rule.PricePerSlot));
        }
        var account = await db.VenuePaymentAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.VenueId == venue.Id, ct);
        var qr = account is null ? null : await db.MediaUploads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == account.QrUploadId &&
            x.VenueId == venue.Id && x.Purpose == "QR" && x.Status == "READY", ct);
        if (account is null || qr is null) return (null, "PAYMENT_SETUP_UNAVAILABLE");
        var recipient = new Recipient(account.BankCode, account.AccountName, account.AccountNumber, qr.Id,
            qr.ObjectKey, qr.ContentType, qr.SizeBytes, qr.Sha256Base64);
        var fingerprint = Hash(JsonSerializer.Serialize(new { slots, court.BookingBlockMinutes, court.MinimumBookingMinutes,
            court.HoldMinutes, recipient, venue.Timezone }, Json));
        return (new(court, venue, utcStart, utcEnd, slots, total, recipient, fingerprint), null);
    }

    private static async Task<IResult> Detail(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (user, error) = await Customer(http, db, ct); if (error is not null) return error;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == user!.Id, ct);
        return booking is null ? Error(http, 404, "NOT_FOUND") : Ok(http, await Data(db, booking, ct, clock.GetUtcNow()));
    }
    private static async Task<IResult> List(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        var (user, error) = await Customer(http, db, ct); if (error is not null) return error;
        var query = http.Request.Query; var limit = 20; Guid? before = null;
        if (query.Any(x => x.Key is not ("limit" or "before") || x.Value.Count != 1) ||
            (query.ContainsKey("limit") && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 100)) ||
            (query.ContainsKey("before") && (!Guid.TryParse(query["before"], out var parsed) || (before = parsed) == Guid.Empty)))
            return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var rows = db.Bookings.AsNoTracking().Where(x => x.CustomerId == user!.Id && db.BookingPayments.Any(p => p.BookingId == x.Id));
        if (before is Guid cursor) rows = rows.Where(x => x.Id.CompareTo(cursor) < 0);
        var bookings = await rows.OrderByDescending(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var items = new List<object>();
        foreach (var b in bookings.Take(limit)) items.Add(await BookingReadModel.Summary(db, b, await db.BookingPayments.AsNoTracking().SingleAsync(p => p.BookingId == b.Id, ct), ct));
        return Ok(http, new { items, nextCursor = bookings.Count > limit ? bookings[limit - 1].Id.ToString() : null });
    }
    private static object Summary(Booking b) => new { bookingId = b.Id, b.BookingNo, b.Status, b.VenueId, b.CourtId,
        b.VenueName, b.CourtName, b.Timezone, date = b.LocalDate.ToString("yyyy-MM-dd"), localStart = b.LocalStart.ToString("HH:mm"),
        localEnd = b.LocalEnd.ToString("HH:mm"), b.StartsAt, b.EndsAt, b.Amount, currency = "VND", b.PaymentDeadline, b.Version, b.CreatedAt };
    private static async Task<object> Data(ShuttleBookDbContext db, Booking b, CancellationToken ct, DateTimeOffset now)
    {
        return await BookingReadModel.Data(db, b.Id, false, ct, now);
    }
    private static async Task<IResult> Qr(HttpContext http, ShuttleBookDbContext db, IConfiguration config,
        IWebHostEnvironment environment, TimeProvider clock, Guid id, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (user, error) = await Customer(http, db, ct); if (error is not null) return error;
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == user!.Id, ct);
        if (booking is null) return Error(http, 404, "NOT_FOUND");
        id = await BookingGroup.AnchorId(db, id, ct) ?? throw new InvalidOperationException("Missing payment scope.");
        var payment = await db.BookingPayments.AsNoTracking().SingleAsync(x => x.BookingId == id, ct);
        var r = JsonSerializer.Deserialize<Recipient>(payment.RecipientSnapshot, Json)!;
        var upload = await db.MediaUploads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == payment.QrUploadId && x.VenueId == booking.VenueId && x.Status == "READY" && x.Purpose == "QR", ct);
        if (upload is null || upload.ObjectKey != r.ObjectKey || upload.Sha256Base64 != r.Sha256Base64) return Error(http, 404, "NOT_FOUND");
        http.Response.Headers.CacheControl = "no-store"; http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (string.Equals(config["Media:Mode"], "S3", StringComparison.OrdinalIgnoreCase))
        {
            try { return Results.Redirect(await http.RequestServices.GetRequiredService<IAmazonS3>().GetPreSignedURLAsync(new GetPreSignedUrlRequest
            { BucketName = config["Media:S3Bucket"], Key = r.ObjectKey, Verb = HttpVerb.GET, Expires = clock.GetUtcNow().AddMinutes(5).UtcDateTime })); }
            catch (AmazonClientException) { return Error(http, 503, "MEDIA_UNAVAILABLE"); }
        }
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing")) || (config["Media:Mode"] is not null && config["Media:Mode"] != "Local")) return Error(http, 503, "MEDIA_UNAVAILABLE");
        var path = Path.Combine(environment.ContentRootPath, ".media-local", upload.Id.ToString("N"));
        if (!File.Exists(path)) return Error(http, 404, "NOT_FOUND");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (bytes.LongLength != r.SizeBytes || Convert.ToBase64String(SHA256.HashData(bytes)) != r.Sha256Base64) return Error(http, 409, "UPLOAD_MISMATCH");
        return Results.File(bytes, r.ContentType);
    }
    private static async Task<(User?, IResult?)> Customer(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var id)) return (null, Error(http, 401, "UNAUTHORIZED"));
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || user.Status != UserStatus.Active) return (null, Error(http, 401, "UNAUTHORIZED"));
        return user.AccountType == AccountType.Customer ? (user, null) : (null, Error(http, 403, "FORBIDDEN"));
    }
    private static bool Time(string? value, out TimeOnly time) => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time) && time.Minute % 30 == 0;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static IResult Ok(HttpContext http, object value) { http.Response.Headers.CacheControl = "no-store"; return Results.Ok(new { data = value, traceId = http.TraceIdentifier }); }
    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
    private static async Task<(T?, IResult?)> Read<T>(HttpContext http, string[] fields, CancellationToken ct)
    {
        try
        {
            using var buffer = new MemoryStream(); var bytes = new byte[1024]; int count;
            while ((count = await http.Request.Body.ReadAsync(bytes, ct)) > 0) { if (buffer.Length + count > 4096) return (default, Error(http, 400, "VALIDATION_FAILED")); buffer.Write(bytes, 0, count); }
            buffer.Position = 0; using var document = await JsonDocument.ParseAsync(buffer, new JsonDocumentOptions { MaxDepth = 4 }, ct);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (default, Error(http, 400, "VALIDATION_FAILED"));
            var seen = new HashSet<string>();
            foreach (var field in document.RootElement.EnumerateObject())
            { if (!fields.Contains(field.Name)) return (default, Error(http, 400, "UNSUPPORTED_FIELD")); if (!seen.Add(field.Name)) return (default, Error(http, 400, "VALIDATION_FAILED")); }
            if (seen.Count != fields.Length) return (default, Error(http, 400, "VALIDATION_FAILED"));
            return (document.RootElement.Deserialize<T>(Json), null);
        }
        catch (JsonException) { return (default, Error(http, 400, "VALIDATION_FAILED")); }
    }
}
