using ShuttleBook.Api.Errors;
using ShuttleBook.Api.Identity;
using ShuttleBook.Api.Onboarding;
using ShuttleBook.Api.Operations;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Health;
using ShuttleBook.Infrastructure.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Amazon;
using Amazon.S3;
using ShuttleBook.Api.Discovery;
using ShuttleBook.Api.Bookings;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    if (!OperatingSystem.IsWindows())
        throw new InvalidOperationException("The configured local data protection key path requires Windows DPAPI.");
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).ProtectKeysWithDpapi();
}
var authRegisterPerIp = Math.Max(1, builder.Configuration.GetValue("RateLimits:AuthRegisterPerIp", 5));
var authVerifyPerIp = Math.Max(1, builder.Configuration.GetValue("RateLimits:AuthVerifyPerIp", 10));
builder.Services.AddShuttleBookDatabase(builder.Configuration);
builder.Services.AddScoped<IReadinessProbe, PostgresReadinessProbe>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<ICustomerRegistrationService, CustomerRegistrationService>();
builder.Services.AddScoped<IContactVerificationDelivery, SmtpContactVerificationDelivery>();
builder.Services.AddScoped<IPartnerRegistrationService, PartnerRegistrationService>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<IBrowserAuthSessionService, AuthSessionService>();
builder.Services.AddSingleton<ContactAttemptLimiter>();
if (string.Equals(builder.Configuration["Media:Mode"], "S3", StringComparison.OrdinalIgnoreCase))
{
    var region = builder.Configuration["Media:S3Region"];
    var bucket = builder.Configuration["Media:S3Bucket"];
    if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(bucket))
        throw new InvalidOperationException("S3 media requires region and private bucket configuration.");
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(RegionEndpoint.GetBySystemName(region)));
}
var jwtKey = builder.Configuration["Identity:JwtSigningKey"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Identity signing key must be configured with at least 32 UTF-8 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Identity:JwtIssuer"] ?? "ShuttleBook",
        ValidateAudience = true, ValidAudience = builder.Configuration["Identity:JwtAudience"] ?? "ShuttleBook.Web",
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = JwtSessionValidation.HandleRequestCancellation(async context =>
        {
            var principal = context.Principal;
            if (!Guid.TryParse(principal?.FindFirst("sub")?.Value, out var userId) ||
                !Guid.TryParse(principal.FindFirst("sid")?.Value, out var familyId))
            {
                context.Fail("Invalid session.");
                return;
            }
            var database = context.HttpContext.RequestServices.GetRequiredService<ShuttleBookDbContext>();
            var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var user = await database.Users.SingleOrDefaultAsync(item => item.Id == userId, context.HttpContext.RequestAborted);
            if (user is null)
            {
                context.Fail("Inactive session.");
                return;
            }
            var familyActive = await database.RefreshSessions.AnyAsync(item =>
                item.UserId == userId && item.FamilyId == familyId && item.RevokedAt == null &&
                item.ConsumedAt == null && item.ExpiresAt > now &&
                (user!.AccountType == AccountType.Admin || item.LastActivityAt == null || item.LastActivityAt > now.AddMinutes(-30)),
                context.HttpContext.RequestAborted);
            if (user is null || !AuthSessionService.CanUseSession(user) || !familyActive ||
                principal.FindFirst("accountType")?.Value != AuthSessionService.AccountTypeName(user.AccountType))
            {
                context.Fail("Inactive session.");
                return;
            }
            if (user.AccountType == AccountType.Admin)
            {
                var activity = await database.RefreshSessions.Where(item => item.UserId == userId &&
                    item.FamilyId == familyId && item.RevokedAt == null)
                    .OrderByDescending(item => item.LastActivityAt).Select(item => item.LastActivityAt)
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                if (activity is null || activity <= now.AddMinutes(-30))
                {
                    context.Fail("Inactive admin session.");
                    return;
                }
                await database.RefreshSessions.Where(item => item.UserId == userId &&
                    item.FamilyId == familyId && item.RevokedAt == null)
                    .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.LastActivityAt, now),
                        context.HttpContext.RequestAborted);
            }
        })
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "900";
        context.HttpContext.Response.ContentType = "application/problem+json";
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "about:blank", title = "Too Many Requests", status = StatusCodes.Status429TooManyRequests,
            code = "RATE_LIMITED", traceId = context.HttpContext.TraceIdentifier
        }));
    };
    options.AddPolicy("auth-register", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = authRegisterPerIp, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true
    }));
    options.AddPolicy("booking-quote", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("booking-create", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("auth-verify", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = authVerifyPerIp, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true
    }));
});

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
    || (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo)
    || origin.Contains('*') || origin != uri.GetLeftPart(UriPartial.Authority)))
{
    throw new InvalidOperationException("Cors:AllowedOrigins must contain exact HTTP(S) origins without a path, trailing slash, wildcard or credentials.");
}
builder.Services.AddCors(options => options.AddPolicy("WebPortals", policy =>
{
    if (origins.Length > 0)
    {
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Retry-After");
    }
}));

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/browser-auth"))
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseCors("WebPortals");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health/live", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Json(new { status = "Healthy" });
});

app.MapGet("/health/ready", async (IReadinessProbe readiness, HttpContext context, CancellationToken cancellationToken) =>
{
    var ready = await readiness.IsReadyAsync(cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Json(new { status = ready ? "Healthy" : "Unhealthy" },
        statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapCustomerRegistrationEndpoints();
app.MapAuthSessionEndpoints();
app.MapBrowserAuthEndpoints();
app.MapAdminAuthEndpoints();
app.MapPartnerRegistrationEndpoints();
app.MapOnboardingEndpoints();
app.MapMediaEndpoints();
app.MapNotificationEndpoints();
app.MapCourtOperationsEndpoints();
app.MapPublicVenuesEndpoints();
app.MapBookingEndpoints();
app.MapSeriesEndpoints();
app.MapPaymentEndpoints();

app.Run();

public partial class Program;
