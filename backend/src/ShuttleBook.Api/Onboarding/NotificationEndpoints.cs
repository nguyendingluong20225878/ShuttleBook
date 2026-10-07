using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Onboarding;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/me/notifications").RequireAuthorization();
        group.MapGet("/", List);
        group.MapPost("/{notificationId:guid}/read", Read);
    }

    private static async Task<IResult> List(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId))
            return CustomerRegistrationEndpoints.Problem(http, 401, "UNAUTHORIZED");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.Status is not (UserStatus.Active or UserStatus.PendingOnboarding))
            return CustomerRegistrationEndpoints.Problem(http, 401, "UNAUTHORIZED");
        var query = http.Request.Query; var limit = 50; Guid? before = null;
        if (query.Any(x => x.Key is not ("limit" or "before") || x.Value.Count != 1) ||
            (query.ContainsKey("limit") && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 100)) ||
            (query.ContainsKey("before") && (!Guid.TryParse(query["before"], out var parsed) || (before = parsed) == Guid.Empty)))
            return CustomerRegistrationEndpoints.Problem(http, 400, "VALIDATION_FAILED");
        var scoped = Scoped(db, user);
        var unreadCount = await scoped.CountAsync(n => n.ReadAt == null, ct);
        if (before is Guid cursor) scoped = scoped.Where(x => x.Id.CompareTo(cursor) < 0);
        var rows = await (from n in scoped join message in db.OutboxMessages on n.OutboxMessageId equals message.Id
                          orderby n.Id descending select new { n.Id, n.Title, n.Body, n.CreatedAt, n.ReadAt,
                              message.EventType, message.EntityId }).Take(limit + 1).ToListAsync(ct);
        var data = rows.Take(limit).Select(n => new
        {
            n.Id, n.Title, n.Body, n.CreatedAt, n.ReadAt,
            bookingId = n.EventType.StartsWith("BOOKING_") || n.EventType.StartsWith("PAYMENT_") ? (Guid?)n.EntityId : null,
            action = n.EventType == "PAYMENT_CONFIRMATION_OVERDUE" && user.AccountType == AccountType.Admin ? "ADMIN_PAYMENT_ALERT" :
                OwnerEvents.Contains(n.EventType) ? "OPERATOR_BOOKING" :
                n.EventType.StartsWith("BOOKING_") || n.EventType.StartsWith("PAYMENT_") ? "CUSTOMER_BOOKING" : null
        });
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data, unreadCount, nextCursor = rows.Count > limit ? rows[limit - 1].Id.ToString() : null,
            traceId = http.TraceIdentifier });
    }

    private static async Task<IResult> Read(HttpContext http, ShuttleBookDbContext db,
        Guid notificationId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId))
            return CustomerRegistrationEndpoints.Problem(http, 401, "UNAUTHORIZED");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.Status is not (UserStatus.Active or UserStatus.PendingOnboarding))
            return CustomerRegistrationEndpoints.Problem(http, 401, "UNAUTHORIZED");
        var query = Scoped(db, user).Where(x => x.Id == notificationId);
        var item = await query.AsNoTracking().SingleOrDefaultAsync(ct);
        if (item is null) return CustomerRegistrationEndpoints.Problem(http, 404, "NOT_FOUND");
        if (item.ReadAt is null)
        {
            await query.Where(x => x.ReadAt == null).ExecuteUpdateAsync(x => x.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
            item = await query.AsNoTracking().SingleOrDefaultAsync(ct);
            if (item is null) return CustomerRegistrationEndpoints.Problem(http, 404, "NOT_FOUND");
        }
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = new { item.Id, item.ReadAt }, traceId = http.TraceIdentifier });
    }

    private static readonly string[] OwnerEvents = ["PAYMENT_TRANSFER_REPORTED", "PAYMENT_EVIDENCE_SUPPLEMENTED", "PAYMENT_CONFIRMATION_OVERDUE"];
    private static IQueryable<Notification> Scoped(ShuttleBookDbContext db, User user) =>
        from n in db.Notifications join message in db.OutboxMessages on n.OutboxMessageId equals message.Id
        where n.UserId == user.Id && (!OwnerEvents.Contains(message.EventType) ||
            (message.EventType == "PAYMENT_CONFIRMATION_OVERDUE" && user.AccountType == AccountType.Admin) ||
            (user.AccountType == AccountType.VenueOperator && user.Status == UserStatus.Active &&
            db.Users.Any(u => u.Id == user.Id && u.Status == UserStatus.Active && u.AccountType == AccountType.VenueOperator) &&
            db.Bookings.Any(booking => booking.Id == message.EntityId &&
                db.Venues.Any(v => v.Id == booking.VenueId &&
                    db.Businesses.Any(b => b.Id == v.BusinessId && b.Status == "ACTIVE") &&
                    db.BusinessMemberships.Any(m => m.BusinessId == v.BusinessId && m.UserId == user.Id &&
                        m.Role == "OWNER" && m.Status == "ACTIVE")))))
        select n;
}
