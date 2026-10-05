using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ShuttleBook.Api.Identity;

namespace ShuttleBook.Api.Tests;

public sealed class AdminAuthApiTests
{
    [Theory]
    [InlineData("accountType")]
    [InlineData("role")]
    [InlineData("status")]
    [InlineData("permissions")]
    [InlineData("isAdmin")]
    [InlineData("userId")]
    public async Task Admin_login_rejects_privilege_fields_before_service(string field)
    {
        var service = new RejectedAuthService();
        await using var factory = new AdminApiFactory(service);
        using var client = factory.CreateClient();
        var payload = new Dictionary<string, object>
        {
            ["contactType"] = "email", ["contact"] = "admin-test@example.test",
            ["password"] = "Admin-Test-Password-2026!", [field] = "ADMIN"
        };
        using var response = await client.PostAsJsonAsync("/api/v1/admin-auth/login", payload);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
        Assert.Equal(0, service.LoginCalls);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"contactType\":42,\"contact\":\"admin-test@example.test\",\"password\":\"Admin-Test-Password-2026!\"}")]
    [InlineData("{\"contactType\":\"email\",\"contact\":\"admin-test@example.test\"}")]
    [InlineData("{\"contactType\":\"email\",\"contact\":\"invalid\",\"password\":\"Admin-Test-Password-2026!\"}")]
    public async Task Admin_login_rejects_invalid_body_without_side_effect(string body)
    {
        var service = new RejectedAuthService();
        await using var factory = new AdminApiFactory(service);
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/admin-auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.Equal(0, service.LoginCalls);
    }

    [Fact]
    public async Task Admin_login_rejects_oversize_password_and_limits_attempts()
    {
        var service = new RejectedAuthService();
        await using var factory = new AdminApiFactory(service);
        using var client = factory.CreateClient();
        using (var tooLong = await client.PostAsJsonAsync("/api/v1/admin-auth/login", new
               { contactType = "email", contact = "admin-test@example.test", password = new string('x', 129) }))
            await AssertProblemAsync(tooLong, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        using (var overBodyLimit = await client.PostAsJsonAsync("/api/v1/admin-auth/login", new
               { contactType = "email", contact = "admin-test@example.test", password = new string('x', 1025) }))
            await AssertProblemAsync(overBodyLimit, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.Equal(0, service.LoginCalls);
        var request = new { contactType = "email", contact = "admin-test@example.test", password = "wrong-password" };
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var rejected = await client.PostAsJsonAsync("/api/v1/admin-auth/login", request);
            await AssertProblemAsync(rejected, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        }
        using var limited = await client.PostAsJsonAsync("/api/v1/admin-auth/login", request);
        await AssertProblemAsync(limited, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Equal(10, service.LoginCalls);
    }

    [Fact]
    public async Task Admin_me_rejects_missing_and_invalid_bearer()
    {
        await using var factory = new AdminApiFactory(new RejectedAuthService());
        using var client = factory.CreateClient();
        using (var missing = await client.GetAsync("/api/v1/admin-auth/me"))
            await AssertProblemAsync(missing, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin-auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var invalid = await client.SendAsync(request);
        await AssertProblemAsync(invalid, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Admin_me_rejects_wrong_signature_issuer_audience_and_expired_jwt()
    {
        await using var factory = new AdminApiFactory(new RejectedAuthService());
        using var client = factory.CreateClient();
        foreach (var token in new[]
        {
            Jwt("wrong-admin-api-signing-key-32-bytes-minimum", "ShuttleBook", "ShuttleBook.Web", false),
            Jwt("admin-api-test-signing-key-32-bytes-minimum", "OtherIssuer", "ShuttleBook.Web", false),
            Jwt("admin-api-test-signing-key-32-bytes-minimum", "ShuttleBook", "OtherAudience", false),
            Jwt("admin-api-test-signing-key-32-bytes-minimum", "ShuttleBook", "ShuttleBook.Web", true)
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin-auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request);
            await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        }
    }

    private static string Jwt(string key, string issuer, string audience, bool expired)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString()),
                new Claim("accountType", "ADMIN"), new Claim("sid", Guid.CreateVersion7().ToString())],
            now.AddMinutes(-10), expired ? now.AddMinutes(-1) : now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("admin-test@example.test", body.RootElement.ToString());
    }

    private sealed class AdminApiFactory(RejectedAuthService service) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ShuttleBook"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                    ["Identity:JwtSigningKey"] = "admin-api-test-signing-key-32-bytes-minimum",
                    ["RateLimits:AuthVerifyPerIp"] = "1000"
                }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthSessionService>();
                services.AddSingleton<IAuthSessionService>(service);
            });
        }
    }

    private sealed class RejectedAuthService : IAuthSessionService
    {
        public int LoginCalls { get; private set; }
        public Task<AuthTokens?> AdminLoginAsync(string contactType, string contact, string password,
            string traceId, CancellationToken cancellationToken)
        {
            LoginCalls++;
            return Task.FromResult<AuthTokens?>(null);
        }
        public Task<AuthTokens?> LoginAsync(string contactType, string contact, string password,
            string traceId, CancellationToken cancellationToken) => Task.FromResult<AuthTokens?>(null);
        public Task<AuthTokens?> RefreshAsync(string refreshToken, string traceId,
            CancellationToken cancellationToken) => Task.FromResult<AuthTokens?>(null);
        public Task<bool> LogoutAsync(Guid userId, Guid familyId, string refreshToken,
            string traceId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
