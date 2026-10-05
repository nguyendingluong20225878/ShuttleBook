using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Database.Tests;

public sealed class CustomerRegistrationDbTests
{
    [Fact]
    public async Task Customer_registration_rules_hold_on_postgres()
    {
        var supplied = Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(supplied));
        var settings = LocalPostgresTestServer.Require(supplied);
        settings.Pooling = true;
        settings.Timeout = 15;
        settings.CommandTimeout = 30;
        var name = $"shuttlebook_f011_test_{Guid.NewGuid():N}";
        var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
        await using var control = new NpgsqlConnection(settings.ConnectionString);
        await control.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {quoted}", control)) await create.ExecuteNonQueryAsync();
        try
        {
            settings.Database = name;
            var options = new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(settings.ConnectionString).Options;
            ShuttleBookDbContext Context() => new(options);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:OtpPepper"] = "f011-test-only-pepper-32-bytes-minimum"
            }).Build();
            var clock = new TestClock();
            var delivery = new CapturedDelivery();
            var hasher = new PasswordHasher<User>();
            CustomerRegistrationService Service(ShuttleBookDbContext db) =>
                new(db, hasher, delivery, configuration, clock, NullLogger<CustomerRegistrationService>.Instance);
            const string originalPassword = "F011-original-password-2026!";
            const string replacementPassword = "F011-replacement-password-2026!";
            const string email = "f011-customer@example.test";
            const string phone = "+84901234567";

            await using (var db = Context())
            {
                await db.Database.MigrateAsync("20260924094947_F011CustomerRegistrationInitial");
                var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.Contains(migrations, value => value.Contains("CustomerRegistration", StringComparison.Ordinal));
                await db.Database.MigrateAsync("20260924094947_F011CustomerRegistrationInitial");
                Assert.Equal(migrations, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
                Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
                    "SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname IN ('postgis', 'btree_gist')").SingleAsync());
                Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
                    "SELECT count(*)::int AS \"Value\" FROM pg_indexes WHERE tablename = 'users' AND indexname IN ('ux_users_normalized_email', 'ux_users_normalized_phone')").SingleAsync());
                Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                    "SELECT count(*)::int AS \"Value\" FROM pg_constraint WHERE conname = 'ck_users_exactly_one_contact'").SingleAsync());
                await db.Database.MigrateAsync();
            }

            await using (var db = Context())
            {
                var service = Service(db);
                Assert.Equal(RegistrationResult.ValidationFailed, await service.RegisterAsync(
                    new("phone", "not-a-phone", originalPassword, "invalid-phone"), default));
                Assert.Equal(RegistrationResult.ValidationFailed, await service.RegisterAsync(
                    new("email", "abc@", originalPassword, "invalid-email"), default));
                Assert.Equal(RegistrationResult.ValidationFailed, await service.RegisterAsync(
                    new("email", email, "weak", "invalid-password"), default));
                Assert.Equal(RegistrationResult.Accepted, await service.RegisterAsync(
                    new("email", "  F011-Customer@Example.Test  ", originalPassword, "email-registration"), default));
            }
            var firstCode = delivery.CodeFor(email);
            Assert.Matches("^[0-9]{6}$", firstCode);
            await using (var db = Context())
            {
                var user = await db.Users.SingleAsync();
                Assert.Equal(AccountType.Customer, user.AccountType);
                Assert.Equal(UserStatus.PendingVerification, user.Status);
                Assert.Equal(email, user.NormalizedEmail);
                Assert.Null(user.NormalizedPhone);
                Assert.NotEqual(originalPassword, user.PasswordHash);
                Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, originalPassword));
                var challenge = await db.ContactVerificationChallenges.SingleAsync();
                Assert.Equal(32, challenge.CodeHash.Length);
                Assert.Null(challenge.ConsumedAt);
                Assert.Null(challenge.InvalidatedAt);
                Assert.DoesNotContain(firstCode, Convert.ToHexString(challenge.CodeHash));
                Assert.Equal(1, await db.AuditEvents.CountAsync(audit => audit.Action == "user.registration_started"));
                Assert.All(await db.AuditEvents.ToListAsync(), audit => Assert.Null(audit.Metadata));
            }

            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(
                    new("email", email, replacementPassword, "pending-duplicate"), default));
            var secondCode = delivery.CodeFor(email);
            await using (var db = Context())
            {
                var user = await db.Users.SingleAsync();
                Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, user.PasswordHash, originalPassword));
                Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, replacementPassword));
                Assert.Equal(2, await db.ContactVerificationChallenges.CountAsync());
                Assert.Equal(1, await db.ContactVerificationChallenges.CountAsync(challenge => challenge.InvalidatedAt == null && challenge.ConsumedAt == null));
                Assert.Equal(VerificationResult.InvalidCode, await Service(db).VerifyAsync(new("email", email, firstCode, "old-code"), default));
                Assert.Equal(UserStatus.PendingVerification, user.Status);
                Assert.Equal(VerificationResult.Verified, await Service(db).VerifyAsync(new("email", email, secondCode, "verify"), default));
                Assert.Equal(UserStatus.Active, user.Status);
                Assert.NotNull(user.EmailVerifiedAt);
                Assert.Equal(1, await db.AuditEvents.CountAsync(audit => audit.Action == "user.contact_verified"));
                Assert.Equal(VerificationResult.InvalidCode, await Service(db).VerifyAsync(new("email", email, secondCode, "reuse"), default));
            }
            var deliveriesBeforeActiveDuplicate = delivery.Count;
            await using (var db = Context())
            {
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(
                    new("email", email, replacementPassword, "active-duplicate"), default));
                Assert.Equal(deliveriesBeforeActiveDuplicate, delivery.Count);
                Assert.Equal(1, await db.Users.CountAsync());
            }

            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(
                    new("phone", phone, originalPassword, "phone-registration"), default));
            var phoneCode = delivery.CodeFor(phone);
            await using (var db = Context())
            {
                var user = await db.Users.SingleAsync(value => value.NormalizedPhone == phone);
                Assert.Null(user.Email);
                Assert.Equal(AccountType.Customer, user.AccountType);
                Assert.Equal(UserStatus.PendingVerification, user.Status);
                Assert.Equal(VerificationResult.Verified, await Service(db).VerifyAsync(new("phone", phone, phoneCode, "phone-verify"), default));
                Assert.NotNull(user.PhoneVerifiedAt);
            }

            const string expired = "f011-expired@example.test";
            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(new("email", expired, originalPassword, "expire-register"), default));
            var expiredCode = delivery.CodeFor(expired);
            clock.Advance(TimeSpan.FromMinutes(10));
            await using (var db = Context())
            {
                Assert.Equal(VerificationResult.InvalidCode, await Service(db).VerifyAsync(new("email", expired, expiredCode, "expired"), default));
                Assert.Equal(UserStatus.PendingVerification, (await db.Users.SingleAsync(value => value.NormalizedEmail == expired)).Status);
            }

            const string attempts = "f011-attempts@example.test";
            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(new("email", attempts, originalPassword, "attempt-register"), default));
            var validCode = delivery.CodeFor(attempts);
            var wrongCode = validCode == "000000" ? "000001" : "000000";
            for (var i = 0; i < 5; i++)
            {
                await using var db = Context();
                Assert.Equal(VerificationResult.InvalidCode, await Service(db).VerifyAsync(new("email", attempts, wrongCode, "bad-attempt"), default));
            }
            await using (var db = Context())
            {
                Assert.Equal(VerificationResult.InvalidCode, await Service(db).VerifyAsync(new("email", attempts, validCode, "attempt-limit"), default));
                var attemptUser = await db.Users.SingleAsync(value => value.NormalizedEmail == attempts);
                Assert.Equal(5, (await db.ContactVerificationChallenges.SingleAsync(challenge => challenge.UserId == attemptUser.Id)).AttemptCount);
                Assert.Equal(UserStatus.PendingVerification, (await db.Users.SingleAsync(value => value.NormalizedEmail == attempts)).Status);
            }

            const string resend = "f011-resend@example.test";
            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(new("email", resend, originalPassword, "resend-register"), default));
            var oldResendCode = delivery.CodeFor(resend);
            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).ResendAsync(new("email", resend, "resend"), default));
            var newResendCode = delivery.CodeFor(resend);
            await using (var db = Context())
            {
                Assert.Equal(VerificationResult.InvalidCode, await Service(db).VerifyAsync(new("email", resend, oldResendCode, "resend-old"), default));
                Assert.Equal(VerificationResult.Verified, await Service(db).VerifyAsync(new("email", resend, newResendCode, "resend-new"), default));
            }

            const string racing = "f011-race@example.test";
            await using (var db = Context())
                Assert.Equal(RegistrationResult.Accepted, await Service(db).RegisterAsync(new("email", racing, originalPassword, "race-register"), default));
            var raceCode = delivery.CodeFor(racing);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<VerificationResult> RaceAsync()
            {
                await start.Task;
                await using var db = Context();
                return await Service(db).VerifyAsync(new("email", racing, raceCode, "race-verify"), default);
            }
            var racers = new[] { Task.Run(RaceAsync), Task.Run(RaceAsync) };
            start.SetResult();
            var results = await Task.WhenAll(racers);
            Assert.Equal(1, results.Count(result => result == VerificationResult.Verified));
            Assert.Equal(1, results.Count(result => result == VerificationResult.InvalidCode));
            await using (var db = Context())
            {
                var raceUser = await db.Users.SingleAsync(user => user.NormalizedEmail == racing);
                Assert.Equal(1, await db.AuditEvents.CountAsync(audit => audit.Action == "user.contact_verified" && audit.EntityId == raceUser.Id));
                Assert.Equal(1, await db.ContactVerificationChallenges.CountAsync(challenge => challenge.User.NormalizedEmail == racing && challenge.ConsumedAt != null));
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {quoted} WITH (FORCE)", control);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class CapturedDelivery : IContactVerificationDelivery
    {
        private readonly Dictionary<string, string> codes = [];
        public int Count { get; private set; }
        public string CodeFor(string contact) => codes[contact];
        public Task DeliverAsync(ContactType type, string normalizedContact, string code, CancellationToken cancellationToken)
        {
            codes[normalizedContact] = code;
            Count++;
            return Task.CompletedTask;
        }
    }
}
