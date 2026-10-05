using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Database.Tests;

public sealed class AdminFlowTests
{
    private const string Password = "Admin-Test-Password-2026!";
    private const string NewPassword = "Rotated-Test-Password-2026!";

    [Fact]
    public async Task Bootstrap_login_rotation_and_revocation_use_postgres()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context()) await db.Database.MigrateAsync();
            await using (var db = context())
            {
                var customer = new User
                {
                    AccountType = AccountType.Customer, Status = UserStatus.Active,
                    Email = "existing@example.test", NormalizedEmail = "existing@example.test",
                    EmailVerifiedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                customer.PasswordHash = new PasswordHasher<User>().HashPassword(customer, Password);
                db.Users.Add(customer);
                var operatorUser = new User
                {
                    AccountType = AccountType.VenueOperator, Status = UserStatus.Active,
                    Phone = "+84901234567", NormalizedPhone = "+84901234567",
                    PhoneVerifiedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                operatorUser.PasswordHash = new PasswordHasher<User>().HashPassword(operatorUser, Password);
                db.Users.Add(operatorUser);
                await db.SaveChangesAsync();
                var operations = Operations(db);
                Assert.Equal(AdminOperationResult.ContactInUse, await operations.BootstrapAsync(
                    "email", " Existing@Example.Test ", Password, "collision", CancellationToken.None));
                Assert.Equal(AdminOperationResult.ContactInUse, await operations.BootstrapAsync(
                    "phone", "+84901234567", Password, "operator-collision", CancellationToken.None));
                Assert.Equal(AdminOperationResult.InvalidInput, await operations.BootstrapAsync(
                    "email", "bad", "weak", "invalid", CancellationToken.None));
                Assert.Empty(await db.Users.Where(user => user.AccountType == AccountType.Admin).ToListAsync());
            }
            Guid adminId;
            await using (var db = context())
            {
                var operations = Operations(db);
                Assert.Equal(AdminOperationResult.Applied, await operations.BootstrapAsync(
                    "email", " ADMIN@Example.Test ", Password, "bootstrap", CancellationToken.None));
                var admin = await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin);
                adminId = admin.Id;
                Assert.Equal(UserStatus.Active, admin.Status);
                Assert.Equal("admin@example.test", admin.NormalizedEmail);
                Assert.NotNull(admin.EmailVerifiedAt);
                Assert.NotEqual(Password, admin.PasswordHash);
                Assert.NotEqual(PasswordVerificationResult.Failed,
                    new PasswordHasher<User>().VerifyHashedPassword(admin, admin.PasswordHash, Password));
                Assert.Empty(await db.ContactVerificationChallenges.ToListAsync());
                Assert.Single(await db.AuditEvents.Where(item => item.Action == "admin.bootstrap_succeeded").ToListAsync());
                Assert.Equal(AdminOperationResult.AlreadyExists, await operations.BootstrapAsync(
                    "email", "other@example.test", NewPassword, "repeat", CancellationToken.None));
                Assert.Equal(adminId, (await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin)).Id);
            }
            await using (var db = context())
            {
                var second = new User
                {
                    AccountType = AccountType.Admin, Status = UserStatus.Active,
                    Email = "second@example.test", NormalizedEmail = "second@example.test",
                    EmailVerifiedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow, PasswordHash = "test-hash"
                };
                db.Users.Add(second);
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.IsType<PostgresException>(error.InnerException);
            }

            await using var factory = new AdminApiFactory(connection);
            using var client = factory.CreateClient();
            using (var injection = await client.PostAsJsonAsync("/api/v1/admin-auth/login", new
                   { contactType = "email", contact = "admin@example.test", password = Password, accountType = "ADMIN" }))
            {
                Assert.Equal(HttpStatusCode.BadRequest, injection.StatusCode);
                Assert.Equal("UNSUPPORTED_FIELD", await CodeAsync(injection));
            }
            using (var invalid = await client.PostAsJsonAsync("/api/v1/admin-auth/login", new
                   { contactType = "email", contact = "bad", password = Password }))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
                Assert.Equal("VALIDATION_FAILED", await CodeAsync(invalid));
            }
            using (var anonymous = await client.GetAsync("/api/v1/admin-auth/me"))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
                Assert.Equal("UNAUTHORIZED", await CodeAsync(anonymous));
            }
            var customerLogin = await ReadTokensAsync(await LoginAsync(client, "/api/v1/auth/login", "existing@example.test", Password));
            using (var forbidden = await MeAsync(client, customerLogin.AccessToken))
            {
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
                Assert.Equal("FORBIDDEN", await CodeAsync(forbidden));
            }
            var login = await LoginAsync(client, "/api/v1/admin-auth/login", "admin@example.test", Password);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.True(login.Headers.CacheControl?.NoStore);
            var first = await ReadTokensAsync(login);
            Assert.Equal("ADMIN", first.AccountType);
            Assert.Equal("ACTIVE", first.Status);
            await using (var db = context())
                Assert.Equal(AdminOperationResult.AlreadyExists, await Operations(db).BootstrapAsync(
                    "email", "ignored-after-login@example.test", NewPassword, "repeat-with-session", CancellationToken.None));
            Assert.Equal(HttpStatusCode.OK, (await MeAsync(client, first.AccessToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await LoginAsync(client, "/api/v1/auth/login", "admin@example.test", Password)).StatusCode);
            await AssertRejectedAdminLoginAsync(client, "admin@example.test", "wrong");
            await AssertRejectedAdminLoginAsync(client, "existing@example.test", Password);
            await AssertRejectedAdminLoginAsync(client, "unknown@example.test", Password);

            using (var me = await MeAsync(client, first.AccessToken))
            {
                Assert.Equal(HttpStatusCode.OK, me.StatusCode);
                var response = await me.Content.ReadAsStringAsync();
                Assert.DoesNotContain("admin@example.test", response);
            }
            var rotatedSessionResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
            Assert.Equal(HttpStatusCode.OK, rotatedSessionResponse.StatusCode);
            Assert.True(rotatedSessionResponse.Headers.CacheControl?.NoStore);
            var rotatedSession = await ReadTokensAsync(rotatedSessionResponse);
            Assert.NotEqual(first.RefreshToken, rotatedSession.RefreshToken);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, rotatedSession.AccessToken)).StatusCode);

            var active = await ReadTokensAsync(await LoginAsync(client, "/api/v1/admin-auth/login", "admin@example.test", Password));
            await using (var db = context())
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).RotateAsync(NewPassword, "rotate", CancellationToken.None));
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, active.AccessToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = active.RefreshToken })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await LoginAsync(client, "/api/v1/admin-auth/login", "admin@example.test", Password)).StatusCode);
            var afterRotate = await ReadTokensAsync(await LoginAsync(client, "/api/v1/admin-auth/login", "admin@example.test", NewPassword));
            await using (var db = context())
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).SuspendAsync("suspend", CancellationToken.None));
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, afterRotate.AccessToken)).StatusCode);
            await AssertRejectedAdminLoginAsync(client, "admin@example.test", NewPassword);
            await using (var db = context())
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).ActivateAsync("activate", CancellationToken.None));
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, afterRotate.AccessToken)).StatusCode);
            var afterActivate = await ReadTokensAsync(await LoginAsync(client, "/api/v1/admin-auth/login", "admin@example.test", NewPassword));
            var secondAfterActivate = await ReadTokensAsync(await LoginAsync(client, "/api/v1/admin-auth/login", "admin@example.test", NewPassword));
            await using (var db = context())
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).RevokeSessionsAsync("revoke", CancellationToken.None));
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, afterActivate.AccessToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, secondAfterActivate.AccessToken)).StatusCode);
            await using (var db = context())
            {
                Assert.Equal(1, await db.Users.CountAsync(user => user.AccountType == AccountType.Admin));
                Assert.DoesNotContain(Password, string.Join(' ', (await db.AuditEvents.ToListAsync())
                    .Select(item => item.Metadata ?? "")));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.Action == "admin.password_rotated"));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.Action == "admin.suspended"));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.Action == "admin.activated"));
                Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.Action == "admin.sessions_revoked"));
                Assert.All(await db.RefreshSessions.Where(item => item.UserId == adminId).ToListAsync(),
                    item => Assert.NotNull(item.RevokedAt));
            }
        });
    }

    [Fact]
    public async Task Concurrent_bootstrap_creates_one_admin_and_migration_is_repeatable()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            await using (var db = context()) await db.Database.MigrateAsync();
            async Task<AdminOperationResult> Bootstrap(string contact)
            {
                await using var db = context();
                return await Operations(db).BootstrapAsync("email", contact, Password, "race", CancellationToken.None);
            }
            var results = await Task.WhenAll(Bootstrap("first@example.test"), Bootstrap("second@example.test"));
            Assert.Single(results, result => result == AdminOperationResult.Applied);
            Assert.Single(results, result => result == AdminOperationResult.AlreadyExists);
            await using var check = context();
            Assert.Equal(1, await check.Users.CountAsync(user => user.AccountType == AccountType.Admin));
            Assert.Equal(1, await check.AuditEvents.CountAsync(item => item.Action == "admin.bootstrap_succeeded"));
            var applied = (await check.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Contains(applied, item => item.Contains("F014AdminHardening", StringComparison.Ordinal));
            await check.Database.MigrateAsync();
            Assert.Equal(applied, (await check.Database.GetAppliedMigrationsAsync()).ToArray());
        });
    }

    [Fact]
    public async Task Concurrent_admin_login_refresh_and_revoke_leave_no_old_family_active()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).BootstrapAsync(
                    "email", "race-admin@example.test", Password, "setup", CancellationToken.None));
            }
            async Task<AuthTokens?> OldLogin()
            {
                await using var db = context();
                return await Auth(db).AdminLoginAsync("email", "race-admin@example.test", Password,
                    "old-login", CancellationToken.None);
            }
            async Task<AdminOperationResult> Rotate()
            {
                await using var db = context();
                return await Operations(db).RotateAsync(NewPassword, "rotate-race", CancellationToken.None);
            }
            var logins = Enumerable.Range(0, 3).Select(_ => OldLogin()).ToArray();
            var rotate = Rotate();
            var issued = await Task.WhenAll(logins);
            Assert.Equal(AdminOperationResult.Applied, await rotate);
            await using (var db = context())
            {
                var admin = await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin);
                Assert.False(await db.RefreshSessions.AnyAsync(session => session.UserId == admin.Id && session.RevokedAt == null));
                Assert.NotEqual(PasswordVerificationResult.Failed,
                    new PasswordHasher<User>().VerifyHashedPassword(admin, admin.PasswordHash, NewPassword));
            }
            foreach (var token in issued.Where(token => token is not null))
            {
                await using var db = context();
                Assert.Null(await Auth(db).RefreshAsync(token!.RefreshToken, "old-refresh", CancellationToken.None));
            }
            AuthTokens active;
            await using (var db = context())
                active = Assert.IsType<AuthTokens>(await Auth(db).AdminLoginAsync(
                    "email", "race-admin@example.test", NewPassword, "new-login", CancellationToken.None));
            async Task<AuthTokens?> Refresh()
            {
                await using var db = context();
                return await Auth(db).RefreshAsync(active.RefreshToken, "refresh-race", CancellationToken.None);
            }
            async Task<AdminOperationResult> Revoke()
            {
                await using var db = context();
                return await Operations(db).RevokeSessionsAsync("revoke-race", CancellationToken.None);
            }
            var refresh = Refresh();
            var revoke = Revoke();
            await Task.WhenAll(refresh, revoke);
            Assert.Equal(AdminOperationResult.Applied, await revoke);
            await using (var db = context())
            {
                var admin = await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin);
                Assert.False(await db.RefreshSessions.AnyAsync(session => session.UserId == admin.Id && session.RevokedAt == null));
            }
        });
    }

    [Fact]
    public async Task Admin_lock_orders_login_and_refresh_after_credential_and_session_changes()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).BootstrapAsync(
                    "email", "locked-admin@example.test", Password, "setup", CancellationToken.None));
            }
            await using (var holder = context())
            {
                await using var transaction = await holder.Database.BeginTransactionAsync();
                await AdminOperations.AcquireLockAsync(holder, CancellationToken.None);
                async Task<AuthTokens?> Login()
                {
                    await using var db = context();
                    return await Auth(db).AdminLoginAsync("email", "locked-admin@example.test", Password,
                        "blocked-login", CancellationToken.None);
                }
                var login = Login();
                await WaitForAdvisoryWaiterAsync(connection);
                var admin = await holder.Users.SingleAsync(user => user.AccountType == AccountType.Admin);
                admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, NewPassword);
                await holder.SaveChangesAsync();
                await transaction.CommitAsync();
                Assert.Null(await login);
            }
            AuthTokens current;
            await using (var db = context())
                current = Assert.IsType<AuthTokens>(await Auth(db).AdminLoginAsync(
                    "email", "locked-admin@example.test", NewPassword, "new-login", CancellationToken.None));
            await using (var holder = context())
            {
                await using var transaction = await holder.Database.BeginTransactionAsync();
                await AdminOperations.AcquireLockAsync(holder, CancellationToken.None);
                async Task<AuthTokens?> Refresh()
                {
                    await using var db = context();
                    return await Auth(db).RefreshAsync(current.RefreshToken, "blocked-refresh", CancellationToken.None);
                }
                var refresh = Refresh();
                await WaitForAdvisoryWaiterAsync(connection);
                var adminId = await holder.Users.Where(user => user.AccountType == AccountType.Admin)
                    .Select(user => user.Id).SingleAsync();
                await holder.RefreshSessions.Where(session => session.UserId == adminId && session.RevokedAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, DateTimeOffset.UtcNow));
                await transaction.CommitAsync();
                Assert.Null(await refresh);
            }
        });
    }

    [Fact]
    public async Task Upgrade_preserves_f01_data_and_failed_admin_audit_rolls_back_rotation()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            Guid customerId;
            Guid familyId;
            await using (var db = context())
            {
                await db.Database.MigrateAsync("20260924102728_F012AuthSessions");
                var customer = new User
                {
                    AccountType = AccountType.Customer, Status = UserStatus.Active,
                    Email = "upgrade@example.test", NormalizedEmail = "upgrade@example.test",
                    EmailVerifiedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow, PasswordHash = "test-hash"
                };
                db.Users.Add(customer);
                customerId = customer.Id;
                familyId = Guid.CreateVersion7();
                db.RefreshSessions.Add(new RefreshSession
                {
                    User = customer, FamilyId = familyId, TokenHash = System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes("upgrade-token")),
                    CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
                });
                await db.SaveChangesAsync();
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
            }
            await using (var db = context())
            {
                Assert.Equal(customerId, (await db.Users.SingleAsync(user => user.NormalizedEmail == "upgrade@example.test")).Id);
                Assert.True(await db.RefreshSessions.AnyAsync(session => session.UserId == customerId && session.FamilyId == familyId));
                var invalidAdmin = new User
                {
                    AccountType = AccountType.Admin, Status = UserStatus.PendingVerification,
                    Email = "invalid-admin@example.test", NormalizedEmail = "invalid-admin@example.test",
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, PasswordHash = "test-hash"
                };
                db.Users.Add(invalidAdmin);
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }
            await using (var db = context())
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).BootstrapAsync(
                    "email", "upgrade-admin@example.test", Password, "bootstrap", CancellationToken.None));
            string hashBefore;
            await using (var db = context())
            {
                hashBefore = (await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin)).PasswordHash;
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_events ADD CONSTRAINT ck_test_reject_rotation CHECK (action <> 'admin.password_rotated')");
            }
            await using (var db = context())
                await Assert.ThrowsAsync<DbUpdateException>(() => Operations(db).RotateAsync(
                    NewPassword, "forced-failure", CancellationToken.None));
            await using (var db = context())
            {
                Assert.Equal(hashBefore, (await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin)).PasswordHash);
                Assert.Equal(0, await db.AuditEvents.CountAsync(item => item.Action == "admin.password_rotated"));
            }
        });
    }

    [Fact]
    public async Task Migration_rejects_two_existing_admins_without_changing_them()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync("20260924102728_F012AuthSessions");
                foreach (var contact in new[] { "first-preupgrade@example.test", "second-preupgrade@example.test" })
                {
                    var admin = new User
                    {
                        AccountType = AccountType.Admin, Status = UserStatus.Active,
                        Email = contact, NormalizedEmail = contact,
                        EmailVerifiedAt = DateTimeOffset.UtcNow,
                        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
                    };
                    admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, Password);
                    db.Users.Add(admin);
                }
                await db.SaveChangesAsync();
            }
            await using (var db = context())
            {
                var failure = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("23505", failure.ToString());
            }
            await using (var check = context())
            {
                Assert.Equal(2, await check.Users.CountAsync(user => user.AccountType == AccountType.Admin));
                Assert.DoesNotContain(await check.Database.GetAppliedMigrationsAsync(),
                    migration => migration.Contains("F014AdminHardening", StringComparison.Ordinal));
            }
        });
    }

    [Fact]
    public async Task Admin_cli_reads_secret_from_stdin_without_argv_and_is_idempotent()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context()) await db.Database.MigrateAsync();
            var contact = $"cli-{Guid.NewGuid():N}@example.test";
            var password = $"Cli-{Guid.NewGuid():N}!Aa1";
            var rotatedPassword = $"Rotated-{Guid.NewGuid():N}!Aa1";
            var invalid = await RunCliAsync(connection, "bootstrap", "email", "invalid-contact", "weak");
            Assert.NotEqual(0, invalid.ExitCode);
            var tooLong = await RunCliAsync(connection, "bootstrap", "email", contact, new string('x', 129));
            Assert.NotEqual(0, tooLong.ExitCode);
            var incomplete = await RunCliAsync(connection, "bootstrap", "email", contact);
            Assert.NotEqual(0, incomplete.ExitCode);
            foreach (var operation in new[] { "rotate", "suspend", "activate", "revoke-sessions" })
            {
                var result = operation == "rotate"
                    ? await RunCliAsync(connection, operation, rotatedPassword)
                    : await RunCliAsync(connection, operation);
                Assert.NotEqual(0, result.ExitCode);
            }
            await using (var empty = context())
            {
                Assert.Empty(await empty.Users.Where(user => user.AccountType == AccountType.Admin).ToListAsync());
                Assert.Empty(await empty.AuditEvents.Where(item => item.Action.StartsWith("admin.")).ToListAsync());
            }
            var first = await RunCliAsync(connection, "bootstrap", "email", contact, password);
            Assert.Equal(0, first.ExitCode);
            Assert.DoesNotContain(contact, first.Output);
            Assert.DoesNotContain(password, first.Output);
            string firstHash;
            await using (var db = context())
            {
                var admin = await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin);
                firstHash = admin.PasswordHash;
                Assert.NotEqual(password, firstHash);
            }
            var repeat = await RunCliAsync(connection, "bootstrap", "email", "other@example.test", rotatedPassword);
            Assert.Equal(0, repeat.ExitCode);
            await using (var db = context())
                Assert.Equal(firstHash, (await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin)).PasswordHash);
            var rotate = await RunCliAsync(connection, "rotate", rotatedPassword);
            Assert.Equal(0, rotate.ExitCode);
            Assert.DoesNotContain(rotatedPassword, rotate.Output);
            await using (var db = context())
            {
                var admin = await db.Users.SingleAsync(user => user.AccountType == AccountType.Admin);
                Assert.NotEqual(firstHash, admin.PasswordHash);
                Assert.NotEqual(PasswordVerificationResult.Failed,
                    new PasswordHasher<User>().VerifyHashedPassword(admin, admin.PasswordHash, rotatedPassword));
            }
            var missingConfiguration = await RunCliAsync(null, "bootstrap");
            Assert.NotEqual(0, missingConfiguration.ExitCode);
            Assert.DoesNotContain(contact, missingConfiguration.Output);
            await using (var db = context())
                Assert.Equal(1, await db.Users.CountAsync(user => user.AccountType == AccountType.Admin));
        });
    }

    [Fact]
    public async Task Two_admin_cli_processes_bootstrap_only_one_account()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context()) await db.Database.MigrateAsync();
            var firstContact = $"cli-race-a-{Guid.NewGuid():N}@example.test";
            var secondContact = $"cli-race-b-{Guid.NewGuid():N}@example.test";
            var firstPassword = $"Cli-race-{Guid.NewGuid():N}!Aa1";
            var secondPassword = $"Cli-race-{Guid.NewGuid():N}!Aa1";
            var results = await Task.WhenAll(
                RunCliAsync(connection, "bootstrap", "email", firstContact, firstPassword),
                RunCliAsync(connection, "bootstrap", "email", secondContact, secondPassword));
            Assert.All(results, result => Assert.Equal(0, result.ExitCode));
            Assert.All(results, result =>
            {
                Assert.DoesNotContain(firstContact, result.Output);
                Assert.DoesNotContain(secondContact, result.Output);
                Assert.DoesNotContain(firstPassword, result.Output);
                Assert.DoesNotContain(secondPassword, result.Output);
            });
            await using var check = context();
            Assert.Equal(1, await check.Users.CountAsync(user => user.AccountType == AccountType.Admin));
            Assert.Equal(1, await check.AuditEvents.CountAsync(item => item.Action == "admin.bootstrap_succeeded"));
        });
    }

    [Fact]
    public async Task Concurrent_admin_refresh_reuse_and_logout_keep_families_isolated()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                Assert.Equal(AdminOperationResult.Applied, await Operations(db).BootstrapAsync(
                    "email", "admin-families@example.test", Password, "setup", CancellationToken.None));
            }
            await using var factory = new AdminApiFactory(connection);
            using var client = factory.CreateClient();
            var first = await ReadTokensAsync(await LoginAsync(client, "/api/v1/admin-auth/login",
                "admin-families@example.test", Password));
            var second = await ReadTokensAsync(await LoginAsync(client, "/api/v1/admin-auth/login",
                "admin-families@example.test", Password));
            var concurrent = await Task.WhenAll(
                client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }),
                client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }));
            Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(concurrent, response => response.StatusCode == HttpStatusCode.Unauthorized);
            var replacement = await ReadTokensAsync(concurrent.Single(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, replacement.AccessToken)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await MeAsync(client, second.AccessToken)).StatusCode);
            using (var wrongFamily = await LogoutAsync(client, second.AccessToken, first.RefreshToken))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, wrongFamily.StatusCode);
                Assert.Equal("INVALID_REFRESH_TOKEN", await CodeAsync(wrongFamily));
            }
            Assert.Equal(HttpStatusCode.OK, (await MeAsync(client, second.AccessToken)).StatusCode);
            using (var logout = await LogoutAsync(client, second.AccessToken, second.RefreshToken))
                Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            using (var repeated = await LogoutAsync(client, second.AccessToken, second.RefreshToken))
                Assert.Equal(HttpStatusCode.Unauthorized, repeated.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(client, second.AccessToken)).StatusCode);
            await using var check = context();
            Assert.False(await check.RefreshSessions.AnyAsync(item => item.RevokedAt == null));
        });
    }

    private static AdminOperations Operations(ShuttleBookDbContext db) =>
        new(db, new PasswordHasher<User>(), TimeProvider.System);

    private static AuthSessionService Auth(ShuttleBookDbContext db) => new(db, new PasswordHasher<User>(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:JwtSigningKey"] = "admin-flow-test-jwt-signing-key-32-bytes",
            ["Identity:JwtIssuer"] = "ShuttleBook",
            ["Identity:JwtAudience"] = "ShuttleBook.Web"
        }).Build(), TimeProvider.System);

    private static async Task WaitForAdvisoryWaiterAsync(string connection)
    {
        await using var observer = new NpgsqlConnection(connection);
        await observer.OpenAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event = 'advisory'", observer);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return;
            await Task.Delay(50);
        }
        Assert.Fail("An Admin operation did not wait on the PostgreSQL advisory lock.");
    }

    private sealed record CliResult(int ExitCode, string Output);
    private static async Task<CliResult> RunCliAsync(string? connection, string operation, params string[] input)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var backendRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var cli = Path.Combine(backendRoot, "src", "ShuttleBook.AdminCli", "bin", configuration,
            "net10.0", "ShuttleBook.AdminCli.dll");
        Assert.True(File.Exists(cli), "Build ShuttleBook.slnx before database tests so the Admin CLI is available.");
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        var dotnet = string.IsNullOrWhiteSpace(dotnetRoot) ? "dotnet" : Path.Combine(dotnetRoot,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var start = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(cli);
        start.ArgumentList.Add(operation);
        if (connection is null) start.Environment.Remove("ConnectionStrings__ShuttleBook");
        else start.Environment["ConnectionStrings__ShuttleBook"] = connection;
        using var process = Process.Start(start)!;
        foreach (var line in input) await process.StandardInput.WriteLineAsync(line);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, await stdout + await stderr);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string path, string contact, string password) =>
        client.PostAsJsonAsync(path, new { contactType = "email", contact, password });

    private static async Task AssertRejectedAdminLoginAsync(HttpClient client, string contact, string password)
    {
        using var response = await LoginAsync(client, "/api/v1/admin-auth/login", contact, password);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", await CodeAsync(response));
    }

    private static Task<HttpResponseMessage> MeAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin-auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string token, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { refreshToken });
        return await client.SendAsync(request);
    }

    private sealed record Tokens(string AccessToken, string RefreshToken, string AccountType, string Status);
    private static async Task<string> CodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString()!;
    }
    private static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        return new Tokens(data.GetProperty("accessToken").GetString()!, data.GetProperty("refreshToken").GetString()!,
            data.GetProperty("user").GetProperty("accountType").GetString()!,
            data.GetProperty("user").GetProperty("status").GetString()!);
    }

    private static async Task WithDatabaseAsync(Func<string, Func<ShuttleBookDbContext>, Task> run)
    {
        var supplied = Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(supplied));
        var settings = LocalPostgresTestServer.Require(supplied);
        settings.Pooling = true;
        settings.Timeout = 15;
        settings.CommandTimeout = 30;
        var name = $"shuttlebook_admin_test_{Guid.NewGuid():N}";
        var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
        await using var control = new NpgsqlConnection(settings.ConnectionString);
        await control.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {quoted}", control))
            await create.ExecuteNonQueryAsync();
        try
        {
            settings.Database = name;
            var connection = settings.ConnectionString;
            var options = new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(connection).Options;
            ShuttleBookDbContext Context() => new(options);
            await run(connection, Context);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {quoted} WITH (FORCE)", control);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class AdminApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ShuttleBook"] = connectionString,
                    ["Identity:JwtSigningKey"] = "admin-flow-test-jwt-signing-key-32-bytes",
                    ["Identity:JwtIssuer"] = "ShuttleBook",
                    ["Identity:JwtAudience"] = "ShuttleBook.Web",
                    ["RateLimits:AuthVerifyPerIp"] = "1000"
                }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
