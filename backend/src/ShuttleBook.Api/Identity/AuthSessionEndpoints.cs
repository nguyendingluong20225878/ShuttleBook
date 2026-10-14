using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Identity;

public static class AuthSessionEndpoints
{
    private static readonly HashSet<string> LoginFields = ["contactType", "contact", "password"];
    private static readonly HashSet<string> RefreshFields = ["refreshToken"];
    private static readonly HashSet<string> ProfileFields = ["displayName"];

    public static void MapAuthSessionEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/auth");
        routes.MapPost("/login", LoginAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/refresh", RefreshAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/logout", LogoutAsync).RequireAuthorization();
        routes.MapGet("/me", MeAsync).RequireAuthorization();
        routes.MapGet("/profile", ProfileAsync).RequireAuthorization();
        routes.MapPut("/profile", UpdateProfileAsync).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(HttpContext context, IAuthSessionService service, ContactAttemptLimiter limiter, CancellationToken cancellationToken)
    {
        var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, LoginFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var data = body.Value!.Value;
        if (!limiter.TryAcquire("login", data.GetProperty("contactType").GetString()!,
            data.GetProperty("contact").GetString()!, 10, out var retryAfter))
            return CustomerRegistrationEndpoints.RateLimited(context, retryAfter);
        var tokens = await service.LoginAsync(data.GetProperty("contactType").GetString()!,
            data.GetProperty("contact").GetString()!, data.GetProperty("password").GetString()!,
            context.TraceIdentifier, cancellationToken);
        return tokens is null
            ? CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_CREDENTIALS")
            : TokenResponse(context, tokens);
    }

    private static async Task<IResult> RefreshAsync(HttpContext context, IAuthSessionService service, CancellationToken cancellationToken)
    {
        var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, RefreshFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var tokens = await service.RefreshAsync(body.Value!.Value.GetProperty("refreshToken").GetString()!,
            context.TraceIdentifier, cancellationToken);
        if (tokens is null)
        {
            AdminBrowserSession.Clear(context);
            return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_REFRESH_TOKEN");
        }
        AdminBrowserSession.Write(context, tokens);
        return TokenResponse(context, tokens);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAuthSessionService service, CancellationToken cancellationToken)
    {
        var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, RefreshFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var userId) ||
            !Guid.TryParse(context.User.FindFirstValue("sid"), out var familyId))
            return CustomerRegistrationEndpoints.Problem(context, 401, "UNAUTHORIZED");
        if (!await service.LogoutAsync(userId, familyId,
            body.Value!.Value.GetProperty("refreshToken").GetString()!, context.TraceIdentifier, cancellationToken))
            return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_REFRESH_TOKEN");
        AdminBrowserSession.Clear(context);
        return Results.NoContent();
    }

    private static IResult MeAsync(HttpContext context) =>
        Results.Ok(new { data = new
        {
            userId = context.User.FindFirstValue("sub"),
            accountType = context.User.FindFirstValue("accountType")
        }, traceId = context.TraceIdentifier });

    private static async Task<IResult> ProfileAsync(HttpContext context, ShuttleBookDbContext db, CancellationToken ct)
    {
        var user = await ProfileUser(context, db, ct);
        if (user is null) return CustomerRegistrationEndpoints.Problem(context, 403, "FORBIDDEN");
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = ProfileData(user), traceId = context.TraceIdentifier });
    }

    private static async Task<IResult> UpdateProfileAsync(HttpContext context, ShuttleBookDbContext db, CancellationToken ct)
    {
        var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, ProfileFields, ct);
        if (body.Error is not null) return body.Error;
        var name = body.Value!.Value.GetProperty("displayName").GetString()?.Trim();
        if (name is null || name.Length is < 2 or > 80 || name.Any(char.IsControl))
            return CustomerRegistrationEndpoints.Problem(context, 400, "VALIDATION_FAILED");
        var user = await ProfileUser(context, db, ct);
        if (user is null) return CustomerRegistrationEndpoints.Problem(context, 403, "FORBIDDEN");
        user.DisplayName = name; user.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent { ActorUserId = user.Id, Action = "account.profile_updated",
            EntityType = "user", EntityId = user.Id, CorrelationId = context.TraceIdentifier,
            CreatedAt = user.UpdatedAt });
        await db.SaveChangesAsync(ct);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = ProfileData(user), traceId = context.TraceIdentifier });
    }

    private static async Task<User?> ProfileUser(HttpContext context, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var id)) return null;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        return user is { AccountType: AccountType.Customer or AccountType.VenueOperator,
            Status: UserStatus.Active or UserStatus.PendingOnboarding } &&
            (user.EmailVerifiedAt is not null || user.PhoneVerifiedAt is not null) ? user : null;
    }

    private static object ProfileData(User user) => new { user.Id, user.DisplayName,
        accountType = user.AccountType.ToString().ToUpperInvariant(),
        contactType = user.NormalizedEmail is not null ? "email" : "phone",
        contact = user.Email ?? user.Phone ?? "" };

    private static IResult TokenResponse(HttpContext context, AuthTokens tokens)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { data = tokens, traceId = context.TraceIdentifier });
    }
}
