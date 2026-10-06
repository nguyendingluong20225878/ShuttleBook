using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Discovery;

public static class PublicVenuesEndpoints
{
    private sealed class NearbyRow
    {
        public Guid Id { get; set; }
        public double DistanceMeters { get; set; }
    }

    private sealed record Page(int Limit, int Offset);
    private sealed record SlotCandidate(Guid CourtId, string StartsAt, string EndsAt,
        DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

    public static void MapPublicVenuesEndpoints(this WebApplication app)
    {
        var venues = app.MapGroup("/api/v1/venues");
        venues.MapGet("/nearby", Nearby);
        venues.MapGet("/{venueId:guid}/availability", Availability);
        venues.MapGet("/{venueId:guid}/image", Image);
        venues.MapGet("/{venueId:guid}", Detail);
        venues.MapGet("", List);
    }

    private static async Task<IResult> List(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!OnlyQuery(http, "q", "limit", "cursor") || !TryPage(http, out var page))
            return Error(http, 400, "VALIDATION_FAILED");
        var q = http.Request.Query["q"].ToString().Trim();
        if (q.Length > 120) return Error(http, 400, "VALIDATION_FAILED");
        var venues = PublicQuery(db);
        if (q.Length > 0)
        {
            var pattern = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            venues = venues.Where(v => EF.Functions.ILike(v.Name, pattern, "\\") ||
                EF.Functions.ILike(v.Address, pattern, "\\"));
        }
        var rows = await venues.OrderBy(v => v.Name).ThenBy(v => v.Id)
            .Skip(page.Offset).Take(page.Limit + 1).ToListAsync(ct);
        var hasMore = rows.Count > page.Limit;
        var items = rows.Take(page.Limit).ToList();
        var images = await ReadyImages(db, items, ct);
        return Ok(http, new { items = items.Select(v => Summary(v, images)),
            nextCursor = hasMore ? Cursor(page.Offset + page.Limit) : null });
    }

