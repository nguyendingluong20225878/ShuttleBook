using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ShuttleBook.Api.Identity;

namespace ShuttleBook.Api.Tests;

public sealed class JwtSessionCancellationTests
{
    [Fact]
    public async Task Client_cancelling_session_query_returns_no_authentication_without_JWT_error_log()
    {
        using var abort = new CancellationTokenSource();
        var calls = 0;
        var (result, logs) = await Authenticate(abort.Token, _ =>
        {
            calls++;
            abort.Cancel();
            throw new OperationCanceledException(abort.Token);
        });

        Assert.Equal(1, calls);
        Assert.True(result.None);
        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
        Assert.Null(result.Failure);
        Assert.DoesNotContain(logs, log => log.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Cancellation_unrelated_to_client_propagates_and_remains_a_logged_error()
    {
        var failure = new OperationCanceledException("Test query cancellation unrelated to RequestAborted.");
        var logger = new CapturingLoggerProvider();
        var propagated = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Authenticate(CancellationToken.None, _ => throw failure, captureLogs: logger));

        Assert.Same(failure, propagated);
        Assert.Contains(logger.Entries, log => log.Category.Contains("JwtBearerHandler", StringComparison.Ordinal) &&
            log.Level == LogLevel.Error && ReferenceEquals(log.Exception, failure));
    }

    [Fact]
    public async Task Already_aborted_request_skips_session_queries_and_does_not_authenticate()
    {
        using var abort = new CancellationTokenSource();
        abort.Cancel();
        var calls = 0;
        var (result, logs) = await Authenticate(abort.Token, _ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(0, calls);
        Assert.True(result.None);
        Assert.False(result.Succeeded);
        Assert.DoesNotContain(logs, log => log.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Client_cancelling_as_validation_finishes_cannot_receive_authenticated_principal()
    {
        using var abort = new CancellationTokenSource();
        var (result, _) = await Authenticate(abort.Token, _ =>
        {
            abort.Cancel();
            return Task.CompletedTask;
        });

        Assert.True(result.None);
        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Non_cancelled_request_still_runs_session_validation_and_authenticates_valid_session()
    {
        var calls = 0;
        var (result, logs) = await Authenticate(CancellationToken.None, context =>
        {
            calls++;
            Assert.Equal("test-user", context.Principal?.FindFirst("sub")?.Value);
            return Task.CompletedTask;
        });

        Assert.Equal(1, calls);
        Assert.True(result.Succeeded);
        Assert.Equal("test-user", result.Principal?.FindFirst("sub")?.Value);
        Assert.DoesNotContain(logs, log => log.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Session_validation_failure_is_preserved_for_non_cancelled_request()
    {
        var (result, _) = await Authenticate(CancellationToken.None, context =>
        {
            context.Fail("Inactive session.");
            return Task.CompletedTask;
        });

        Assert.False(result.None);
        Assert.False(result.Succeeded);
        Assert.Equal("Inactive session.", result.Failure?.Message);
    }

    [Fact]
    public async Task Invalid_JWT_signature_fails_before_session_callback()
    {
        var calls = 0;
        var (result, _) = await Authenticate(CancellationToken.None, _ =>
        {
            calls++;
            return Task.CompletedTask;
        }, validSignature: false);

        Assert.Equal(0, calls);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    private static async Task<(AuthenticateResult Result, IReadOnlyList<LogEntry> Logs)> Authenticate(
        CancellationToken requestAborted,
        Func<TokenValidatedContext, Task> validateSession,
        bool validSignature = true,
        CapturingLoggerProvider? captureLogs = null)
    {
        const string issuer = "test-only-issuer";
        const string audience = "test-only-audience";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-jwt-cancellation-signing-key-32-bytes-minimum"));
        var logger = captureLogs ?? new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.ClearProviders().AddProvider(logger));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = JwtSessionValidation.HandleRequestCancellation(validateSession)
            };
        });
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, RequestAborted = requestAborted };
        var signingKey = validSignature ? key : new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-only-different-signing-key-32-bytes-minimum"));
        var token = new JwtSecurityToken(issuer, audience, [new Claim("sub", "test-user")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        context.Request.Headers.Authorization = "Bearer " + new JwtSecurityTokenHandler().WriteToken(token);

        var result = await context.AuthenticateAsync();
        return (result, logger.Entries.ToArray());
    }

    private sealed record LogEntry(string Category, LogLevel Level, Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);
        public void Dispose() { }

        private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue(new(category, level, exception));
        }
    }
}
