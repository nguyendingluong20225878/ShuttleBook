using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;
using ShuttleBook.Infrastructure.Recruitment;

namespace ShuttleBook.Api.Recruitment;

public static class RecruitmentEndpoints
{
    private sealed record CreatePostInput(Guid BookingId, int SlotsNeeded, string Level, string? Caption = null,
        string? ContactName = null, string? ContactPhone = null, bool SharePhoneWithAccepted = false, Guid? ImageUploadId = null,
        bool PublishContactPhone = false, string? ContributionMode = null, int? CommonContributionVnd = null,
        int? MaleContributionVnd = null, int? FemaleContributionVnd = null);
    private sealed record RequestInput(string? Level = null, string? Message = null, string? ContactPhone = null,
        bool SharePhoneWithAccepted = false);
    private sealed record CapacityInput(int SlotsNeeded);
    private sealed record ReportInput(string Reason);
    private sealed record LocatedSearchInput(string City, string? Date, string? Level,
        double Latitude, double Longitude, int RadiusMeters, int? Limit, string? Cursor,
        string? Q = null, string? Sort = null);
    private sealed class SearchRow { public Guid Id { get; set; } public DateTimeOffset SortAt { get; set; } public double? DistanceMeters { get; set; } }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapRecruitmentEndpoints(this WebApplication app)
    {
        var posts = app.MapGroup("/api/v1/recruitment-posts");
        posts.MapGet("", Search).RequireRateLimiting("recruitment-read");
        posts.MapPost("/search", SearchLocated).RequireRateLimiting("recruitment-read");
        posts.MapGet("/{id:guid}", Detail).RequireRateLimiting("recruitment-read");
        posts.MapPost("/{id:guid}/requests", Request).RequireAuthorization().RequireRateLimiting("recruitment-write");
        posts.MapPost("/{id:guid}/reports", Report).RequireAuthorization().RequireRateLimiting("recruitment-write");
        var mine = app.MapGroup("/api/v1/me").RequireAuthorization();
        mine.MapGet("/recruitment-posts", MyPosts);
        mine.MapPost("/recruitment-posts", Create).RequireRateLimiting("recruitment-write");
        mine.MapPost("/recruitment-posts/{id:guid}/capacity", UpdateCapacity).RequireRateLimiting("recruitment-write");
        mine.MapPost("/recruitment-posts/{id:guid}/close", Close).RequireRateLimiting("recruitment-write");
        mine.MapGet("/recruitment-posts/{id:guid}/requests", PostRequests);
        mine.MapPost("/recruitment-posts/{id:guid}/requests/{requestId:guid}/accept", Accept).RequireRateLimiting("recruitment-write");
        mine.MapPost("/recruitment-posts/{id:guid}/requests/{requestId:guid}/decline", Decline).RequireRateLimiting("recruitment-write");
        mine.MapGet("/recruitment-requests", MyRequests);
        mine.MapPost("/recruitment-requests/{id:guid}/withdraw", Withdraw).RequireRateLimiting("recruitment-write");
        var admin = app.MapGroup("/api/v1/admin/recruitment").RequireAuthorization();
        admin.MapGet("/reports", AdminReports);
        admin.MapGet("/posts", AdminHiddenPosts);
        admin.MapPost("/reports/{id:guid}/dismiss", Dismiss).RequireRateLimiting("recruitment-write");
        admin.MapPost("/reports/{id:guid}/hide", Hide).RequireRateLimiting("recruitment-write");
        admin.MapPost("/posts/{id:guid}/restore", Restore).RequireRateLimiting("recruitment-write");
    }

    internal static IQueryable<RecruitmentPost> PublicPosts(ShuttleBookDbContext db, DateTimeOffset now,
        bool requireOpenSlots = true) =>
        from p in db.RecruitmentPosts
        join b in db.Bookings on p.BookingId equals b.Id
        join v in db.Venues on b.VenueId equals v.Id
        join business in db.Businesses on v.BusinessId equals business.Id
        join court in db.Courts on b.CourtId equals court.Id
        join owner in db.Users on p.OwnerUserId equals owner.Id
        where p.Status == "OPEN" && b.BookingType == "CASUAL" && b.Status == "CONFIRMED" &&
              b.StartsAt > now && v.Status == "PUBLISHED" && (v.CityCode == "HN" || v.CityCode == "HCM") &&
              business.Status == "ACTIVE" && court.Status == "ACTIVE" &&
              owner.AccountType == AccountType.Customer && owner.Status == UserStatus.Active &&
              (!requireOpenSlots || db.RecruitmentRequests.Count(r => r.PostId == p.Id && r.Status == "ACCEPTED") < p.SlotsNeeded)
        select p;

    private static async Task<bool> Eligible(ShuttleBookDbContext db, RecruitmentPost post, DateTimeOffset now, CancellationToken ct) =>
        await (from b in db.Bookings join v in db.Venues on b.VenueId equals v.Id
               join business in db.Businesses on v.BusinessId equals business.Id
               join court in db.Courts on b.CourtId equals court.Id
               join owner in db.Users on post.OwnerUserId equals owner.Id
               where b.Id == post.BookingId && b.CustomerId == post.OwnerUserId &&
                   b.BookingType == "CASUAL" && b.Status == "CONFIRMED" && b.StartsAt > now &&
                   business.Status == "ACTIVE" && v.Status == "PUBLISHED" &&
                   (v.CityCode == "HN" || v.CityCode == "HCM") && court.Status == "ACTIVE" &&
                   owner.AccountType == AccountType.Customer && owner.Status == UserStatus.Active
               select b.Id).AnyAsync(ct);