    private static async Task<IResult> Nearby(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!OnlyQuery(http, "latitude", "longitude", "radiusMeters", "limit", "cursor") ||
            !TryPage(http, out var page) ||
            !double.TryParse(http.Request.Query["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(http.Request.Query["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            !double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 ||
            longitude is < -180 or > 180 ||
            !TryOptionalInt(http, "radiusMeters", 5000, 1, 50000, out var radius))
            return Error(http, 400, "VALIDATION_FAILED");

        var candidates = await db.Database.SqlQuery<NearbyRow>($"""
            SELECT v.id AS "Id",
                ST_Distance(v.location, ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography) AS "DistanceMeters"
            FROM venues v JOIN businesses b ON b.id = v.business_id
            WHERE b.status = 'ACTIVE' AND v.status = 'PUBLISHED'
                AND EXISTS (SELECT 1 FROM courts c WHERE c.venue_id = v.id AND c.status = 'ACTIVE')
                AND ST_DWithin(v.location, ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography, {radius})
            ORDER BY "DistanceMeters", v.id
            LIMIT {page.Limit + 1} OFFSET {page.Offset}
            """).ToListAsync(ct);
        var hasMore = candidates.Count > page.Limit;
        candidates = candidates.Take(page.Limit).ToList();
        var ids = candidates.Select(x => x.Id).ToArray();
        var venues = await db.Venues.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        var byId = venues.ToDictionary(v => v.Id);
        var images = await ReadyImages(db, venues, ct);
        return Ok(http, new { items = candidates.Select(row => new
            {
                row.Id, byId[row.Id].Name, byId[row.Id].Address,
                byId[row.Id].Latitude, byId[row.Id].Longitude,
                row.DistanceMeters, imageUrl = ImageUrl(byId[row.Id], images)
            }), nextCursor = hasMore ? Cursor(page.Offset + page.Limit) : null });
    }

    private static async Task<IResult> Detail(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, CancellationToken ct)
    {
        if (!OnlyQuery(http)) return Error(http, 400, "VALIDATION_FAILED");
        var venue = await PublicQuery(db).SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return Error(http, 404, "NOT_FOUND");
        var courts = await db.Courts.AsNoTracking().Where(c => c.VenueId == venueId && c.Status == "ACTIVE")
            .OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(ct);
        var images = await ReadyImages(db, [venue], ct);
        return Ok(http, new { venue.Id, venue.Name, venue.Address, venue.Contact,
            venue.Latitude, venue.Longitude, venue.Timezone, imageUrl = ImageUrl(venue, images),
            courts = courts.Select(c => new { c.Id, c.Name, c.BookingBlockMinutes,
                c.MinimumBookingMinutes, c.HoldMinutes }) });
    }

    private static async Task<IResult> Availability(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, Guid venueId, CancellationToken ct)
    {
        if (!OnlyQuery(http, "date", "courtId") ||
            !DateOnly.TryParseExact(http.Request.Query["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) return Error(http, 400, "VALIDATION_FAILED");
        Guid? courtId = null;
        if (http.Request.Query.ContainsKey("courtId"))
        {
            if (!Guid.TryParse(http.Request.Query["courtId"], out var parsed) || parsed == Guid.Empty)
                return Error(http, 400, "VALIDATION_FAILED");
            courtId = parsed;
        }
        var venue = await PublicQuery(db).SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null) return Error(http, 404, "NOT_FOUND");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone); }
        catch (TimeZoneNotFoundException) { return Error(http, 503, "SCHEDULE_UNAVAILABLE"); }
        catch (InvalidTimeZoneException) { return Error(http, 503, "SCHEDULE_UNAVAILABLE"); }
        var now = clock.GetUtcNow();
        if (date < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).Date))
            return Error(http, 400, "VALIDATION_FAILED");
        var courtsQuery = db.Courts.AsNoTracking().Where(c => c.VenueId == venueId && c.Status == "ACTIVE");
        if (courtId is Guid requested) courtsQuery = courtsQuery.Where(c => c.Id == requested);
        var courts = await courtsQuery.OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(ct);
        if (courtId.HasValue && courts.Count == 0) return Error(http, 404, "NOT_FOUND");
        var ids = courts.Select(c => c.Id).ToArray();
        var day = (int)date.DayOfWeek;
        var hours = await db.CourtOperatingHours.AsNoTracking()
            .Where(x => ids.Contains(x.CourtId) && x.DayOfWeek == day).ToListAsync(ct);
        var rules = await db.PricingRules.AsNoTracking().Where(x => ids.Contains(x.CourtId) &&
            x.DayOfWeek == day && x.StartsOn <= date && x.EndsOn >= date).ToListAsync(ct);
        var candidates = new List<SlotCandidate>();
        foreach (var hour in hours)
        {
            for (var cursor = hour.OpensAt; cursor < hour.ClosesAt; cursor = cursor.AddMinutes(30))
            {
                var next = cursor.AddMinutes(30);
                if (next <= cursor || next > hour.ClosesAt) break;
                var start = date.ToDateTime(cursor, DateTimeKind.Unspecified);
                var end = date.ToDateTime(next, DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(start) || zone.IsInvalidTime(end) ||
                    zone.IsAmbiguousTime(start) || zone.IsAmbiguousTime(end))
                    return Error(http, 409, "SCHEDULE_UNAVAILABLE");
                candidates.Add(new SlotCandidate(hour.CourtId,
                    cursor.ToString("HH:mm", CultureInfo.InvariantCulture),
                    next.ToString("HH:mm", CultureInfo.InvariantCulture),
                    new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeSpan.Zero),
                    new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(end, zone), TimeSpan.Zero)));
            }
        }
        var allocations = new List<CourtAllocation>();
        if (candidates.Count > 0)
        {
            var first = candidates.Min(x => x.StartsAtUtc);
            var last = candidates.Max(x => x.EndsAtUtc);
            allocations = await db.CourtAllocations.AsNoTracking().Where(x => ids.Contains(x.CourtId) &&
                x.Status == "RESERVED" && x.StartsAt < last && x.EndsAt > first).ToListAsync(ct);
        }
        var byCourt = candidates.GroupBy(x => x.CourtId).ToDictionary(g => g.Key, g => g.ToList());
        var rulesByCourt = rules.GroupBy(x => x.CourtId).ToDictionary(g => g.Key, g => g.ToList());
        var allocationsByCourt = allocations.GroupBy(x => x.CourtId).ToDictionary(g => g.Key, g => g.ToList());
        var result = courts.Select(c => new
        {
            courtId = c.Id, c.Name, c.BookingBlockMinutes, c.MinimumBookingMinutes, c.HoldMinutes,
            slots = (byCourt.GetValueOrDefault(c.Id) ?? []).Select(slot =>
            {
                var startTime = TimeOnly.ParseExact(slot.StartsAt, "HH:mm", CultureInfo.InvariantCulture);
                var endTime = TimeOnly.ParseExact(slot.EndsAt, "HH:mm", CultureInfo.InvariantCulture);
                var rule = (rulesByCourt.GetValueOrDefault(c.Id) ?? [])
                    .Where(x => x.StartsAt <= startTime && x.EndsAt >= endTime)
                    .OrderByDescending(x => x.Priority).FirstOrDefault();
                var reserved = (allocationsByCourt.GetValueOrDefault(c.Id) ?? [])
                    .Any(x => x.StartsAt < slot.EndsAtUtc && x.EndsAt > slot.StartsAtUtc);
                var status = slot.StartsAtUtc <= now ? "PAST" : reserved ? "RESERVED" :
                    rule is null ? "NO_PRICE" : "AVAILABLE";
                return new { slot.StartsAt, slot.EndsAt, slot.StartsAtUtc, slot.EndsAtUtc,
                    status, pricePerSlot = rule?.PricePerSlot };
            })
        });
        http.Response.Headers.CacheControl = "no-store";
        return Ok(http, new { venueId, date, venue.Timezone, generatedAt = now,
            stepMinutes = 30, courts = result });
    }

    private static async Task<IResult> Image(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, TimeProvider clock, Guid venueId, CancellationToken ct)
    {
        if (!OnlyQuery(http)) return Error(http, 400, "VALIDATION_FAILED");
        var venue = await PublicQuery(db).SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue?.ImageUploadId is not Guid imageId) return Error(http, 404, "NOT_FOUND");
        var upload = await db.MediaUploads.AsNoTracking().SingleOrDefaultAsync(u => u.Id == imageId &&
            u.VenueId == venueId && u.Purpose == "VENUE_IMAGE" && u.Status == "READY", ct);
        if (upload is null) return Error(http, 404, "NOT_FOUND");
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (string.Equals(config["Media:Mode"], "S3", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var client = http.RequestServices.GetRequiredService<IAmazonS3>();
                var url = await client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
                {
                    BucketName = config["Media:S3Bucket"], Key = upload.ObjectKey,
                    Verb = HttpVerb.GET, Expires = clock.GetUtcNow().AddMinutes(5).UtcDateTime
                });
                return Results.Redirect(url);
            }
            catch (AmazonClientException) { return Error(http, 503, "MEDIA_UNAVAILABLE"); }
        }
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing")) ||
            (config["Media:Mode"] is not null &&
                !string.Equals(config["Media:Mode"], "Local", StringComparison.OrdinalIgnoreCase)))
            return Error(http, 503, "MEDIA_UNAVAILABLE");
        var path = Path.Combine(environment.ContentRootPath, ".media-local", upload.Id.ToString("N"));
        if (!File.Exists(path)) return Error(http, 404, "NOT_FOUND");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (bytes.LongLength != upload.SizeBytes ||
            Convert.ToBase64String(SHA256.HashData(bytes)) != upload.Sha256Base64)
            return Error(http, 409, "UPLOAD_MISMATCH");
        return Results.File(bytes, upload.ContentType);
    }

    private static IQueryable<Venue> PublicQuery(ShuttleBookDbContext db) =>
        from venue in db.Venues.AsNoTracking()
        join business in db.Businesses on venue.BusinessId equals business.Id
        where business.Status == "ACTIVE" && venue.Status == "PUBLISHED" &&
            db.Courts.Any(c => c.VenueId == venue.Id && c.Status == "ACTIVE")
        select venue;

    private static async Task<HashSet<(Guid Id, Guid VenueId)>> ReadyImages(ShuttleBookDbContext db,
        IReadOnlyCollection<Venue> venues, CancellationToken ct)
    {
        var ids = venues.Where(v => v.ImageUploadId.HasValue).Select(v => v.ImageUploadId!.Value).ToArray();
        if (ids.Length == 0) return [];
        var ready = await db.MediaUploads.AsNoTracking().Where(u => ids.Contains(u.Id) &&
            u.Purpose == "VENUE_IMAGE" && u.Status == "READY")
            .Select(u => new { u.Id, u.VenueId }).ToListAsync(ct);
        return ready.Select(u => (u.Id, u.VenueId)).ToHashSet();
    }

    private static object Summary(Venue venue, HashSet<(Guid Id, Guid VenueId)> images) => new
    {
        venue.Id, venue.Name, venue.Address, venue.Latitude, venue.Longitude,
        imageUrl = ImageUrl(venue, images)
    };
    private static string? ImageUrl(Venue venue, HashSet<(Guid Id, Guid VenueId)> images) =>
        venue.ImageUploadId is Guid id && images.Contains((id, venue.Id))
            ? $"/api/v1/venues/{venue.Id}/image" : null;

    private static bool OnlyQuery(HttpContext http, params string[] allowed) =>
        http.Request.Query.All(pair => allowed.Contains(pair.Key, StringComparer.Ordinal) && pair.Value.Count == 1);

    private static bool TryPage(HttpContext http, out Page page)
    {
        page = new Page(20, 0);
        if (!TryOptionalInt(http, "limit", 20, 1, 100, out var limit)) return false;
        var offset = 0;
        if (http.Request.Query.TryGetValue("cursor", out var cursor))
        {
            if (cursor.ToString().Length is < 1 or > 32) return false;
            try
            {
                var token = cursor.ToString().Replace('-', '+').Replace('_', '/');
                token = token.PadRight((token.Length + 3) / 4 * 4, '=');
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                if (!int.TryParse(decoded, NumberStyles.None, CultureInfo.InvariantCulture, out offset) ||
                    offset is < 1 or > 10000) return false;
            }
            catch (FormatException) { return false; }
        }
        page = new Page(limit, offset);
        return true;
    }

    private static string Cursor(int offset) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        offset.ToString(CultureInfo.InvariantCulture))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryOptionalInt(HttpContext http, string key, int fallback, int min, int max, out int result)
    {
        result = fallback;
        if (!http.Request.Query.TryGetValue(key, out var raw)) return true;
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= min && result <= max;
    }

    private static IResult Ok(HttpContext http, object data) => Results.Ok(new { data, traceId = http.TraceIdentifier });
    private static IResult Error(HttpContext http, int status, string code) =>
        CustomerRegistrationEndpoints.Problem(http, status, code);
}
