using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task Notification_feedback_actions_are_role_scoped_and_read_count_is_global_idempotent()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            Guid businessId, approvalId, adminNotice, ownerNotice, customerNotice;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var admin = User("notice-admin@example.test", AccountType.Admin, UserStatus.Active);
                var owner = User("notice-owner@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding);
                var customer = User("notice-customer@example.test", AccountType.Customer, UserStatus.Active);
                db.Users.AddRange(admin, owner, customer);
                var business = new Business { Name = "Notification test", LegalName = "Test", Contact = "Test",
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                businessId = business.Id; db.Businesses.Add(business);
                db.BusinessMemberships.Add(new BusinessMembership { BusinessId = business.Id, UserId = owner.Id, CreatedAt = DateTimeOffset.UtcNow });
                var approval = new ApprovalRequest { BusinessId = business.Id, SubmittedBy = owner.Id, SubmittedAt = DateTimeOffset.UtcNow };
                approvalId = approval.Id; db.ApprovalRequests.Add(approval);
                Notification Add(Guid user, string eventType, string title)
                {
                    var message = new OutboxMessage { EntityId = approval.Id, EventType = eventType,
                        CreatedAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow, ProcessedAt = DateTimeOffset.UtcNow };
                    db.OutboxMessages.Add(message);
                    var notice = new Notification { OutboxMessageId = message.Id, UserId = user, Title = title,
                        Body = "Test message", CreatedAt = DateTimeOffset.UtcNow };
                    db.Notifications.Add(notice); return notice;
                }
                // More unread notices than one page; titles deliberately do not identify actions.
                adminNotice = Add(admin.Id, "APPROVAL_SUBMITTED", "Generic title").Id;
                for (var index = 0; index < 54; index++) Add(admin.Id, "APPROVAL_SUBMITTED", "Generic title");
                ownerNotice = Add(owner.Id, "APPROVAL_CHANGES_REQUESTED", "Generic title").Id;
                customerNotice = Add(customer.Id, "APPROVAL_SUBMITTED", "Có hồ sơ chờ duyệt").Id;
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var adminClient = await Login(anonymous, factory, "notice-admin@example.test", true);
            using var ownerClient = await Login(anonymous, factory, "notice-owner@example.test");
            using var customerClient = await Login(anonymous, factory, "notice-customer@example.test");
            using (var response = await adminClient.GetAsync("/api/v1/me/notifications?limit=2"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
                var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                Assert.Equal(55, payload.GetProperty("unreadCount").GetInt32());
                Assert.Equal(2, payload.GetProperty("data").GetArrayLength());
                Assert.All(payload.GetProperty("data").EnumerateArray(), notice =>
                { Assert.Equal("ADMIN_APPROVAL", notice.GetProperty("action").GetString()); Assert.Equal(JsonValueKind.Null, notice.GetProperty("bookingId").ValueKind); });
                Assert.NotNull(payload.GetProperty("nextCursor").GetString());
            }
            var ownerRows = await Data(await ownerClient.GetAsync("/api/v1/me/notifications"));
            var ownerRow = Assert.Single(ownerRows.EnumerateArray());
            Assert.Equal("PARTNER_PROFILE", ownerRow.GetProperty("action").GetString());
            Assert.Equal(businessId, ownerRow.GetProperty("profileBusinessId").GetGuid());
            await using (var db = context())
            {
                var membership = await db.BusinessMemberships.SingleAsync(m => m.BusinessId == businessId);
                membership.Status = "REVOKED"; await db.SaveChangesAsync();
            }
            var historical = Assert.Single((await Data(await ownerClient.GetAsync("/api/v1/me/notifications"))).EnumerateArray());
            Assert.Equal(JsonValueKind.Null, historical.GetProperty("action").ValueKind);
            Assert.Equal(JsonValueKind.Null, historical.GetProperty("profileBusinessId").ValueKind);
            var customerRows = await Data(await customerClient.GetAsync("/api/v1/me/notifications"));
            Assert.Equal(JsonValueKind.Null, Assert.Single(customerRows.EnumerateArray()).GetProperty("action").ValueKind);
            await Problem(await customerClient.PostAsync($"/api/v1/me/notifications/{adminNotice}/read", null), HttpStatusCode.NotFound, "NOT_FOUND");
            var first = await Data(await adminClient.PostAsync($"/api/v1/me/notifications/{adminNotice}/read", null));
            Assert.Equal(54, first.GetProperty("unreadCount").GetInt32());
            var second = await Data(await adminClient.PostAsync($"/api/v1/me/notifications/{adminNotice}/read", null));
            Assert.Equal(54, second.GetProperty("unreadCount").GetInt32());
            Assert.Equal(first.GetProperty("readAt").GetString(), second.GetProperty("readAt").GetString());
            await using var check = context();
            Assert.Null((await check.Notifications.FindAsync(ownerNotice))!.ReadAt);
            Assert.Null((await check.Notifications.FindAsync(customerNotice))!.ReadAt);
            Assert.Equal("PENDING", (await check.ApprovalRequests.FindAsync(approvalId))!.Status);
        });
    }
}