    private static async Task<object> View(ShuttleBookDbContext db, RecruitmentPost post, double? distance, CancellationToken ct,
        bool revealPhone = false, bool ownerView = false, bool shareable = false)
    {
        var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == post.BookingId, ct);
        var venue = await db.Venues.AsNoTracking().SingleAsync(x => x.Id == booking.VenueId, ct);
        var counts = await db.RecruitmentRequests.AsNoTracking().Where(x => x.PostId == post.Id)
            .GroupBy(_ => 1).Select(group => new {
                Accepted = group.Count(x => x.Status == "ACCEPTED"),
                Pending = group.Count(x => x.Status == "PENDING")
            }).SingleOrDefaultAsync(ct);
        var accepted = counts?.Accepted ?? 0;
        return new { post.Id, venueId = venue.Id, venueName = venue.Name, venue.Address, venue.CityCode,
            courtName = booking.CourtName, date = booking.LocalDate.ToString("yyyy-MM-dd"),
            startsAt = booking.LocalStart.ToString("HH:mm"), endsAt = booking.LocalEnd.ToString("HH:mm"),
            booking.Timezone, post.SlotsNeeded, slotsOpen = Math.Max(0, post.SlotsNeeded - accepted),
            post.Level, post.Status, post.Version, post.CreatedAt, post.Caption, post.ContactName,
            contactPhone = ownerView || post.PublishContactPhone || revealPhone && post.SharePhoneWithAccepted ? post.ContactPhone : null,
            phoneVisibility = post.PublishContactPhone ? "PUBLIC" : post.SharePhoneWithAccepted ? "ACCEPTED" : "HIDDEN",
            post.ContributionMode, post.CommonContributionVnd, post.MaleContributionVnd, post.FemaleContributionVnd,
            acceptedCount = ownerView ? (int?)accepted : null,
            pendingCount = ownerView ? (int?)(counts?.Pending ?? 0) : null,
            shareable = ownerView && shareable,
            imageUrl = post.ImageUploadId is Guid ? $"/api/v1/recruitment-posts/{post.Id}/image" : null,
            distanceMeters = distance };
    }

    private static Task<IResult> Search(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, CancellationToken ct) =>
        SearchCore(http, db, clock, null, ct);

    private static async Task<IResult> SearchLocated(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<LocatedSearchInput>(http,
            ["city", "date", "level", "latitude", "longitude", "radiusMeters", "limit", "cursor", "q", "sort"], ct,
            ["q", "sort"]);
        return error ?? await SearchCore(http, db, clock, input, ct);
    }

    private static async Task<IResult> SearchCore(HttpContext http, ShuttleBookDbContext db, TimeProvider clock,
        LocatedSearchInput? located, CancellationToken ct)
    {
        var q = http.Request.Query;
        if (located is null && q.Any(x => x.Key is not ("city" or "date" or "level" or "limit" or "cursor" or "q" or "sort") || x.Value.Count != 1))
            return Error(http, 400, "VALIDATION_FAILED");
        var city = located?.City ?? q["city"].ToString();
        if (city is not ("HN" or "HCM")) return Error(http, 400, "VALIDATION_FAILED");
        var level = located?.Level ?? q["level"].ToString();
        if (level.Length == 0) level = "ANY";
        if (!ValidLevel(level, true)) return Error(http, 400, "VALIDATION_FAILED");
        var term = (located?.Q ?? q["q"].ToString()).Trim();
        if (term.Length > 80 || term.Length == 1 || term.Any(char.IsControl)) return Error(http, 400, "VALIDATION_FAILED");
        var sort = located?.Sort ?? q["sort"].ToString();
        if (sort.Length == 0) sort = "NEWEST";
        if (sort is not ("NEWEST" or "SOONEST")) return Error(http, 400, "VALIDATION_FAILED");
        var soonest = sort == "SOONEST";
        var dateText = located?.Date ?? q["date"].ToString();
        var dateSpecified = !string.IsNullOrEmpty(dateText);
        if (dateSpecified && !DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return Error(http, 400, "VALIDATION_FAILED");
        var date = dateSpecified ? DateOnly.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture) : DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var hasPosition = located is not null;
        var latitude = located?.Latitude ?? 0; var longitude = located?.Longitude ?? 0; var radius = located?.RadiusMeters ?? 5000;
        if (hasPosition && (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180 || radius is < 1 or > 50000))
            return Error(http, 400, "VALIDATION_FAILED");
        var limit = located?.Limit ?? 20;
        if (located is null && q.ContainsKey("limit") && !int.TryParse(q["limit"], out limit)) return Error(http, 400, "VALIDATION_FAILED");
        if (limit is < 1 or > 50) return Error(http, 400, "VALIDATION_FAILED");
        var cursorText = located?.Cursor ?? q["cursor"].ToString();
        var hasCursor = !string.IsNullOrEmpty(cursorText);
        var afterSortAt = DateTimeOffset.UnixEpoch; var afterId = Guid.Empty;
        if (hasCursor)
        {
            try { var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursorText!));
                var parts = raw.Split('|');
                if (parts.Length != 3 || parts[0] != sort || !DateTimeOffset.TryParseExact(parts[1], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out afterSortAt) || afterSortAt.Offset != TimeSpan.Zero ||
                    !Guid.TryParseExact(parts[2], "D", out afterId))
                    return Error(http, 400, "VALIDATION_FAILED"); }
            catch (FormatException) { return Error(http, 400, "VALIDATION_FAILED"); }
        }
        var now = clock.GetUtcNow();
        var rows = await db.Database.SqlQuery<SearchRow>($"""
            SELECT p.id AS "Id", CASE WHEN {soonest} THEN b.starts_at ELSE p.created_at END AS "SortAt", CASE WHEN {hasPosition} THEN
                ST_Distance(v.location, ST_SetSRID(ST_MakePoint({longitude},{latitude}),4326)::geography)
                ELSE NULL END AS "DistanceMeters"
            FROM recruitment_posts p
            JOIN bookings b ON b.id=p.booking_id
            JOIN venues v ON v.id=b.venue_id
            JOIN businesses biz ON biz.id=v.business_id
            JOIN courts c ON c.id=b.court_id
            JOIN users owner ON owner.id=p.owner_user_id
            WHERE p.status='OPEN' AND b.booking_type='CASUAL' AND b.status='CONFIRMED'
                AND b.starts_at > {now} AND v.status='PUBLISHED' AND v.city_code={city}
                AND biz.status='ACTIVE' AND c.status='ACTIVE'
                AND owner.account_type='Customer' AND owner.status='Active'
                AND (NOT {dateSpecified} OR b.local_date={date})
                AND ({term}='' OR position(lower({term}) in lower(v.name))>0
                    OR position(lower({term}) in lower(v.address))>0
                    OR position(lower({term}) in lower(b.court_name))>0
                    OR position(lower({term}) in lower(p.contact_name))>0
                    OR position(lower({term}) in lower(p.caption))>0)
                AND ({level}='ANY' OR p.level='ANY' OR p.level={level}
                    OR ({level}='NEWBIE' AND p.level='BEGINNER')
                    OR ({level}='TB' AND p.level='INTERMEDIATE')
                    OR ({level}='TBK' AND p.level='ADVANCED'))
                AND (NOT {hasPosition} OR ST_DWithin(v.location,
                    ST_SetSRID(ST_MakePoint({longitude},{latitude}),4326)::geography,{radius}))
                AND (SELECT count(*) FROM recruitment_requests r WHERE r.post_id=p.id AND r.status='ACCEPTED') < p.slots_needed
                AND (NOT {hasCursor} OR ({soonest} AND (b.starts_at>{afterSortAt} OR (b.starts_at={afterSortAt} AND p.id>{afterId})))
                    OR (NOT {soonest} AND (p.created_at<{afterSortAt} OR (p.created_at={afterSortAt} AND p.id<{afterId}))))
            ORDER BY CASE WHEN {soonest} THEN b.starts_at END ASC,
                CASE WHEN NOT {soonest} THEN p.created_at END DESC,
                CASE WHEN {soonest} THEN p.id END ASC,
                CASE WHEN NOT {soonest} THEN p.id END DESC
            LIMIT {limit + 1}
            """).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToArray();
        var posts = await db.RecruitmentPosts.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var byId = posts.ToDictionary(x => x.Id);
        var items = new List<object>();
        foreach (var row in rows.Take(limit)) items.Add(await View(db, byId[row.Id], row.DistanceMeters, ct));
        var last = rows.Take(limit).LastOrDefault();
        return Ok(http, new { items, nextCursor = rows.Count > limit && last is not null
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sort}|{last.SortAt:O}|{last.Id:D}")) : null });
    }

    private static async Task<IResult> Detail(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var post = await PublicPosts(db, clock.GetUtcNow()).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return post is null ? Error(http, 404, "NOT_FOUND") : Ok(http, await View(db, post, null, ct));
    }

    private static async Task<IResult> Create(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<CreatePostInput>(http,
            ["bookingId", "slotsNeeded", "level", "caption", "contactName", "contactPhone", "sharePhoneWithAccepted", "imageUploadId",
             "publishContactPhone", "contributionMode", "commonContributionVnd", "maleContributionVnd", "femaleContributionVnd"], ct,
            ["caption", "contactName", "contactPhone", "sharePhoneWithAccepted", "imageUploadId",
             "publishContactPhone", "contributionMode", "commonContributionVnd", "maleContributionVnd", "femaleContributionVnd"]);
        if (error is not null) return error;
        var contributionMode = input!.ContributionMode ?? "UNSPECIFIED";
        var validContribution = contributionMode switch
        {
            "UNSPECIFIED" => input.CommonContributionVnd is null && input.MaleContributionVnd is null && input.FemaleContributionVnd is null,
            "COMMON" => input.CommonContributionVnd is >= 0 and <= 2_000_000 && input.MaleContributionVnd is null && input.FemaleContributionVnd is null,
            "BY_GENDER" => input.CommonContributionVnd is null && input.MaleContributionVnd is >= 0 and <= 2_000_000 && input.FemaleContributionVnd is >= 0 and <= 2_000_000,
            _ => false
        };
        if (input!.BookingId == Guid.Empty || input.SlotsNeeded is < 1 or > 8 || !ValidLevel(input.Level, true) ||
            input.Caption?.Trim().Length > 1000 || string.IsNullOrWhiteSpace(input.ContactName) ||
            input.ContactName.Trim().Length is < 2 or > 100 ||
            input.ContactPhone?.Trim().Length > 20 ||
            input.ContactPhone is not null && !System.Text.RegularExpressions.Regex.IsMatch(input.ContactPhone.Trim(), @"^(?:\+84|0)[0-9]{9,10}$") ||
            input.SharePhoneWithAccepted && input.PublishContactPhone ||
            (input.SharePhoneWithAccepted || input.PublishContactPhone) && string.IsNullOrWhiteSpace(input.ContactPhone) ||
            !(input.SharePhoneWithAccepted || input.PublishContactPhone) && !string.IsNullOrWhiteSpace(input.ContactPhone) ||
            !validContribution)
            return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id={input.BookingId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (booking is null || booking.CustomerId != user!.Id) return Error(http, 404, "NOT_FOUND");
        var lockedUser = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={user.Id} FOR UPDATE").SingleAsync(ct);
        if (lockedUser.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        var previous = await db.RecruitmentPosts.SingleOrDefaultAsync(x => x.BookingId == booking.Id, ct);
        if (previous is not null) return previous.SlotsNeeded == input.SlotsNeeded && previous.Level == input.Level &&
            previous.Caption == (input.Caption?.Trim() ?? "") && previous.ContactName == (input.ContactName?.Trim() ?? "") &&
            previous.ContactPhone == (input.SharePhoneWithAccepted || input.PublishContactPhone ? input.ContactPhone?.Trim() : null) &&
            previous.SharePhoneWithAccepted == input.SharePhoneWithAccepted &&
            previous.PublishContactPhone == input.PublishContactPhone &&
            previous.ContributionMode == contributionMode &&
            previous.CommonContributionVnd == input.CommonContributionVnd &&
            previous.MaleContributionVnd == input.MaleContributionVnd &&
            previous.FemaleContributionVnd == input.FemaleContributionVnd &&
            previous.ImageUploadId == input.ImageUploadId
            ? Ok(http, await View(db, previous, null, ct)) : Error(http, 409, "REQUEST_EXISTS");
        var now = clock.GetUtcNow();
        if (booking.BookingType != "CASUAL" || booking.Status != "CONFIRMED" || booking.StartsAt <= now ||
            !await (from v in db.Venues join biz in db.Businesses on v.BusinessId equals biz.Id
                    join court in db.Courts on booking.CourtId equals court.Id
                    where v.Id == booking.VenueId && v.Status == "PUBLISHED" &&
                        (v.CityCode == "HN" || v.CityCode == "HCM") && biz.Status == "ACTIVE" &&
                        court.VenueId == v.Id && court.Status == "ACTIVE" select v.Id).AnyAsync(ct))
            return Error(http, 409, "BOOKING_NOT_ELIGIBLE");
        if (await db.RecruitmentPosts.CountAsync(x => x.OwnerUserId == user.Id && x.Status == "OPEN" &&
            db.Bookings.Any(b => b.Id == x.BookingId && b.StartsAt > now), ct) >= Quota(http, "MaxOpenPosts", 3, 20))
            return Error(http, 409, "LIMIT_REACHED");
        if (input.ImageUploadId is Guid imageId && !await db.MediaUploads.AnyAsync(x => x.Id == imageId &&
            x.OwnerUserId == user.Id && x.BookingId == booking.Id && x.VenueId == booking.VenueId &&
            x.Purpose == "RECRUITMENT_IMAGE" && x.Status == "READY", ct)) return Error(http, 400, "VALIDATION_FAILED");
        var post = new RecruitmentPost { BookingId = booking.Id, OwnerUserId = user.Id,
            SlotsNeeded = input.SlotsNeeded, Level = input.Level, Caption = input.Caption?.Trim() ?? "",
            ContactName = input.ContactName.Trim(), ContactPhone = input.SharePhoneWithAccepted || input.PublishContactPhone ? input.ContactPhone?.Trim() : null,
            SharePhoneWithAccepted = input.SharePhoneWithAccepted, PublishContactPhone = input.PublishContactPhone,
            ContributionMode = contributionMode, CommonContributionVnd = input.CommonContributionVnd,
            MaleContributionVnd = input.MaleContributionVnd, FemaleContributionVnd = input.FemaleContributionVnd,
            ImageUploadId = input.ImageUploadId,
            CreatedAt = now, UpdatedAt = now };
        db.RecruitmentPosts.Add(post); Audit(db, http, user.Id, "recruitment.post_created", post.Id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Json(new { data = await View(db, post, null, ct), traceId = http.TraceIdentifier }, statusCode: 201);
    }

    private static async Task<IResult> MyPosts(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        Guid? bookingId = null;
        if (http.Request.Query.Count != 0)
        {
            if (http.Request.Query.Count != 1 || !http.Request.Query.TryGetValue("bookingId", out var raw) ||
                raw.Count != 1 || !Guid.TryParse(raw[0], out var parsed) || parsed == Guid.Empty)
                return Error(http, 400, "VALIDATION_FAILED");
            bookingId = parsed;
        }
        var rows = await db.RecruitmentPosts.AsNoTracking()
            .Where(x => x.OwnerUserId == user!.Id && (bookingId == null || x.BookingId == bookingId))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(bookingId == null ? 100 : 1).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var shareable = (await PublicPosts(db, clock.GetUtcNow()).AsNoTracking()
            .Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var items = new List<object>();
        foreach (var p in rows) items.Add(await View(db, p, null, ct, ownerView: true, shareable: shareable.Contains(p.Id)));
        return Ok(http, new { items });
    }

    private static async Task<IResult> Request(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<RequestInput>(http, ["level", "message", "contactPhone", "sharePhoneWithAccepted"], ct,
            ["level", "message", "contactPhone", "sharePhoneWithAccepted"]); if (error is not null) return error;
        var requestLevel = input!.Level ?? "UNSPECIFIED";
        var message = input.Message?.Trim() ?? "";
        if (!(requestLevel is "UNSPECIFIED" or "BEGINNER" or "INTERMEDIATE" or "ADVANCED") || message.Length > 500 || input.SharePhoneWithAccepted &&
            (string.IsNullOrWhiteSpace(input.ContactPhone) ||
             !System.Text.RegularExpressions.Regex.IsMatch(input.ContactPhone.Trim(), @"^(?:\+84|0)[0-9]{9,10}$")) ||
            !input.SharePhoneWithAccepted && !string.IsNullOrWhiteSpace(input.ContactPhone))
            return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, id, ct);
        if (post is null || post.OwnerUserId == user!.Id) return Error(http, 404, "NOT_FOUND");
        var previous = await db.RecruitmentRequests.SingleOrDefaultAsync(x => x.PostId == id && x.ApplicantUserId == user.Id, ct);
        if (previous is not null) return previous.Level == requestLevel && previous.Message == message &&
            previous.ContactPhone == (input.SharePhoneWithAccepted ? input.ContactPhone!.Trim() : null) &&
            previous.SharePhoneWithAccepted == input.SharePhoneWithAccepted &&
            previous.Status is ("PENDING" or "ACCEPTED")
            ? Ok(http, RequestView(previous)) : Error(http, 409, "REQUEST_EXISTS");
        var now = clock.GetUtcNow();
        if (post.Status != "OPEN" || !await Eligible(db, post, now, ct)) return Error(http, 409, "POST_CLOSED");
        if (await db.RecruitmentRequests.CountAsync(x => x.PostId == id && x.Status == "ACCEPTED", ct) >= post.SlotsNeeded)
            return Error(http, 409, "CAPACITY_REACHED");
        if (await db.RecruitmentRequests.CountAsync(x => x.PostId == id, ct) >= Quota(http, "MaxRequestsPerPost", 20, 100))
            return Error(http, 409, "LIMIT_REACHED");
        var request = new RecruitmentRequest { PostId = id, ApplicantUserId = user.Id, Level = requestLevel, Message = message,
            ContactPhone = input.SharePhoneWithAccepted ? input.ContactPhone!.Trim() : null,
            SharePhoneWithAccepted = input.SharePhoneWithAccepted, CreatedAt = now, UpdatedAt = now };
        db.RecruitmentRequests.Add(request);
        Notice(db, "RECRUITMENT_REQUESTED", post.OwnerUserId, post.Id, now);
        Audit(db, http, user.Id, "recruitment.requested", request.Id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Json(new { data = RequestView(request), traceId = http.TraceIdentifier }, statusCode: 201);
    }

    private static object RequestView(RecruitmentRequest r) => new { r.Id, r.PostId, r.Level, r.Message, r.Status, r.Version, r.CreatedAt, r.UpdatedAt };

    private static async Task<IResult> MyRequests(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        var query = http.Request.Query;
        Guid? postId = null;
        if (query.Count != 0)
        {
            if (query.Count != 1 || !query.TryGetValue("postId", out var rawPostId) || rawPostId.Count != 1 ||
                !Guid.TryParse(rawPostId[0], out var parsedPostId) || parsedPostId == Guid.Empty)
                return Error(http, 400, "VALIDATION_FAILED");
            postId = parsedPostId;
        }
        var rows = await db.RecruitmentRequests.AsNoTracking()
            .Where(x => x.ApplicantUserId == user!.Id && (postId == null || x.PostId == postId))
            .OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var items = new List<object>();
        foreach (var r in rows)
        {
            var post = await db.RecruitmentPosts.AsNoTracking().SingleAsync(x => x.Id == r.PostId, ct);
            items.Add(new { request = RequestView(r), post = await View(db, post, null, ct, r.Status == "ACCEPTED") });
        }
        return Ok(http, new { items });
    }

    private static async Task<IResult> PostRequests(HttpContext http, ShuttleBookDbContext db, Guid id, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var post = await db.RecruitmentPosts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == user!.Id, ct);
        if (post is null) return Error(http, 404, "NOT_FOUND");
        var rows = await (from r in db.RecruitmentRequests.AsNoTracking() join u in db.Users on r.ApplicantUserId equals u.Id
                          where r.PostId == id orderby r.CreatedAt descending
                          select new { r.Id, r.Level, r.Message, r.Status, r.Version, r.CreatedAt,
                              displayName = u.DisplayName ?? "Người chơi",
                              contactPhone = r.Status == "ACCEPTED" && r.SharePhoneWithAccepted ? r.ContactPhone : null }).ToListAsync(ct);
        return Ok(http, new { items = rows });
    }

    private static async Task<IResult> Decide(HttpContext http, ShuttleBookDbContext db, TimeProvider clock,
        Guid id, Guid requestId, bool accept, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        var empty = await Empty(http, ct); if (empty is not null) return empty;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, id, ct);
        if (post is null || post.OwnerUserId != user!.Id) return Error(http, 404, "NOT_FOUND");
        var request = await db.RecruitmentRequests.FromSqlInterpolated($"SELECT * FROM recruitment_requests WHERE id={requestId} AND post_id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (request is null) return Error(http, 404, "NOT_FOUND");
        var target = accept ? "ACCEPTED" : "DECLINED";
        if (request.Status == target) return Ok(http, RequestView(request));
        var now = clock.GetUtcNow();
        if (post.Status != "OPEN" || !await Eligible(db, post, now, ct)) return Error(http, 409, "POST_CLOSED");
        if (request.Status != "PENDING") return Error(http, 409, "STATE_CONFLICT");
        if (accept && await db.RecruitmentRequests.CountAsync(x => x.PostId == id && x.Status == "ACCEPTED", ct) >= post.SlotsNeeded)
            return Error(http, 409, "CAPACITY_REACHED");
        request.Status = target; request.Version++; request.UpdatedAt = now;
        post.Version++; post.UpdatedAt = now;
        Notice(db, accept ? "RECRUITMENT_ACCEPTED" : "RECRUITMENT_DECLINED", request.ApplicantUserId, id, now);
        Audit(db, http, user.Id, accept ? "recruitment.accepted" : "recruitment.declined", request.Id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(http, RequestView(request));
    }
    private static Task<IResult> Accept(HttpContext h, ShuttleBookDbContext d, TimeProvider c, Guid id, Guid requestId, CancellationToken ct) => Decide(h, d, c, id, requestId, true, ct);
    private static Task<IResult> Decline(HttpContext h, ShuttleBookDbContext d, TimeProvider c, Guid id, Guid requestId, CancellationToken ct) => Decide(h, d, c, id, requestId, false, ct);

    private static async Task<IResult> Withdraw(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        var empty = await Empty(http, ct); if (empty is not null) return empty;
        var lookup = await db.RecruitmentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ApplicantUserId == user!.Id, ct);
        if (lookup is null) return Error(http, 404, "NOT_FOUND");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, lookup.PostId, ct);
        var request = await db.RecruitmentRequests.FromSqlInterpolated($"SELECT * FROM recruitment_requests WHERE id={id} FOR UPDATE").SingleAsync(ct);
        if (request.ApplicantUserId != user!.Id) return Error(http, 404, "NOT_FOUND");
        if (request.Status == "WITHDRAWN") return Ok(http, RequestView(request));
        if (request.Status != "PENDING") return Error(http, 409, "STATE_CONFLICT");
        var now = clock.GetUtcNow(); request.Status = "WITHDRAWN"; request.Version++; request.UpdatedAt = now;
        post!.Version++; post.UpdatedAt = now;
        Notice(db, "RECRUITMENT_WITHDRAWN", post.OwnerUserId, post.Id, now);
        Audit(db, http, user.Id, "recruitment.withdrawn", request.Id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(http, RequestView(request));
    }

    private static async Task<IResult> Close(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        var empty = await Empty(http, ct); if (empty is not null) return empty;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, id, ct);
        if (post is null || post.OwnerUserId != user!.Id) return Error(http, 404, "NOT_FOUND");
        if (post.Status == "CLOSED") return Ok(http, await View(db, post, null, ct));
        if (post.Status != "OPEN") return Error(http, 409, "STATE_CONFLICT");
        var now = clock.GetUtcNow(); post.Status = "CLOSED"; post.Version++; post.UpdatedAt = now;
        var accepted = await db.RecruitmentRequests.AsNoTracking().Where(x => x.PostId == id && x.Status == "ACCEPTED").Select(x => x.ApplicantUserId).ToListAsync(ct);
        foreach (var applicant in accepted) Notice(db, "RECRUITMENT_CLOSED", applicant, id, now);
        Audit(db, http, user.Id, "recruitment.closed", id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(http, await View(db, post, null, ct));
    }

    private static async Task<IResult> UpdateCapacity(HttpContext http, ShuttleBookDbContext db, TimeProvider clock,
        Guid id, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<CapacityInput>(http, ["slotsNeeded"], ct);
        if (error is not null) return error;
        if (input!.SlotsNeeded is < 1 or > 8) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, id, ct);
        if (post is null || post.OwnerUserId != user!.Id) return Error(http, 404, "NOT_FOUND");
        if (post.Status != "OPEN" || !await Eligible(db, post, clock.GetUtcNow(), ct))
            return Error(http, 409, "POST_CLOSED");
        var accepted = await db.RecruitmentRequests.CountAsync(x => x.PostId == id && x.Status == "ACCEPTED", ct);
        if (input.SlotsNeeded < accepted) return Error(http, 409, "CAPACITY_BELOW_ACCEPTED");
        if (post.SlotsNeeded == input.SlotsNeeded) return Ok(http, await View(db, post, null, ct, ownerView: true));
        var now = clock.GetUtcNow(); post.SlotsNeeded = input.SlotsNeeded; post.Version++; post.UpdatedAt = now;
        Audit(db, http, user.Id, "recruitment.capacity_updated", id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(http, await View(db, post, null, ct, ownerView: true));
    }

    private static async Task<IResult> Report(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var (user, auth) = await Customer(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (input, error) = await Read<ReportInput>(http, ["reason"], ct); if (error is not null) return error;
        if (input!.Reason is not ("SPAM" or "MISLEADING" or "UNSAFE")) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, id, ct);
        if (post is null || post.OwnerUserId == user!.Id || post.Status != "OPEN" || !await Eligible(db, post, clock.GetUtcNow(), ct))
            return Error(http, 404, "NOT_FOUND");
        var previous = await db.RecruitmentReports.SingleOrDefaultAsync(x => x.PostId == id && x.ReporterUserId == user.Id, ct);
        if (previous is not null) return previous.Reason == input.Reason ? Ok(http, new { previous.Id, previous.Status }) : Error(http, 409, "REQUEST_EXISTS");
        var now = clock.GetUtcNow();
        var report = new RecruitmentReport { PostId = id, ReporterUserId = user.Id, Reason = input.Reason, CreatedAt = now };
        db.RecruitmentReports.Add(report); Audit(db, http, user.Id, "recruitment.reported", report.Id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Results.Json(new { data = new { report.Id, report.Status }, traceId = http.TraceIdentifier }, statusCode: 201);
    }

    private static async Task<IResult> AdminReports(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        var auth = await Admin(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var rows = await db.RecruitmentReports.AsNoTracking().Where(x => x.Status == "OPEN")
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(100).ToListAsync(ct);
        var items = new List<object>();
        foreach (var r in rows)
        {
            var post = await db.RecruitmentPosts.AsNoTracking().SingleAsync(x => x.Id == r.PostId, ct);
            items.Add(new { r.Id, r.PostId, r.Reason, r.CreatedAt, post.Status,
                post = await View(db, post, null, ct) });
        }
        return Ok(http, new { items });
    }

    private static async Task<IResult> AdminHiddenPosts(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        var auth = await Admin(http, db, ct); if (auth is not null) return auth;
        if (http.Request.Query.Count != 1 || http.Request.Query["status"].ToString() != "HIDDEN")
            return Error(http, 400, "VALIDATION_FAILED");
        var rows = await db.RecruitmentPosts.AsNoTracking().Where(x => x.Status == "HIDDEN")
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var items = new List<object>(); foreach (var post in rows) items.Add(await View(db, post, null, ct));
        return Ok(http, new { items });
    }

    private static async Task<IResult> Moderate(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, bool hide, CancellationToken ct)
    {
        var auth = await Admin(http, db, ct); if (auth is not null) return auth;
        var empty = await Empty(http, ct); if (empty is not null) return empty;
        var found = await db.RecruitmentReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (found is null) return Error(http, 404, "NOT_FOUND");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, found.PostId, ct);
        var report = await db.RecruitmentReports.FromSqlInterpolated($"SELECT * FROM recruitment_reports WHERE id={id} FOR UPDATE").SingleAsync(ct);
        if (report.Status != "OPEN") return Ok(http, new { report.Id, report.Status });
        if (hide && post!.Status != "OPEN") return Error(http, 409, "STATE_CONFLICT");
        var now = clock.GetUtcNow(); var adminId = Guid.Parse(http.User.FindFirstValue("sub")!);
        report.Status = hide ? "RESOLVED" : "DISMISSED"; report.ResolvedAt = now; report.ResolvedBy = adminId;
        if (hide && post!.Status == "OPEN")
        {
            post.Status = "HIDDEN"; post.Version++; post.UpdatedAt = now;
            Notice(db, "RECRUITMENT_HIDDEN", post.OwnerUserId, post.Id, now);
            var others = await db.RecruitmentReports.Where(x => x.PostId == post.Id && x.Id != id && x.Status == "OPEN").ToListAsync(ct);
            foreach (var other in others) { other.Status = "RESOLVED"; other.ResolvedAt = now; other.ResolvedBy = adminId; }
        }
        Audit(db, http, adminId, hide ? "recruitment.hidden" : "recruitment.report_dismissed", post!.Id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(http, new { report.Id, report.Status });
    }
    private static Task<IResult> Dismiss(HttpContext h, ShuttleBookDbContext d, TimeProvider c, Guid id, CancellationToken ct) => Moderate(h, d, c, id, false, ct);
    private static Task<IResult> Hide(HttpContext h, ShuttleBookDbContext d, TimeProvider c, Guid id, CancellationToken ct) => Moderate(h, d, c, id, true, ct);

    private static async Task<IResult> Restore(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var auth = await Admin(http, db, ct); if (auth is not null) return auth;
        var empty = await Empty(http, ct); if (empty is not null) return empty;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var post = await LockPost(db, id, ct);
        if (post is null) return Error(http, 404, "NOT_FOUND");
        if (post.Status != "HIDDEN") return Error(http, 409, "STATE_CONFLICT");
        var now = clock.GetUtcNow();
        if (!await Eligible(db, post, now, ct)) return Error(http, 409, "BOOKING_NOT_ELIGIBLE");
        post.Status = "OPEN"; post.Version++; post.UpdatedAt = now;
        Audit(db, http, Guid.Parse(http.User.FindFirstValue("sub")!), "recruitment.restored", id, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(http, await View(db, post, null, ct));
    }

    private static async Task<RecruitmentPost?> LockPost(ShuttleBookDbContext db, Guid id, CancellationToken ct) =>
        await db.RecruitmentPosts.FromSqlInterpolated($"SELECT * FROM recruitment_posts WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static bool ValidLevel(string? level, bool any) => level is
        "BEGINNER" or "INTERMEDIATE" or "ADVANCED" or "NEWBIE" or
        "Y_MINUS" or "Y" or "Y_PLUS" or "TBY_MINUS" or "TBY" or "TBY_PLUS" or
        "TB_MINUS" or "TB" or "TB_PLUS" or "TBK_MINUS" or "TBK" or "TBK_PLUS" || any && level == "ANY";
    internal static int Quota(HttpContext http, string key, int fallback, int ceiling)
    {
        var value = http.RequestServices.GetRequiredService<IConfiguration>().GetValue<int?>("Recruitment:" + key) ?? fallback;
        return Math.Clamp(value, 1, ceiling);
    }
    private static void Notice(ShuttleBookDbContext db, string type, Guid target, Guid post, DateTimeOffset now) =>
        db.OutboxMessages.Add(new OutboxMessage { EventType = type, TargetUserId = target, EntityId = post,
            CreatedAt = now, NextAttemptAt = now });
    private static void Audit(ShuttleBookDbContext db, HttpContext http, Guid actor, string action, Guid id, DateTimeOffset now) =>
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor, Action = action, EntityType = "Recruitment", EntityId = id,
            CorrelationId = http.TraceIdentifier, Metadata = "{}", CreatedAt = now });
    private static async Task<(User?, IResult?)> Customer(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var id)) return (null, Error(http, 401, "UNAUTHORIZED"));
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || user.Status != UserStatus.Active) return (null, Error(http, 401, "UNAUTHORIZED"));
        return user.AccountType == AccountType.Customer ? (user, null) : (null, Error(http, 403, "FORBIDDEN"));
    }
    private static async Task<IResult?> Admin(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var id)) return Error(http, 401, "UNAUTHORIZED");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return user?.AccountType == AccountType.Admin && user.Status == UserStatus.Active ? null : Error(http, 403, "FORBIDDEN");
    }
    private static async Task<IResult?> Empty(HttpContext http, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var (_, error) = await Read<JsonElement>(http, [], ct); return error;
    }
    private static async Task<(T?, IResult?)> Read<T>(HttpContext http, string[] fields, CancellationToken ct, string[]? optional = null)
    {
        try
        {
            using var buffer = new MemoryStream(); var bytes = new byte[1024]; int count;
            while ((count = await http.Request.Body.ReadAsync(bytes, ct)) > 0)
            { if (buffer.Length + count > 4096) return (default, Error(http, 400, "VALIDATION_FAILED")); buffer.Write(bytes, 0, count); }
            buffer.Position = 0;
            using var document = await JsonDocument.ParseAsync(buffer, new JsonDocumentOptions { MaxDepth = 4 }, ct);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (default, Error(http, 400, "VALIDATION_FAILED"));
            var seen = new HashSet<string>();
            foreach (var field in document.RootElement.EnumerateObject())
            { if (!fields.Contains(field.Name)) return (default, Error(http, 400, "UNSUPPORTED_FIELD"));
                if (!seen.Add(field.Name)) return (default, Error(http, 400, "VALIDATION_FAILED")); }
            if (fields.Except(optional ?? []).Any(field => !seen.Contains(field))) return (default, Error(http, 400, "VALIDATION_FAILED"));
            return (document.RootElement.Deserialize<T>(Json), null);
        }
        catch (JsonException) { return (default, Error(http, 400, "VALIDATION_FAILED")); }
    }
    private static IResult Ok(HttpContext http, object data) { http.Response.Headers.CacheControl = "no-store"; return Results.Ok(new { data, traceId = http.TraceIdentifier }); }
    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
}
