using System.Security.Claims;

namespace ShuttleBook.Api.Identity;

public static class AuthSessionEndpoints
{
    private static readonly HashSet<string> LoginFields = ["contactType", "contact", "password"];
    private static readonly HashSet<string> RefreshFields = ["refreshToken"];

    public static void MapAuthSessionEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/auth");
        routes.MapPost("/login", LoginAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/refresh", RefreshAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/logout", LogoutAsync).RequireAuthorization();
        routes.MapGet("/me", MeAsync).RequireAuthorization();
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
            : Results.Ok(new { data = tokens, traceId = context.TraceIdentifier });
    }

    private static async Task<IResult> RefreshAsync(HttpContext context, IAuthSessionService service, CancellationToken cancellationToken)
    {
        var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, RefreshFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var tokens = await service.RefreshAsync(body.Value!.Value.GetProperty("refreshToken").GetString()!,
            context.TraceIdentifier, cancellationToken);
        return tokens is null
            ? CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_REFRESH_TOKEN")
            : Results.Ok(new { data = tokens, traceId = context.TraceIdentifier });
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
        return Results.NoContent();
    }

    private static IResult MeAsync(HttpContext context) =>
        Results.Ok(new { data = new
        {
            userId = context.User.FindFirstValue("sub"),
            accountType = context.User.FindFirstValue("accountType")
        }, traceId = context.TraceIdentifier });
}
