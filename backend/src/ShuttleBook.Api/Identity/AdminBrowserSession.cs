namespace ShuttleBook.Api.Identity;

internal static class AdminBrowserSession
{
    private const string CookieName = "shuttlebook_admin_refresh";

    internal static bool IsAllowedOrigin(HttpContext context)
    {
        var configured = context.RequestServices.GetRequiredService<IConfiguration>()["AdminSession:AllowedOrigin"];
        if (string.IsNullOrWhiteSpace(configured) &&
            context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            configured = "http://localhost:5175";
        return !string.IsNullOrWhiteSpace(configured) &&
            string.Equals(context.Request.Headers.Origin.ToString(), configured, StringComparison.Ordinal);
    }

    internal static string? Read(HttpContext context) =>
        IsAllowedOrigin(context) && context.Request.Cookies.TryGetValue(CookieName, out var value) ? value : null;

    internal static void Write(HttpContext context, AuthTokens tokens)
    {
        if (tokens.User.AccountType != "ADMIN" || !IsAllowedOrigin(context)) return;
        context.Response.Cookies.Append(CookieName, tokens.RefreshToken, Options(context));
    }

    internal static void Clear(HttpContext context)
    {
        if (IsAllowedOrigin(context)) context.Response.Cookies.Delete(CookieName, Options(context));
    }

    private static CookieOptions Options(HttpContext context) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1",
        MaxAge = TimeSpan.FromMinutes(30),
        IsEssential = true
    };
}
