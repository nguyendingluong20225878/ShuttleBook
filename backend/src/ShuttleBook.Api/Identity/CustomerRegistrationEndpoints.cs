using System.Text.Json;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Identity;

public static class CustomerRegistrationEndpoints
{
    private static readonly HashSet<string> RegisterFields = ["contactType", "contact", "password"];
    private static readonly HashSet<string> VerifyFields = ["contactType", "contact", "code"];
    private static readonly HashSet<string> ResendFields = ["contactType", "contact"];

    public static void MapCustomerRegistrationEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/auth");
        routes.MapPost("/register", RegisterAsync).RequireRateLimiting("auth-register");
        routes.MapPost("/verify-contact", VerifyAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/verification-resend", ResendAsync).RequireRateLimiting("auth-register");
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, ICustomerRegistrationService service, ContactAttemptLimiter limiter, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context, RegisterFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        if (!limiter.TryAcquire("register", body.Value!.Value.GetProperty("contactType").GetString()!,
            body.Value.Value.GetProperty("contact").GetString()!, configuration.GetValue("Identity:RateLimits:RegisterPerContact", 3), out var retryAfter))
            return RateLimited(context, retryAfter);
        var result = await service.RegisterAsync(new RegisterCustomerCommand(
            body.Value!.Value.GetProperty("contactType").GetString() ?? string.Empty,
            body.Value.Value.GetProperty("contact").GetString() ?? string.Empty,
            body.Value.Value.GetProperty("password").GetString() ?? string.Empty,
            context.TraceIdentifier), cancellationToken);
        return result switch
        {
            RegistrationResult.Accepted => Results.Accepted(value: Envelope(new { verificationRequired = true,
                message = "Nếu thông tin hợp lệ, mã xác minh đã được gửi." }, context)),
            RegistrationResult.ValidationFailed => Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"),
            RegistrationResult.DeliveryUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, "IDENTITY_DELIVERY_UNAVAILABLE"),
            _ => Problem(context, StatusCodes.Status429TooManyRequests, "RATE_LIMITED")
        };
    }

    private static async Task<IResult> VerifyAsync(HttpContext context, ICustomerRegistrationService service, ContactAttemptLimiter limiter, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context, VerifyFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        if (!limiter.TryAcquire("verify", body.Value!.Value.GetProperty("contactType").GetString()!,
            body.Value.Value.GetProperty("contact").GetString()!, 10, out var retryAfter))
            return RateLimited(context, retryAfter);
        var result = await service.VerifyAsync(new VerifyContactCommand(
            body.Value!.Value.GetProperty("contactType").GetString() ?? string.Empty,
            body.Value.Value.GetProperty("contact").GetString() ?? string.Empty,
            body.Value.Value.GetProperty("code").GetString() ?? string.Empty,
            context.TraceIdentifier), cancellationToken);
        return result switch
        {
            VerificationResult.Verified => Results.Ok(Envelope(new { verified = true, nextStep = "LOGIN" }, context)),
            VerificationResult.ValidationFailed => Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"),
            _ => Problem(context, StatusCodes.Status400BadRequest, "INVALID_VERIFICATION_CODE")
        };
    }

    private static async Task<IResult> ResendAsync(HttpContext context, ICustomerRegistrationService service, ContactAttemptLimiter limiter, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context, ResendFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        if (!limiter.TryAcquire("register", body.Value!.Value.GetProperty("contactType").GetString()!,
            body.Value.Value.GetProperty("contact").GetString()!, configuration.GetValue("Identity:RateLimits:RegisterPerContact", 3), out var retryAfter))
            return RateLimited(context, retryAfter);
        var result = await service.ResendAsync(new ResendVerificationCommand(
            body.Value!.Value.GetProperty("contactType").GetString() ?? string.Empty,
            body.Value.Value.GetProperty("contact").GetString() ?? string.Empty,
            context.TraceIdentifier), cancellationToken);
        return result switch
        {
            RegistrationResult.Accepted => Results.Accepted(value: Envelope(new { verificationRequired = true,
                message = "Náº¿u thĂ´ng tin há»£p lá»‡, mĂ£ xĂ¡c minh Ä‘Ă£ Ä‘Æ°á»£c gá»­i." }, context)),
            RegistrationResult.ValidationFailed => Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"),
            RegistrationResult.DeliveryUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, "IDENTITY_DELIVERY_UNAVAILABLE"),
            _ => Problem(context, StatusCodes.Status429TooManyRequests, "RATE_LIMITED")
        };
    }

    internal static async Task<(JsonElement? Value, IResult? Error)> ReadBodyAsync(HttpContext context, HashSet<string> fields, CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength is > 1_024) return (null, Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"));
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[1_025];
            int read;
            while ((read = await context.Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > 1_024)
                    return (null, Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"));
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (null, Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!fields.Contains(property.Name)) return (null, Problem(context, StatusCodes.Status400BadRequest, "UNSUPPORTED_FIELD"));
                if (!seen.Add(property.Name) || property.Value.ValueKind != JsonValueKind.String)
                    return (null, Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"));
            }
            if (fields.Any(field => !document.RootElement.TryGetProperty(field, out _)))
                return (null, Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"));
            return (document.RootElement.Clone(), null);
        }
        catch (JsonException)
        {
            return (null, Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"));
        }
    }

    private static object Envelope(object data, HttpContext context) => new { data, traceId = context.TraceIdentifier };
    internal static IResult RateLimited(HttpContext context, int retryAfterSeconds)
    {
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        return Problem(context, StatusCodes.Status429TooManyRequests, "RATE_LIMITED");
    }
    internal static IResult Problem(HttpContext context, int status, string code) => Results.Json(new
    {
        type = "about:blank", title = status == 429 ? "Too Many Requests" : "Bad Request", status, code, traceId = context.TraceIdentifier
    }, statusCode: status, contentType: "application/problem+json");
}
