using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Api.Recruitment;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Bookings;

public static class PlayScheduleEndpoints
{
    private const int PageSize = 20;
    private sealed record Cursor(DateTimeOffset Start, string Kind, Guid Id, string View);
    private sealed record Row(string Kind, Guid Id, Guid? BookingId, Guid? PostId,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateOnly Date, TimeOnly LocalStart,
        TimeOnly LocalEnd, string Timezone, string VenueName, string CourtName, string Status,
        string? OrganizerName, string? BookingType, DateTimeOffset? PaymentDeadline);
    private sealed record RecruitmentAction(Guid? PostId, string? Status, int? SlotsNeeded,
        int? AcceptedCount, int? PendingCount, bool CanCreate);

    public static void MapPlayScheduleEndpoints(this WebApplication app) =>
        app.MapGet("/api/v1/me/play-schedule", List).RequireAuthorization();

    private static async Task<IResult> List(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId)) return Error(http, 401, "UNAUTHORIZED");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (user.AccountType != AccountType.Customer) return Error(http, 403, "FORBIDDEN");

        var query = http.Request.Query;
        if (query.Count > 2 || query.Keys.Any(x => x is not ("view" or "cursor")) ||
            query.Any(x => x.Value.Count != 1)) return Error(http, 400, "VALIDATION_FAILED");
        var view = query.TryGetValue("view", out var rawView) ? rawView[0] : "upcoming";
        if (view is not ("upcoming" or "past")) return Error(http, 400, "VALIDATION_FAILED");
        Cursor? after = null;
        if (query.TryGetValue("cursor", out var rawCursor) && (!TryDecode(rawCursor[0], out after) || after!.View != view))
            return Error(http, 400, "VALIDATION_FAILED");
        var upcoming = view == "upcoming";
        var now = clock.GetUtcNow();

        var own = db.Bookings.AsNoTracking().Where(b => b.CustomerId == userId &&
            (upcoming ? b.EndsAt > now && (b.Status == "CONFIRMED" || b.Status == "AWAITING_TRANSFER" ||
                b.Status == "AWAITING_OWNER_CONFIRMATION" || b.Status == "NEEDS_REVIEW")
                : b.EndsAt <= now && b.Status == "CONFIRMED"));
        var joined = from request in db.RecruitmentRequests.AsNoTracking()
                     join post in db.RecruitmentPosts.AsNoTracking() on request.PostId equals post.Id
                     join booking in db.Bookings.AsNoTracking() on post.BookingId equals booking.Id
                     where request.ApplicantUserId == userId && request.Status == "ACCEPTED" &&
                           booking.Status == "CONFIRMED" && (upcoming ? booking.EndsAt > now : booking.EndsAt <= now)
                     select new { Request = request, Post = post, Booking = booking };

        if (after is not null)
        {
            var cursor = after;
            if (upcoming)
            {
                own = own.Where(b => b.StartsAt > cursor.Start ||
                    b.StartsAt == cursor.Start && cursor.Kind == "B" && b.Id.CompareTo(cursor.Id) > 0);
                joined = joined.Where(x => x.Booking.StartsAt > cursor.Start ||
                    x.Booking.StartsAt == cursor.Start && (cursor.Kind == "B" || x.Request.Id.CompareTo(cursor.Id) > 0));
            }
            else
            {
                own = own.Where(b => b.StartsAt < cursor.Start ||
                    b.StartsAt == cursor.Start && cursor.Kind == "B" && b.Id.CompareTo(cursor.Id) > 0);
                joined = joined.Where(x => x.Booking.StartsAt < cursor.Start ||
                    x.Booking.StartsAt == cursor.Start && (cursor.Kind == "B" || x.Request.Id.CompareTo(cursor.Id) > 0));
            }
        }

        var bookings = await (upcoming ? own.OrderBy(b => b.StartsAt).ThenBy(b => b.Id)
            : own.OrderByDescending(b => b.StartsAt).ThenBy(b => b.Id))
            .Take(PageSize + 1).Select(b => new Row("BOOKING", b.Id, b.Id, null, b.StartsAt, b.EndsAt,
                b.LocalDate, b.LocalStart, b.LocalEnd, b.Timezone, b.VenueName, b.CourtName, b.Status,
                null, b.BookingType, b.PaymentDeadline))
            .ToListAsync(ct);
        var teams = await (upcoming ? joined.OrderBy(x => x.Booking.StartsAt).ThenBy(x => x.Request.Id)
            : joined.OrderByDescending(x => x.Booking.StartsAt).ThenBy(x => x.Request.Id))
            .Take(PageSize + 1).Select(x => new Row("TEAM", x.Request.Id, null, x.Post.Id,
                x.Booking.StartsAt, x.Booking.EndsAt, x.Booking.LocalDate, x.Booking.LocalStart,
                x.Booking.LocalEnd, x.Booking.Timezone, x.Booking.VenueName, x.Booking.CourtName,
                "ACCEPTED", x.Post.ContactName, null, null)).ToListAsync(ct);
        var ordered = upcoming
            ? bookings.Concat(teams).OrderBy(x => x.StartsAt).ThenBy(x => x.Kind == "BOOKING" ? 0 : 1).ThenBy(x => x.Id).ToList()
            : bookings.Concat(teams).OrderByDescending(x => x.StartsAt).ThenBy(x => x.Kind == "BOOKING" ? 0 : 1).ThenBy(x => x.Id).ToList();
        var pageRows = ordered.Take(PageSize).ToList();
        var bookingIds = pageRows.Where(x => x.BookingId.HasValue).Select(x => x.BookingId!.Value).ToArray();
        var posts = await db.RecruitmentPosts.AsNoTracking().Where(p => bookingIds.Contains(p.BookingId))
            .Select(p => new { p.BookingId, p.Id, p.Status, p.SlotsNeeded,
                AcceptedCount = db.RecruitmentRequests.Count(r => r.PostId == p.Id && r.Status == "ACCEPTED"),
                PendingCount = db.RecruitmentRequests.Count(r => r.PostId == p.Id && r.Status == "PENDING") })
            .ToDictionaryAsync(p => p.BookingId, ct);
        var eligibleIds = await (from b in db.Bookings.AsNoTracking()
            join venue in db.Venues.AsNoTracking() on b.VenueId equals venue.Id
            join business in db.Businesses.AsNoTracking() on venue.BusinessId equals business.Id
            join court in db.Courts.AsNoTracking() on b.CourtId equals court.Id
            where bookingIds.Contains(b.Id) && b.CustomerId == userId && b.BookingType == "CASUAL" &&
                b.Status == "CONFIRMED" && b.StartsAt > now && venue.Status == "PUBLISHED" &&
                (venue.CityCode == "HN" || venue.CityCode == "HCM") && business.Status == "ACTIVE" &&
                court.VenueId == venue.Id && court.Status == "ACTIVE"
            select b.Id).ToListAsync(ct);
        var openPostCount = eligibleIds.Count == 0 ? 0 : await db.RecruitmentPosts.AsNoTracking()
            .CountAsync(p => p.OwnerUserId == userId && p.Status == "OPEN" &&
                db.Bookings.Any(b => b.Id == p.BookingId && b.StartsAt > now), ct);
        var canPost = openPostCount < RecruitmentEndpoints.Quota(http, "MaxOpenPosts", 3, 20);
        var eligible = eligibleIds.ToHashSet();
        RecruitmentAction? ActionFor(Row row)
        {
            if (row.BookingId is not Guid bookingId) return null;
            if (posts.TryGetValue(bookingId, out var post))
                return new RecruitmentAction(post.Id, post.Status, post.SlotsNeeded,
                    post.AcceptedCount, post.PendingCount, false);
            return new RecruitmentAction(null, null, null, null, null, canPost && eligible.Contains(bookingId));
        }
        var page = pageRows.Select(x => new
        {
            kind = x.Kind, id = x.Id, x.BookingId, x.PostId, x.StartsAt, x.EndsAt,
            date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            localStart = x.LocalStart.ToString("HH:mm", CultureInfo.InvariantCulture),
            localEnd = x.LocalEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
            x.Timezone, x.VenueName, x.CourtName, x.Status, x.OrganizerName,
            x.BookingType, x.PaymentDeadline,
            transferDeadlinePassed = x.Kind == "BOOKING" && x.Status == "AWAITING_TRANSFER" &&
                x.PaymentDeadline <= now,
            recruitment = ActionFor(x)
        }).ToList();
        var last = ordered.Skip(PageSize - 1).FirstOrDefault();
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = new { items = page,
            nextCursor = ordered.Count > PageSize && last is not null ? Encode(new Cursor(last.StartsAt, last.Kind == "BOOKING" ? "B" : "T", last.Id, view)) : null },
            traceId = http.TraceIdentifier });
    }

    private static string Encode(Cursor cursor) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
        $"{cursor.Start.UtcTicks}|{cursor.Kind}|{cursor.Id:N}|{cursor.View}"));

    private static bool TryDecode(string? value, out Cursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
        try
        {
            var fields = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)).Split('|');
            if (fields.Length != 4 || !long.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
                fields[1] is not ("B" or "T") || !Guid.TryParseExact(fields[2], "N", out var id) || id == Guid.Empty ||
                fields[3] is not ("upcoming" or "past"))
                return false;
            cursor = new Cursor(new DateTimeOffset(ticks, TimeSpan.Zero), fields[1], id, fields[3]);
            return true;
        }
        catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException) { return false; }
    }

    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
}
