using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
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

public sealed class CustomerRegistrationApiTests
{
    [Fact]
    public async Task Register_verify_and_resend_use_allowlisted_contract_without_secrets()
    {
        var service = new FakeCustomerRegistrationService();
        await using var factory = new CustomerApiFactory(service);
        using var client = factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            contactType = "email", contact = "f011-api@example.test", password = "F011-test-password-2026!"
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        Assert.NotNull(service.LastRegister);
        await AssertSafeResponseAsync(register, "F011-test-password-2026!");

        using var resend = await client.PostAsJsonAsync("/api/v1/auth/verification-resend", new
        {
            contactType = "email", contact = "f011-api@example.test"
        });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.NotNull(service.LastResend);
        await AssertSafeResponseAsync(resend, "f011-api@example.test");

        using var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-contact", new
        {
            contactType = "email", contact = "f011-api@example.test", code = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.NotNull(service.LastVerify);
        await AssertSafeResponseAsync(verify, "123456");
    }

    [Theory]
    [InlineData("accountType")]
    [InlineData("role")]
    [InlineData("status")]
    [InlineData("isAdmin")]
    [InlineData("permissions")]
    [InlineData("userId")]
    [InlineData("unknown")]
    public async Task Register_rejects_extra_fields_before_service(string field)
    {
        var service = new FakeCustomerRegistrationService();
        await using var factory = new CustomerApiFactory(service);
        using var client = factory.CreateClient();
        var payload = new Dictionary<string, object>
        {
            ["contactType"] = "email", ["contact"] = "f011-api@example.test",
            ["password"] = "F011-test-password-2026!", [field] = "ADMIN"
        };
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
        Assert.Null(service.LastRegister);
    }

    [Theory]
    [InlineData("{\"contactType\":42,\"contact\":\"f011-api@example.test\",\"password\":\"F011-test-password-2026!\"}")]
    [InlineData("{\"contactType\":\"email\",\"contact\":\"f011-api@example.test\"}")]
    [InlineData("{\"contactType\":\"email\",\"contact\":null,\"password\":\"F011-test-password-2026!\"}")]
    [InlineData("[]")]
    public async Task Register_rejects_invalid_json_shape(string payload)
    {
        var service = new FakeCustomerRegistrationService();
        await using var factory = new CustomerApiFactory(service);
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/auth/register",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.Null(service.LastRegister);
    }

    [Fact]
    public async Task Register_rejects_body_over_limit()
    {
        var service = new FakeCustomerRegistrationService();
        await using var factory = new CustomerApiFactory(service);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            contactType = "email", contact = "f011-api@example.test", password = new string('x', 1025)
        });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.Null(service.LastRegister);
    }

    [Fact]
    public async Task Duplicate_public_response_is_indistinguishable_and_rate_limit_has_retry_after()
    {
        var service = new FakeCustomerRegistrationService();
        await using var factory = new CustomerApiFactory(service);
        using var client = factory.CreateClient();
        var request = new { contactType = "email", contact = "f011-rate@example.test", password = "F011-test-password-2026!" };
        string? standardMessage = null;
        for (var i = 0; i < 3; i++)
        {
            using var accepted = await client.PostAsJsonAsync("/api/v1/auth/register", request);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            using var json = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
            var message = json.RootElement.GetProperty("data").GetProperty("message").GetString();
            standardMessage ??= message;
            Assert.Equal(standardMessage, message);
        }
        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Equal(3, service.RegisterCalls);
    }

    [Fact]
    public async Task Verify_rate_limit_has_retry_after_and_stops_service()
    {
        var service = new FakeCustomerRegistrationService();
        await using var factory = new CustomerApiFactory(service);
        using var client = factory.CreateClient();
        var request = new { contactType = "email", contact = "f011-verify-rate@example.test", code = "123456" };
        for (var i = 0; i < 10; i++)
        {
            using var accepted = await client.PostAsJsonAsync("/api/v1/auth/verify-contact", request);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/verify-contact", request);
        await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Equal(10, service.VerifyCalls);
    }

    [Fact]
    public async Task Internal_failure_does_not_put_contact_password_or_code_in_response_or_log()
    {
        const string contact = "f011-private@example.test";
        const string password = "F011-private-password-2026!";
        const string code = "654321";
        var service = new FakeCustomerRegistrationService { FailureMessage = $"{contact} {password} {code}" };
        using var logs = new CapturingLoggerProvider();
        await using var factory = new CustomerApiFactory(service, logs);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            contactType = "email", contact, password
        });
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "INTERNAL_ERROR");
        var output = await response.Content.ReadAsStringAsync() + string.Join(' ', logs.Messages);
        Assert.DoesNotContain(contact, output);
        Assert.DoesNotContain(password, output);
        Assert.DoesNotContain(code, output);
    }

    private static async Task AssertSafeResponseAsync(HttpResponseMessage response, string secret)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("passwordHash", text);
        Assert.DoesNotContain("codeHash", text);
        using var json = JsonDocument.Parse(text);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
    }

    private sealed class CustomerApiFactory(FakeCustomerRegistrationService service, ILoggerProvider? loggerProvider = null) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ShuttleBook"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                    ["Identity:JwtSigningKey"] = "test-only-jwt-signing-key-32-bytes-minimum",
                    ["Identity:OtpPepper"] = "test-only-otp-pepper-32-bytes-minimum",
                    ["RateLimits:AuthRegisterPerIp"] = "100",
                    ["RateLimits:AuthVerifyPerIp"] = "100"
                }));
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                if (loggerProvider is not null) logging.AddProvider(loggerProvider);
            });
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICustomerRegistrationService>();
                services.AddSingleton<ICustomerRegistrationService>(service);
            });
        }
    }

    private sealed class FakeCustomerRegistrationService : ICustomerRegistrationService
    {
        public string? FailureMessage { get; init; }
        public int RegisterCalls { get; private set; }
        public int VerifyCalls { get; private set; }
        public RegisterCustomerCommand? LastRegister { get; private set; }
        public VerifyContactCommand? LastVerify { get; private set; }
        public ResendVerificationCommand? LastResend { get; private set; }
        public Task<RegistrationResult> RegisterAsync(RegisterCustomerCommand command, CancellationToken cancellationToken)
        {
            if (FailureMessage is not null) throw new InvalidOperationException(FailureMessage);
            RegisterCalls++;
            LastRegister = command;
            return Task.FromResult(RegistrationResult.Accepted);
        }
        public Task<VerificationResult> VerifyAsync(VerifyContactCommand command, CancellationToken cancellationToken)
        {
            VerifyCalls++;
            LastVerify = command;
            return Task.FromResult(VerificationResult.Verified);
        }
        public Task<RegistrationResult> ResendAsync(ResendVerificationCommand command, CancellationToken cancellationToken)
        {
            LastResend = command;
            return Task.FromResult(RegistrationResult.Accepted);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();
        public IEnumerable<string> Messages => messages;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => Scope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + exception?.ToString());
        }

        private sealed class Scope : IDisposable
        {
            public static readonly Scope Instance = new();
            public void Dispose() { }
        }
    }
}
