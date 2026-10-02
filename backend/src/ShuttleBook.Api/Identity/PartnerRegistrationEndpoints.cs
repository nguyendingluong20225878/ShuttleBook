using ShuttleBook.Infrastructure.Identity;
using static ShuttleBook.Api.Identity.CustomerRegistrationEndpoints;

namespace ShuttleBook.Api.Identity;

public static class PartnerRegistrationEndpoints
{
    private static readonly HashSet<string> RegisterFields = ["contactType", "contact", "password"];
    private static readonly HashSet<string> VerifyFields = ["contactType", "contact", "code"];
    private static readonly HashSet<string> ResendFields = ["contactType", "contact"];

    public static void MapPartnerRegistrationEndpoints(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1/partner-auth");
        routes.MapPost("/register", RegisterAsync).RequireRateLimiting("auth-register");
        routes.MapPost("/verify", VerifyAsync).RequireRateLimiting("auth-verify");
        routes.MapPost("/verification-resend", ResendAsync).RequireRateLimiting("auth-register");
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, IPartnerRegistrationService service,
        ContactAttemptLimiter limiter, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context, RegisterFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var value = body.Value!.Value;
        if (!limiter.TryAcquire("register", value.GetProperty("contactType").GetString()!, value.GetProperty("contact").GetString()!,
            configuration.GetValue("Identity:RateLimits:RegisterPerContact", 3), out var retryAfter))
            return RateLimited(context, retryAfter);
        var result = await service.RegisterAsync(new RegisterPartnerCommand(
            value.GetProperty("contactType").GetString()!,
            value.GetProperty("contact").GetString()!,
            value.GetProperty("password").GetString()!,
            context.TraceIdentifier), cancellationToken);

        return RegistrationResponse(context, result);
    }

    private static async Task<IResult> VerifyAsync(HttpContext context, IPartnerRegistrationService service,
        ContactAttemptLimiter limiter, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context, VerifyFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var value = body.Value!.Value;
        if (!limiter.TryAcquire("verify", value.GetProperty("contactType").GetString()!, value.GetProperty("contact").GetString()!, 10, out var retryAfter))
            return RateLimited(context, retryAfter);
        var result = await service.VerifyAsync(new VerifyPartnerContactCommand(
            value.GetProperty("contactType").GetString()!,
            value.GetProperty("contact").GetString()!,
            value.GetProperty("code").GetString()!,
            context.TraceIdentifier), cancellationToken);

        return result switch
        {
            VerificationResult.Verified => Results.Ok(Envelope(new { verified = true, nextStep = "LOGIN" }, context)),
            VerificationResult.ValidationFailed => Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"),
            VerificationResult.RateLimited => Problem(context, StatusCodes.Status429TooManyRequests, "RATE_LIMITED"),
            _ => Problem(context, StatusCodes.Status400BadRequest, "INVALID_VERIFICATION_CODE")
        };
    }

    private static async Task<IResult> ResendAsync(HttpContext context, IPartnerRegistrationService service,
        ContactAttemptLimiter limiter, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(context, ResendFields, cancellationToken);
        if (body.Error is not null) return body.Error;
        var value = body.Value!.Value;
        if (!limiter.TryAcquire("register", value.GetProperty("contactType").GetString()!, value.GetProperty("contact").GetString()!,
            configuration.GetValue("Identity:RateLimits:RegisterPerContact", 3), out var retryAfter))
            return RateLimited(context, retryAfter);
        var result = await service.ResendAsync(new ResendPartnerVerificationCommand(
            value.GetProperty("contactType").GetString()!,
            value.GetProperty("contact").GetString()!,
            context.TraceIdentifier), cancellationToken);

        return RegistrationResponse(context, result);
    }

    private static IResult RegistrationResponse(HttpContext context, RegistrationResult result) => result switch
    {
        RegistrationResult.Accepted => Results.Accepted(value: Envelope(new
        {
            verificationRequired = true,
            message = "Nếu thông tin hợp lệ, mã xác minh đã được gửi."
        }, context)),
        RegistrationResult.ValidationFailed => Problem(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED"),
        RegistrationResult.DeliveryUnavailable => Problem(context, StatusCodes.Status503ServiceUnavailable, "IDENTITY_DELIVERY_UNAVAILABLE"),
        _ => Problem(context, StatusCodes.Status429TooManyRequests, "RATE_LIMITED")
    };

    private static object Envelope(object data, HttpContext context) => new { data, traceId = context.TraceIdentifier };
}
