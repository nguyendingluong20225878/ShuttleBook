using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Database.Tests;

public sealed class IdentityFlowTests
{
    [Fact]
    public async Task Customer_and_partner_verification_and_session_rotation_use_postgres()
    {
        var supplied = Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(supplied));
        var settings = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.RequireConnectionString(supplied))
        {
            Pooling = false, Timeout = 5, CommandTimeout = 30
        };
        var name = $"shuttlebook_identity_test_{Guid.NewGuid():N}";
        var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
        await using var control = new NpgsqlConnection(settings.ConnectionString);
        await control.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {quoted}", control))
            await command.ExecuteNonQueryAsync();
        try
        {
            settings.Database = name;
            var options = new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(settings.ConnectionString).Options;
            ShuttleBookDbContext Context() => new(options);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:OtpPepper"] = "identity-flow-test-pepper-32-bytes",
                ["Identity:JwtSigningKey"] = "identity-flow-test-jwt-signing-key-32-bytes",
                ["Identity:JwtIssuer"] = "ShuttleBook",
                ["Identity:JwtAudience"] = "ShuttleBook.Web"
            }).Build();
            var delivery = new CapturedDelivery();
            var hasher = new PasswordHasher<User>();
            var clock = TimeProvider.System;
            const string password = "Valid-Password-2026!";
            const string customer = "flow-customer@example.test";
            const string partner = "flow-partner@example.test";

            await using (var db = Context()) await db.Database.MigrateAsync();
            await using (var db = Context())
            {
                var registration = new CustomerRegistrationService(db, hasher, delivery, config, clock,
                    NullLogger<CustomerRegistrationService>.Instance);
                Assert.Equal(RegistrationResult.Accepted, await registration.RegisterAsync(
                    new("email", customer, password, "test-customer-register"), CancellationToken.None));
            }
            var customerCode = delivery.LastCode;
            Assert.NotNull(customerCode);
            await using (var db = Context())
            {
                var registration = new CustomerRegistrationService(db, hasher, delivery, config, clock,
                    NullLogger<CustomerRegistrationService>.Instance);
                Assert.Equal(VerificationResult.Verified, await registration.VerifyAsync(
                    new("email", customer, customerCode!, "test-customer-verify"), CancellationToken.None));
                Assert.Equal(UserStatus.Active, (await db.Users.SingleAsync(user => user.NormalizedEmail == customer)).Status);
            }
            AuthTokens first;
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                Assert.Null(await auth.LoginAsync("email", customer, "wrong", "bad-login", CancellationToken.None));
                first = Assert.IsType<AuthTokens>(await auth.LoginAsync("email", customer, password, "login", CancellationToken.None));
                Assert.Equal("CUSTOMER", first.User.AccountType);
                Assert.Equal(600, first.ExpiresInSeconds);
                Assert.DoesNotContain(customer, first.AccessToken);
                Assert.Single(await db.RefreshSessions.ToListAsync());
            }
            AuthTokens rotated;
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                rotated = Assert.IsType<AuthTokens>(await auth.RefreshAsync(first.RefreshToken, "refresh", CancellationToken.None));
                Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
            }
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                Assert.Null(await auth.RefreshAsync(first.RefreshToken, "reuse", CancellationToken.None));
            }
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                Assert.Null(await auth.RefreshAsync(rotated.RefreshToken, "revoked-child", CancellationToken.None));
            }
            AuthTokens secondFamily;
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                secondFamily = Assert.IsType<AuthTokens>(await auth.LoginAsync(
                    "email", customer, password, "second-login", CancellationToken.None));
            }
            await using var factory = new IdentityApiFactory(settings.ConnectionString);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondFamily.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                Assert.True(await auth.LogoutAsync(secondFamily.User.Id,
                    GetFamilyId(secondFamily.AccessToken), secondFamily.RefreshToken, "logout", CancellationToken.None));
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                Assert.Null(await auth.RefreshAsync(secondFamily.RefreshToken, "after-logout", CancellationToken.None));
                Assert.False(await auth.LogoutAsync(secondFamily.User.Id,
                    GetFamilyId(secondFamily.AccessToken), secondFamily.RefreshToken, "logout-again", CancellationToken.None));
            }

            await using (var db = Context())
            {
                var registration = new PartnerRegistrationService(db, hasher, delivery, config, clock,
                    NullLogger<PartnerRegistrationService>.Instance);
                Assert.Equal(RegistrationResult.Accepted, await registration.RegisterAsync(
                    new("email", partner, password, "test-partner-register"), CancellationToken.None));
            }
            var partnerCode = delivery.LastCode;
            await using (var db = Context())
            {
                var customerRegistration = new CustomerRegistrationService(db, hasher, delivery, config, clock,
                    NullLogger<CustomerRegistrationService>.Instance);
                Assert.Equal(VerificationResult.InvalidCode, await customerRegistration.VerifyAsync(
                    new("email", partner, partnerCode!, "cross-role-verify"), CancellationToken.None));
            }
            await using (var db = Context())
            {
                var partnerRegistration = new PartnerRegistrationService(db, hasher, delivery, config, clock,
                    NullLogger<PartnerRegistrationService>.Instance);
                Assert.Equal(VerificationResult.Verified, await partnerRegistration.VerifyAsync(
                    new("email", partner, partnerCode!, "partner-verify"), CancellationToken.None));
                Assert.Equal(UserStatus.PendingOnboarding,
                    (await db.Users.SingleAsync(user => user.NormalizedEmail == partner)).Status);
            }
            await using (var db = Context())
            {
                var auth = new AuthSessionService(db, hasher, config, clock);
                var tokens = Assert.IsType<AuthTokens>(await auth.LoginAsync("email", partner, password, "partner-login", CancellationToken.None));
                Assert.Equal("VENUE_OPERATOR", tokens.User.AccountType);
                Assert.Equal("PENDING_ONBOARDING", tokens.User.Status);
            }
        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE {quoted} WITH (FORCE)", control);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class CapturedDelivery : IContactVerificationDelivery
    {
        public string? LastCode { get; private set; }
        public Task DeliverAsync(ContactType contactType, string normalizedContact, string code, CancellationToken cancellationToken)
        {
            LastCode = code;
            return Task.CompletedTask;
        }
    }

    private static Guid GetFamilyId(string accessToken)
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(token.Claims.Single(claim => claim.Type == "sid").Value);
    }

    private sealed class IdentityApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ShuttleBook"] = connectionString,
                    ["Identity:OtpPepper"] = "identity-flow-test-pepper-32-bytes",
                    ["Identity:JwtSigningKey"] = "identity-flow-test-jwt-signing-key-32-bytes"
                }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
