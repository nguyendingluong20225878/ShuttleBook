using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;
using ShuttleBook.Worker;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    private const string Password = "Onboarding-Test-Password-2026!";
    private const string Root = "/api/v1/partner-onboarding";
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Outbox_retries_failed_delivery_and_replay_does_not_duplicate_notification()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            Guid messageId;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var admin = User("retry-admin@example.test", AccountType.Admin, UserStatus.Active);
                db.Users.Add(admin);
                var message = new OutboxMessage { EventType = "UNKNOWN", TargetUserId = admin.Id,
                    EntityId = Guid.CreateVersion7(), Payload = "{}", CreatedAt = DateTimeOffset.UtcNow,
                    NextAttemptAt = DateTimeOffset.UtcNow };
                messageId = message.Id;
                db.OutboxMessages.Add(message);
                await db.SaveChangesAsync();
            }

            using var services = new ServiceCollection().AddDbContext<ShuttleBookDbContext>(
                options => options.UseNpgsql(connection)).BuildServiceProvider();

            async Task RunUntil(Func<OutboxMessage, bool> condition)
            {
                var worker = new ApprovalOutboxWorker(services.GetRequiredService<IServiceScopeFactory>(),
                    TimeProvider.System, new ConfigurationBuilder().Build(),
                    NullLogger<ApprovalOutboxWorker>.Instance);
                await worker.StartAsync(CancellationToken.None);
                try
                {
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        await using var check = context();
                        var current = await check.OutboxMessages.SingleAsync(m => m.Id == messageId);
                        if (condition(current)) return;
                        await Task.Delay(100);
                    }
                    throw new TimeoutException("Outbox worker did not reach the expected state.");
                }
                finally { await worker.StopAsync(CancellationToken.None); worker.Dispose(); }
            }

            await RunUntil(message => message.Attempts > 0);
            await using (var db = context())
            {
                var message = await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
                Assert.Null(message.ProcessedAt);
                Assert.True(message.NextAttemptAt > message.CreatedAt);
                Assert.Empty(await db.Notifications.ToListAsync());
                message.EventType = "APPROVAL_APPROVED";
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }

            await RunUntil(message => message.ProcessedAt is not null);
            await using (var db = context())
            {
                Assert.Equal(1, await db.Notifications.CountAsync());
                var message = await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
                message.ProcessedAt = null;
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }

            await RunUntil(message => message.ProcessedAt is not null);
            await using (var db = context())
                Assert.Equal(1, await db.Notifications.CountAsync());
        });
    }

    [Fact]
    public async Task F02_migrations_preserve_existing_f01_user_and_session()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            Guid userId;
            Guid sessionId;
            await using (var db = context())
            {
                await db.Database.MigrateAsync("20261002000000_F014AdminHardening");
                var owner = User("existing-owner@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding);
                userId = owner.Id;
                db.Users.Add(owner);
                var session = new RefreshSession { UserId = owner.Id, FamilyId = Guid.CreateVersion7(),
                    TokenHash = SHA256.HashData(Guid.NewGuid().ToByteArray()), CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
                sessionId = session.Id;
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO refresh_sessions (id, family_id, user_id, token_hash, created_at, expires_at)
                    VALUES ({session.Id}, {session.FamilyId}, {session.UserId}, {session.TokenHash},
                        {session.CreatedAt}, {session.ExpiresAt})
                    """);
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
            await using (var db = context())
            {
                Assert.Equal(UserStatus.PendingOnboarding, (await db.Users.SingleAsync(u => u.Id == userId)).Status);
                Assert.Equal(userId, (await db.RefreshSessions.SingleAsync(s => s.Id == sessionId)).UserId);
                Assert.Equal(2, await db.Database.SqlQueryRaw<int>(
                    "SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname IN ('postgis', 'btree_gist')").SingleAsync());
            }
        });
    }

    [Fact]
    public async Task Concurrent_business_creation_keeps_one_pending_owner_membership()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                db.Users.Add(User("single-owner@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding));
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "single-owner@example.test");
            var responses = await Task.WhenAll(owner.PostAsJsonAsync($"{Root}/businesses", BusinessBody()),
                owner.PostAsJsonAsync($"{Root}/businesses", BusinessBody()));
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await using var check = context();
            Assert.Equal(1, await check.Businesses.CountAsync());
            Assert.Equal(1, await check.BusinessMemberships.CountAsync());
        });
    }

    [Fact]
    public async Task Concurrent_admin_decisions_commit_only_one_outcome()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                db.Users.AddRange(User("race-owner@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding),
                    User("race-admin@example.test", AccountType.Admin, UserStatus.Active));
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "race-owner@example.test");
            using var admin = await Login(anonymous, factory, "race-admin@example.test", true);
            var businessId = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses", BusinessBody()),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var venueId = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses/{businessId}/venues", VenueBody()),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var courtId = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "Race Court" }),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            await Data(await owner.PutAsJsonAsync($"{Root}/courts/{courtId}/schedule", ScheduleBody()));
            var imageId = await Upload(owner, venueId, "VENUE_IMAGE");
            var qrId = await Upload(owner, venueId, "QR");
            await Data(await owner.PutAsJsonAsync($"{Root}/venues/{venueId}/image", new { uploadId = imageId }));
            await Data(await owner.PutAsJsonAsync($"{Root}/venues/{venueId}/payment-account", new
                { bankCode = "TEST", accountName = "RACE CLUB", accountNumber = "1234567890", qrUploadId = qrId }));
            var approvalId = (await Data(await owner.PostAsync($"{Root}/businesses/{businessId}/submit", null)))
                .GetProperty("id").GetGuid();
            var decisions = await Task.WhenAll(
                admin.PostAsync($"/api/v1/admin/approval-requests/{approvalId}/approve", null),
                admin.PostAsJsonAsync($"/api/v1/admin/approval-requests/{approvalId}/request-changes",
                    new { reason = "Please correct the profile" }));
            Assert.Single(decisions, response => response.StatusCode == HttpStatusCode.OK);
            Assert.True(decisions.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1,
                $"Expected one 409; got {string.Join(",", decisions.Select(response => (int)response.StatusCode))}; " +
                $"server diagnostics: {string.Join(" | ", factory.Diagnostics)}");
            await using var check = context();
            var approval = await check.ApprovalRequests.SingleAsync(a => a.Id == approvalId);
            var business = await check.Businesses.SingleAsync(b => b.Id == businessId);
            Assert.Contains(approval.Status, new[] { "APPROVED", "CHANGES_REQUESTED" });
            Assert.Equal(approval.Status == "APPROVED" ? "ACTIVE" : "DRAFT", business.Status);
            Assert.Equal(1, await check.AuditEvents.CountAsync(a =>
                a.Action == "venue.published" || a.Action == "venue.changes_requested"));
            Assert.Equal(2, await check.OutboxMessages.CountAsync());
        });
    }

    [Fact]
    public async Task Media_rejects_invalid_metadata_and_object_bytes()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                db.Users.Add(User("media-owner@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding));
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            var businessId = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses", BusinessBody()),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var venueId = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses/{businessId}/venues", VenueBody()),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var sha = Convert.ToBase64String(SHA256.HashData(Png));
            await Problem(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new
                { venueId, purpose = "OTHER", contentType = "image/png", sizeBytes = Png.Length, sha256Base64 = sha }),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new
                { venueId, purpose = "QR", contentType = "text/plain", sizeBytes = Png.Length, sha256Base64 = sha }),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new
                { venueId, purpose = "QR", contentType = "image/png", sizeBytes = 5_242_881, sha256Base64 = sha }),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var truncated = Png[..20];
            var data = await Data(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new
            { venueId, purpose = "QR", contentType = "image/png", sizeBytes = truncated.Length,
                sha256Base64 = Convert.ToBase64String(SHA256.HashData(truncated)) }));
            using var content = new ByteArrayContent(truncated);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            await Problem(await owner.PutAsync(data.GetProperty("uploadUrl").GetString(), content),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var uploadId = data.GetProperty("id").GetGuid();
            await Problem(await owner.PostAsync($"/api/v1/uploads/{uploadId}/complete", null),
                HttpStatusCode.Conflict, "UPLOAD_NOT_FOUND");
            await using var check = context();
            Assert.Equal("PENDING", (await check.MediaUploads.SingleAsync(u => u.Id == uploadId)).Status);
        });
    }

    [Fact]
    public async Task Draft_approval_revision_and_scope_use_disposable_postgis()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
                db.Users.AddRange(User("owner-a@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding),
                    User("owner-b@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding),
                    User("admin@example.test", AccountType.Admin, UserStatus.Active),
                    User("customer@example.test", AccountType.Customer, UserStatus.Active));
                await db.SaveChangesAsync();
            }

            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var ownerA = await Login(anonymous, factory, "owner-a@example.test");
            using var ownerB = await Login(anonymous, factory, "owner-b@example.test");
            using var customer = await Login(anonymous, factory, "customer@example.test");
            using var admin = await Login(anonymous, factory, "admin@example.test", true);

            await Problem(await customer.PostAsJsonAsync($"{Root}/businesses", BusinessBody()),
                HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await ownerA.PostAsJsonAsync($"{Root}/businesses", new
                { name = "A Club", legalName = "A Club Company", contact = "Test contact", role = "ADMIN" }),
                HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
            await Problem(await ownerA.PostAsJsonAsync($"{Root}/businesses", new
                { name = "A Club", legalName = "A Club Company", contact = "Test contact", ownerId = Guid.NewGuid() }),
                HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
            await Problem(await ownerA.PostAsJsonAsync($"{Root}/businesses", new
                { name = "A Club", legalName = "A Club Company", contact = "Test contact", status = "ACTIVE" }),
                HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
            var first = await Data(await ownerA.PostAsJsonAsync($"{Root}/businesses", BusinessBody()), HttpStatusCode.Created);
            var businessId = first.GetProperty("id").GetGuid();
            var second = await Data(await ownerB.PostAsJsonAsync($"{Root}/businesses", BusinessBody()), HttpStatusCode.Created);
            var otherBusinessId = second.GetProperty("id").GetGuid();
            await Problem(await ownerA.GetAsync($"{Root}/businesses/{otherBusinessId}"),
                HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await ownerA.PostAsJsonAsync($"{Root}/businesses/{otherBusinessId}/venues", VenueBody()),
                HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null),
                HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            await using (var db = context())
            {
                Assert.Equal(2, await db.BusinessMemberships.CountAsync());
                Assert.All(await db.BusinessMemberships.ToListAsync(), member =>
                {
                    Assert.Equal("OWNER", member.Role); Assert.Equal("PENDING", member.Status);
                });
            }

            var venue = await Data(await ownerA.PostAsJsonAsync($"{Root}/businesses/{businessId}/venues", VenueBody()),
                HttpStatusCode.Created);
            var venueId = venue.GetProperty("id").GetGuid();
            await Problem(await ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null),
                HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            await Problem(await ownerB.PutAsJsonAsync($"{Root}/venues/{venueId}", VenueBody()),
                HttpStatusCode.NotFound, "NOT_FOUND");
            await using (var db = context())
            {
                var coordinate = await db.Database.SqlQueryRaw<string>(
                    "SELECT ST_AsText(location::geometry) AS \"Value\" FROM venues WHERE id = {0}", venueId)
                    .SingleAsync();
                Assert.Equal("POINT(106.7 10.8)", coordinate);
            }
            var court = await Data(await ownerA.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "Sân 1" }),
                HttpStatusCode.Created);
            var courtId = court.GetProperty("id").GetGuid();
            await Problem(await ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null),
                HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            var publicList = await Data(await anonymous.GetAsync("/api/v1/venues"));
            Assert.Empty(publicList.GetProperty("items").EnumerateArray());
            await Problem(await ownerB.PutAsJsonAsync($"{Root}/courts/{courtId}/schedule", ScheduleBody()),
                HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await ownerA.PutAsJsonAsync($"{Root}/courts/{courtId}/schedule", new
                { hours = new[] { new { dayOfWeek = 1, opensAt = "08:15", closesAt = "10:00" } },
                  prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "10:00", pricePerSlot = 100000 } } }),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Data(await ownerA.PutAsJsonAsync($"{Root}/courts/{courtId}/schedule", ScheduleBody()));
            await Problem(await ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null),
                HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");

            var imageId = await Upload(ownerA, venueId, "VENUE_IMAGE");
            var qrId = await Upload(ownerA, venueId, "QR");
            await Problem(await ownerB.GetAsync($"/api/v1/uploads/{imageId}/view"),
                HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await ownerA.PutAsJsonAsync($"{Root}/venues/{venueId}/image", new { uploadId = qrId }),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Data(await ownerA.PutAsJsonAsync($"{Root}/venues/{venueId}/image", new { uploadId = imageId }));
            await Problem(await ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null),
                HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            await Data(await ownerA.PutAsJsonAsync($"{Root}/venues/{venueId}/payment-account", new
                { bankCode = "TEST", accountName = "A CLUB", accountNumber = "1234567890", qrUploadId = qrId }));

            var submissions = await Task.WhenAll(
                ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null),
                ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null));
            Assert.Single(submissions, response => response.StatusCode == HttpStatusCode.OK);
            Assert.True(submissions.Count(response => response.StatusCode == HttpStatusCode.Conflict) == 1,
                $"Expected one 409; got {string.Join(",", submissions.Select(response => (int)response.StatusCode))}; " +
                $"server diagnostics: {string.Join(" | ", factory.Diagnostics)}");
            var submitted = await Data(submissions.Single(response => response.StatusCode == HttpStatusCode.OK));
            var approvalId = submitted.GetProperty("id").GetGuid();
            await Problem(await ownerA.PutAsJsonAsync($"{Root}/courts/{courtId}/schedule", ScheduleBody()),
                HttpStatusCode.Conflict, "STATE_CONFLICT");
            await Problem(await ownerA.PostAsync($"/api/v1/admin/approval-requests/{approvalId}/approve", null),
                HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await customer.PostAsync($"/api/v1/admin/approval-requests/{approvalId}/approve", null),
                HttpStatusCode.Forbidden, "FORBIDDEN");
            var snapshot = (await Data(await admin.GetAsync($"/api/v1/admin/approval-requests/{approvalId}")))
                .GetProperty("snapshot").GetString();
            Assert.Contains("A Club", snapshot);
            await Problem(await admin.PostAsJsonAsync($"/api/v1/admin/approval-requests/{approvalId}/request-changes",
                    new { reason = "short" }), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Data(await admin.PostAsJsonAsync($"/api/v1/admin/approval-requests/{approvalId}/request-changes",
                new { reason = "Please update the profile" }));
            await using (var db = context())
            {
                Assert.Equal("DRAFT", (await db.Businesses.SingleAsync(b => b.Id == businessId)).Status);
                Assert.Equal(snapshot, (await db.ApprovalRequests.SingleAsync(a => a.Id == approvalId)).Snapshot);
            }

            var business = await Data(await ownerA.GetAsync($"{Root}/businesses/{businessId}"));
            using (var update = new HttpRequestMessage(HttpMethod.Put, $"{Root}/businesses/{businessId}")
            {
                Content = JsonContent.Create(new { name = "A Club Updated", legalName = "A Club Company", contact = "Test contact" })
            })
            {
                update.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{business.GetProperty("version").GetInt64()}\""));
                await Data(await ownerA.SendAsync(update));
            }
            var resubmitted = await Data(await ownerA.PostAsync($"{Root}/businesses/{businessId}/submit", null));
            var nextApprovalId = resubmitted.GetProperty("id").GetGuid();
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{nextApprovalId}/approve", null));
            var publishedList = await Data(await anonymous.GetAsync("/api/v1/venues"));
            Assert.Equal(venueId, Assert.Single(publishedList.GetProperty("items").EnumerateArray())
                .GetProperty("id").GetGuid());
            await Problem(await admin.PostAsync($"/api/v1/admin/approval-requests/{nextApprovalId}/approve", null),
                HttpStatusCode.Conflict, "STATE_CONFLICT");
            await using (var db = context())
            {
                Assert.Equal("ACTIVE", (await db.Businesses.SingleAsync(b => b.Id == businessId)).Status);
                Assert.Equal("ACTIVE", (await db.BusinessMemberships.SingleAsync(m => m.BusinessId == businessId)).Status);
                Assert.Equal(UserStatus.Active, (await db.Users.SingleAsync(u => u.NormalizedEmail == "owner-a@example.test")).Status);
                Assert.Equal("PUBLISHED", (await db.Venues.SingleAsync(v => v.Id == venueId)).Status);
                Assert.Equal("ACTIVE", (await db.Courts.SingleAsync(c => c.Id == courtId)).Status);
                Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "venue.published"));
                Assert.Equal(4, await db.OutboxMessages.CountAsync());
            }

            var newQrId = await Upload(ownerA, venueId, "QR");
            var revision = await Data(await ownerA.PostAsJsonAsync($"{Root}/venues/{venueId}/revisions", new
            {
                address = "New address in District One", contact = "New venue contact",
                timezone = "Asia/Ho_Chi_Minh", latitude = 10.9,
                longitude = 106.8, bankCode = "TEST", accountName = "A CLUB", accountNumber = "9876543210",
                qrUploadId = newQrId
            }));
            var revisionId = revision.GetProperty("id").GetGuid();
            await using (var db = context())
            {
                Assert.Equal("Test address in District One", (await db.Venues.SingleAsync(v => v.Id == venueId)).Address);
                Assert.Equal("Test venue contact", (await db.Venues.SingleAsync(v => v.Id == venueId)).Contact);
                Assert.Equal("1234567890", (await db.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venueId)).AccountNumber);
                Assert.Equal(qrId, (await db.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venueId)).QrUploadId);
            }
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{revisionId}/approve", null));
            await using (var db = context())
            {
                Assert.Equal("New address in District One", (await db.Venues.SingleAsync(v => v.Id == venueId)).Address);
                Assert.Equal("New venue contact", (await db.Venues.SingleAsync(v => v.Id == venueId)).Contact);
                Assert.Equal("9876543210", (await db.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venueId)).AccountNumber);
                Assert.Equal(newQrId, (await db.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venueId)).QrUploadId);
            }
            using (var services = new ServiceCollection().AddDbContext<ShuttleBookDbContext>(
                options => options.UseNpgsql(connection)).BuildServiceProvider())
            {
                var worker = new ApprovalOutboxWorker(services.GetRequiredService<IServiceScopeFactory>(),
                    TimeProvider.System, new ConfigurationBuilder().Build(),
                    NullLogger<ApprovalOutboxWorker>.Instance);
                await worker.StartAsync(CancellationToken.None);
                try
                {
                    var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        await using var db = context();
                        if (await db.OutboxMessages.AllAsync(m => m.ProcessedAt != null)) break;
                        await Task.Delay(100);
                    }
                }
                finally { await worker.StopAsync(CancellationToken.None); worker.Dispose(); }
            }
            await using (var db = context())
            {
                Assert.All(await db.OutboxMessages.ToListAsync(), m => Assert.NotNull(m.ProcessedAt));
                Assert.Equal(6, await db.OutboxMessages.CountAsync());
                Assert.Equal(6, await db.Notifications.CountAsync());
                Assert.Equal(6, await db.Notifications.Select(n => new { n.OutboxMessageId, n.UserId }).Distinct().CountAsync());
            }
            var notices = await Data(await ownerA.GetAsync("/api/v1/me/notifications/"));
            Assert.Equal(3, notices.GetArrayLength());
            var noticeId = notices[0].GetProperty("id").GetGuid();
            await Data(await ownerA.PostAsync($"/api/v1/me/notifications/{noticeId}/read", null));
            await Problem(await ownerB.PostAsync($"/api/v1/me/notifications/{noticeId}/read", null),
                HttpStatusCode.NotFound, "NOT_FOUND");
        });
    }

    private static object BusinessBody() => new { name = "A Club", legalName = "A Club Company", contact = "Test contact" };
    private static object VenueBody() => new { name = "Venue One", address = "Test address in District One",
        contact = "Test venue contact",
        timezone = "Asia/Ho_Chi_Minh", latitude = 10.8, longitude = 106.7 };
    private static object ScheduleBody() => new
    {
        hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "10:00" } },
        prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "10:00", pricePerSlot = 100000 } }
    };

    private static User User(string email, AccountType accountType, UserStatus status)
    {
        var user = new User { Email = email, NormalizedEmail = email, AccountType = accountType, Status = status,
            EmailVerifiedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
        return user;
    }
    private static async Task<HttpClient> Login(HttpClient anonymous, OnboardingApiFactory factory, string email, bool admin = false)
    {
        var path = admin ? "/api/v1/admin-auth/login" : "/api/v1/auth/login";
        var data = await Data(await anonymous.PostAsJsonAsync(path,
            new { contactType = "email", contact = email, password = Password }));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<Guid> Upload(HttpClient owner, Guid venueId, string purpose)
    {
        var data = await Data(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new
        { venueId, purpose, contentType = "image/png", sizeBytes = Png.Length,
            sha256Base64 = Convert.ToBase64String(SHA256.HashData(Png)) }));
        var id = data.GetProperty("id").GetGuid();
        using var content = new ByteArrayContent(Png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var put = await owner.PutAsync(data.GetProperty("uploadUrl").GetString(), content);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        await Data(await owner.PostAsync($"/api/v1/uploads/{id}/complete", null));
        await Data(await owner.PostAsync($"/api/v1/uploads/{id}/complete", null));
        return id;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expected, $"Expected {expected}; got {response.StatusCode}: {body}");
            return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
        }
    }
    private static async Task Problem(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expected, $"Expected {expected}; got {response.StatusCode}: {body}");
            Assert.Equal(code, JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
        }
    }
    private static async Task WithDatabaseAsync(Func<string, Func<ShuttleBookDbContext>, Task> run)
    {
        var supplied = Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(supplied));
        var settings = LocalPostgresTestServer.Require(supplied);
        settings.Timeout = 15; settings.CommandTimeout = 30;
        var name = $"shuttlebook_f02_test_{Guid.NewGuid():N}";
        var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
        await using var control = new NpgsqlConnection(settings.ConnectionString);
        await control.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {quoted}", control)) await create.ExecuteNonQueryAsync();
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
    private sealed class OnboardingApiFactory(string connection) : WebApplicationFactory<Program>
    {
        private readonly DiagnosticLoggerProvider diagnostics = new();
        public string[] Diagnostics => diagnostics.Messages;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShuttleBook"] = connection,
                ["Identity:JwtSigningKey"] = "f02-test-jwt-signing-key-more-than-32-bytes",
                ["Identity:OtpPepper"] = "f02-test-otp-pepper-more-than-32-bytes",
                ["RateLimits:AuthVerifyPerIp"] = "1000"
            }));
            builder.ConfigureLogging(log => { log.ClearProviders(); log.AddProvider(diagnostics); });
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

        private sealed class DiagnosticLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<string> messages = new();
            public string[] Messages => messages.ToArray();
            public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(categoryName, messages);
            public void Dispose() { }

            private sealed class DiagnosticLogger(string category, ConcurrentQueue<string> messages) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
                public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
                public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                    if (level >= LogLevel.Warning && category.EndsWith("ProblemDetailsMiddleware", StringComparison.Ordinal))
                        messages.Enqueue(formatter(state, exception));
                }
            }
        }
    }
}
