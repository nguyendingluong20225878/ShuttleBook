using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Tests;

public sealed class PartnerRegistrationApiTests
{
    [Fact]
    public async Task Register_accepts_only_partner_registration_fields_and_never_returns_a_code()
    {
        var service = new FakePartnerRegistrationService();
        await using var factory = new PartnerApiFactory(service);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/partner-auth/register", new
        {
            contactType = "email", contact = "partner@example.test", password = "Example-password-2026!"
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(service.LastRegister);
        Assert.Equal("partner@example.test", service.LastRegister.Contact);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Example-password-2026!", text);
        Assert.DoesNotContain("123456", text);
        Assert.DoesNotContain("VENUE_OPERATOR", text);
        using var json = JsonDocument.Parse(text);
        Assert.True(json.RootElement.GetProperty("data").GetProperty("verificationRequired").GetBoolean());
    }

    [Theory]
    [InlineData("accountType")]
    [InlineData("role")]
    [InlineData("status")]
    [InlineData("isAdmin")]
    public async Task Register_rejects_role_or_status_injection_before_calling_service(string field)
    {
        var service = new FakePartnerRegistrationService();
        await using var factory = new PartnerApiFactory(service);
        using var client = factory.CreateClient();
        var payload = new Dictionary<string, object>
        {
            ["contactType"] = "email", ["contact"] = "partner@example.test",
            ["password"] = "Example-password-2026!", [field] = "ADMIN"
        };

        using var response = await client.PostAsJsonAsync("/api/v1/partner-auth/register", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("UNSUPPORTED_FIELD", await CodeAsync(response));
        Assert.Null(service.LastRegister);
    }

    [Theory]
    [InlineData("{\"contactType\":42,\"contact\":\"partner@example.test\",\"password\":\"Example-password-2026!\"}")]
    [InlineData("{\"contactType\":\"email\",\"contact\":\"partner@example.test\"}")]
    [InlineData("[]")]
    public async Task Register_rejects_wrong_json_shape_without_a_server_error(string payload)
    {
        var service = new FakePartnerRegistrationService();
        await using var factory = new PartnerApiFactory(service);
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/partner-auth/register",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await CodeAsync(response));
        Assert.Null(service.LastRegister);
    }

    [Fact]
    public async Task Verify_and_resend_map_to_partner_service_without_exposing_account_details()
    {
        var service = new FakePartnerRegistrationService();
        await using var factory = new PartnerApiFactory(service);
        using var client = factory.CreateClient();

        using var resend = await client.PostAsJsonAsync("/api/v1/partner-auth/verification-resend", new
        {
            contactType = "email", contact = "partner@example.test"
        });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.NotNull(service.LastResend);

        using var verify = await client.PostAsJsonAsync("/api/v1/partner-auth/verify", new
        {
            contactType = "email", contact = "partner@example.test", code = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.NotNull(service.LastVerify);
        using var json = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        Assert.Equal("LOGIN", json.RootElement.GetProperty("data").GetProperty("nextStep").GetString());
        Assert.False(json.RootElement.GetProperty("data").TryGetProperty("accountType", out _));
    }

    [Fact]
    public async Task Register_limits_repeated_requests_for_the_same_contact_before_calling_service()
    {
        var service = new FakePartnerRegistrationService();
        await using var factory = new PartnerApiFactory(service);
        using var client = factory.CreateClient();
        var request = new { contactType = "email", contact = "limited@example.test", password = "Example-password-2026!" };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var accepted = await client.PostAsJsonAsync("/api/v1/partner-auth/register", request);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
        using var rejected = await client.PostAsJsonAsync("/api/v1/partner-auth/register", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("RATE_LIMITED", await CodeAsync(rejected));
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Equal(3, service.RegisterCalls);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private sealed class PartnerApiFactory(FakePartnerRegistrationService service) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ShuttleBook"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                    ["Identity:JwtSigningKey"] = "test-only-jwt-signing-key-32-bytes-minimum",
                    ["Identity:OtpPepper"] = "test-only-otp-pepper-32-bytes-minimum"
                }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPartnerRegistrationService>();
                services.AddSingleton<IPartnerRegistrationService>(service);
            });
        }
    }

    private sealed class FakePartnerRegistrationService : IPartnerRegistrationService
    {
        public int RegisterCalls { get; private set; }
        public RegisterPartnerCommand? LastRegister { get; private set; }
        public VerifyPartnerContactCommand? LastVerify { get; private set; }
        public ResendPartnerVerificationCommand? LastResend { get; private set; }

        public Task<RegistrationResult> RegisterAsync(RegisterPartnerCommand command, CancellationToken cancellationToken)
        {
            RegisterCalls++;
            LastRegister = command;
            return Task.FromResult(RegistrationResult.Accepted);
        }

        public Task<VerificationResult> VerifyAsync(VerifyPartnerContactCommand command, CancellationToken cancellationToken)
        {
            LastVerify = command;
            return Task.FromResult(VerificationResult.Verified);
        }

        public Task<RegistrationResult> ResendAsync(ResendPartnerVerificationCommand command, CancellationToken cancellationToken)
        {
            LastResend = command;
            return Task.FromResult(RegistrationResult.Accepted);
        }
    }
}
