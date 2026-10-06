using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Identity;

public static class AdminAuthEndpoints
{
    private static readonly HashSet<string> LoginFields = ["contactType", "contact", "password"];

    public static void MapAdminAuthEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/admin-auth");
        routes.MapPost("/login", LoginAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/restore", RestoreAsync).RequireRateLimiting("auth-verify");
        routes.MapGet("/me", MeAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(HttpContext context, IAuthSessionService service,
        ContactAttemptLimiter limiter, CancellationToken cancellationToken)
    {
        var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, LoginFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var data = body.Value!.Value;
        var contactType = data.GetProperty("contactType").GetString()!;
        var contact = data.GetProperty("contact").GetString()!;
        var password = data.GetProperty("password").GetString()!;
        if (!ContactNormalizer.TryNormalize(contactType, contact, out _, out _) || password.Length is < 1 or > 128)
            return CustomerRegistrationEndpoints.Problem(context, 400, "VALIDATION_FAILED");
        if (!limiter.TryAcquire("admin-login", contactType, contact, 10, out var retryAfter))
            return CustomerRegistrationEndpoints.RateLimited(context, retryAfter);
        var tokens = await service.AdminLoginAsync(contactType, contact,
            password, context.TraceIdentifier, cancellationToken);
        if (tokens is null) return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_CREDENTIALS");
        context.Response.Headers.CacheControl = "no-store";
        AdminBrowserSession.Write(context, tokens);
        return Results.Ok(new { data = tokens, traceId = context.TraceIdentifier });
    }

    private static async Task<IResult> RestoreAsync(HttpContext context, IAuthSessionService service,
        CancellationToken cancellationToken)
    {
        if (!AdminBrowserSession.IsAllowedOrigin(context))
            return CustomerRegistrationEndpoints.Problem(context, 403, "FORBIDDEN");
        var refreshToken = AdminBrowserSession.Read(context);
        if (refreshToken is null)
            return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_REFRESH_TOKEN");
        var tokens = await service.RestoreAdminAsync(refreshToken, context.TraceIdentifier, cancellationToken);
        if (tokens?.User.AccountType != "ADMIN")
        {
            AdminBrowserSession.Clear(context);
            return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_REFRESH_TOKEN");
        }
        context.Response.Headers.CacheControl = "no-store";
        AdminBrowserSession.Write(context, tokens);
        return Results.Ok(new { data = tokens, traceId = context.TraceIdentifier });
    }

    private static async Task<IResult> MeAsync(HttpContext context, ShuttleBookDbContext database,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var userId))
            return CustomerRegistrationEndpoints.Problem(context, 401, "UNAUTHORIZED");
        var user = await database.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.AccountType != AccountType.Admin || user.Status != UserStatus.Active)
            return CustomerRegistrationEndpoints.Problem(context, 403, "FORBIDDEN");
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = new { userId, accountType = "ADMIN", status = "ACTIVE" },
            traceId = context.TraceIdentifier });
    }
}
