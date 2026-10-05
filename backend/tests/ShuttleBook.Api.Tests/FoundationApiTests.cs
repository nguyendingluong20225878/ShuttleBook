using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Health;

namespace ShuttleBook.Api.Tests;

public sealed class FoundationApiTests
{
    [Fact]
    public async Task Liveness_does_not_probe_the_database()
    {
        var probe = new StubReadiness(false);
        await using var factory = new ApiFactory(probe);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await ReadStatusAsync(response));
        Assert.Equal(0, probe.CallCount);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK, "Healthy")]
    [InlineData(false, HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task Readiness_returns_only_a_public_status(bool ready, HttpStatusCode expected, string status)
    {
        await using var factory = new ApiFactory(new StubReadiness(ready));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(expected, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal(status, body.RootElement.GetProperty("status").GetString());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_with_trace_id()
    {
        await using var factory = new ApiFactory(new StubReadiness(true));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/route-that-does-not-exist");
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task Unhandled_exception_never_exposes_exception_details()
    {
        await using var factory = new ApiFactory(new ThrowingReadiness());
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
        Assert.DoesNotContain("secret-sentinel", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://localhost:5174", true)]
    [InlineData("http://localhost:5175", true)]
    [InlineData("http://localhost:5176", false)]
    [InlineData("https://localhost:5173", false)]
    [InlineData("http://localhost:5173.attacker.example", false)]
    public async Task Cors_preflight_uses_exact_origin_allowlist(string origin, bool allowed)
    {
        await using var factory = new ApiFactory(new StubReadiness(true));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health/live");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);

        Assert.Equal(allowed, response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(values!));
        }
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Actual_cors_response_exposes_retry_after_to_browser()
    {
        await using var factory = new ApiFactory(new StubReadiness(true));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "http://localhost:5175");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Retry-After", response.Headers.GetValues("Access-Control-Expose-Headers"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Missing_database_configuration_fails_without_fallback(string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.RequireConnectionString(value));
        Assert.Contains("Missing ConnectionStrings__ShuttleBook", error.Message);
    }

    [Fact]
    public void Invalid_database_configuration_does_not_echo_secret()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.RequireConnectionString("Password=secret-sentinel;invalid-key=value"));
        Assert.DoesNotContain("secret-sentinel", error.ToString());
        Assert.Null(error.InnerException);
    }

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("status").GetString();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    private sealed class ApiFactory(IReadinessProbe probe) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Host configuration is available when the minimal entry point constructs its builder.
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ShuttleBook"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                    ["Identity:JwtSigningKey"] = "test-only-jwt-signing-key-32-bytes-minimum",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                    ["Cors:AllowedOrigins:1"] = "http://localhost:5174",
                    ["Cors:AllowedOrigins:2"] = "http://localhost:5175"
                }));
            // TestHost must not write failures to Windows Event Log, which requires elevated OS permissions.
            builder.ConfigureLogging(logging => logging.ClearProviders());
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReadinessProbe>();
                services.AddSingleton(probe);
            });
        }
    }

    private sealed class StubReadiness(bool ready) : IReadinessProbe
    {
        public int CallCount { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(ready);
        }
    }

    private sealed class ThrowingReadiness : IReadinessProbe
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret-sentinel");
    }
}
