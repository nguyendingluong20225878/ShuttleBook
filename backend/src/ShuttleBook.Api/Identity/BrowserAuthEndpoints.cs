using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Identity;

public static class BrowserAuthEndpoints
{
    private static readonly HashSet<string> LoginFields = ["contactType", "contact", "password"];

    public static void MapBrowserAuthEndpoints(this WebApplication app)
    {
        foreach (var portal in new[] { "customer", "partner" })
        {
            var routes = app.MapGroup($"/api/v1/browser-auth/{portal}");
            var role = portal == "customer" ? AccountType.Customer : AccountType.VenueOperator;
            routes.AddEndpointFilter(async (context, next) =>
            {
                var http = context.HttpContext;
                http.Response.Headers.CacheControl = "no-store";
                if (!IsAllowedOrigin(http, portal))
                    return CustomerRegistrationEndpoints.Problem(http, 403, "FORBIDDEN");
                if (!string.Equals(http.Request.ContentType?.Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
                    return CustomerRegistrationEndpoints.Problem(http, 400, "VALIDATION_FAILED");
                return await next(context);
            });
            routes.MapPost("/login", async (HttpContext context, IBrowserAuthSessionService service,
                ContactAttemptLimiter limiter, CancellationToken cancellationToken) =>
            {
                var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, LoginFields, cancellationToken);
                if (body.Error is not null) return body.Error;
                var value = body.Value!.Value;
                var contactType = value.GetProperty("contactType").GetString()!;
                var contact = value.GetProperty("contact").GetString()!;
                if (!limiter.TryAcquire($"browser-{portal}-login", contactType, contact, 10, out var retryAfter))
                    return CustomerRegistrationEndpoints.RateLimited(context, retryAfter);
                var result = await service.BrowserLoginAsync(role, contactType, contact,
                    value.GetProperty("password").GetString()!, context.TraceIdentifier, cancellationToken);
                if (result is null) return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_CREDENTIALS");
                WriteCookie(context, portal, result.Tokens);
                return Response(context, result);
            }).RequireRateLimiting("auth-verify");
            foreach (var action in new[] { "restore", "activity" })
                routes.MapPost($"/{action}", async (HttpContext context, IBrowserAuthSessionService service, CancellationToken cancellationToken) =>
                {
                    var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, [], cancellationToken);
                    if (body.Error is not null) return body.Error;
                    var result = await service.RestoreBrowserAsync(role, ReadCookie(context, portal), action == "activity",
                        context.TraceIdentifier, cancellationToken);
                    if (result is null)
                    {
                        // A delayed response for an old family must not erase a newer login cookie.
                        // Explicit logout owns deletion; invalid credentials already cannot authenticate.
                        return CustomerRegistrationEndpoints.Problem(context, 401, "INVALID_REFRESH_TOKEN");
                    }
                    return Response(context, result);
                });
            routes.MapPost("/logout", async (HttpContext context, IBrowserAuthSessionService service, CancellationToken cancellationToken) =>
            {
                var body = await CustomerRegistrationEndpoints.ReadBodyAsync(context, [], cancellationToken);
                if (body.Error is not null) return body.Error;
                await service.LogoutBrowserAsync(role, ReadCookie(context, portal), context.TraceIdentifier, cancellationToken);
                ClearCookie(context, portal);
                return Results.NoContent();
            });
        }
    }

    private static bool IsAllowedOrigin(HttpContext context, string portal)
    {
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var configured = configuration[$"BrowserSession:{(portal == "customer" ? "Customer" : "Partner")}Origin"];
        if (string.IsNullOrWhiteSpace(configured) && environment.IsDevelopment())
            configured = portal == "customer" ? "http://localhost:5173" : "http://localhost:5174";
        return !string.IsNullOrWhiteSpace(configured) &&
            string.Equals(context.Request.Headers.Origin.ToString(), configured, StringComparison.Ordinal);
    }

    private static string Name(string portal) => $"shuttlebook_{portal}_refresh";
    private static string ReadCookie(HttpContext context, string portal) => context.Request.Cookies[Name(portal)] ?? "";
    private static CookieOptions Options(HttpContext context, string portal) => new()
    {
        HttpOnly = true, SameSite = SameSiteMode.Strict, IsEssential = true,
        Secure = context.Request.IsHttps || !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(),
        Path = $"/api/v1/browser-auth/{portal}"
    };
    private static void WriteCookie(HttpContext context, string portal, AuthTokens tokens)
    {
        var options = Options(context, portal);
        options.MaxAge = tokens.RefreshExpiresAt - context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        context.Response.Cookies.Append(Name(portal), tokens.RefreshToken, options);
    }
    private static void ClearCookie(HttpContext context, string portal) => context.Response.Cookies.Delete(Name(portal), Options(context, portal));
    private static IResult Response(HttpContext context, BrowserAuthTokens result) => Results.Ok(new
    {
        data = new { result.Tokens.TokenType, result.Tokens.AccessToken, result.Tokens.ExpiresInSeconds,
            result.Tokens.RefreshExpiresAt, result.IdleExpiresAt, result.Tokens.User }, traceId = context.TraceIdentifier
    });
}
