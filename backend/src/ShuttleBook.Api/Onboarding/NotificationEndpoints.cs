using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;

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
        var rows = await db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt)
            .Take(50).Select(n => new { n.Id, n.Title, n.Body, n.CreatedAt, n.ReadAt }).ToListAsync(ct);
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = rows, traceId = http.TraceIdentifier });
    }

    private static async Task<IResult> Read(HttpContext http, ShuttleBookDbContext db,
        Guid notificationId, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var userId))
            return CustomerRegistrationEndpoints.Problem(http, 401, "UNAUTHORIZED");
        var item = await db.Notifications.SingleOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (item is null) return CustomerRegistrationEndpoints.Problem(http, 404, "NOT_FOUND");
        if (item.ReadAt is null) { item.ReadAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
        return Results.Ok(new { data = new { item.Id, item.ReadAt }, traceId = http.TraceIdentifier });
    }
}
