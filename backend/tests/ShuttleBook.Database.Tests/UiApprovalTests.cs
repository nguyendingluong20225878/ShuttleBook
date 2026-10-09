using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task UI_admin_paging_preserves_unreviewed_rows_after_decisions_and_binds_filters_and_scope()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var when = DateTimeOffset.UtcNow.AddMinutes(-10);
            Guid[] ids;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var admin = User("ui-page-admin@example.test", AccountType.Admin, UserStatus.Active);
                var owner = User("ui-page-owner@example.test", AccountType.VenueOperator, UserStatus.Active);
                var customer = User("ui-page-customer@example.test", AccountType.Customer, UserStatus.Active);
                db.Users.AddRange(admin, owner, customer);
                var approvals = new List<ApprovalRequest>();
                for (var i = 1; i <= 103; i++)
                {
                    var business = new Business { Name = i == 1 ? "Literal_100% Club" : $"Queue Club {i:D3}",
                        LegalName = "Queue Test Company", Contact = "Test contact", CreatedAt = when, UpdatedAt = when };
                    db.Businesses.Add(business);
                    approvals.Add(new ApprovalRequest { Id = Guid.Parse($"{i:x8}-0000-7000-8000-000000000001"),
                        BusinessId = business.Id, SubmittedBy = owner.Id, Snapshot = "{}", SubmittedAt = when });
                }
                db.ApprovalRequests.AddRange(approvals); await db.SaveChangesAsync(); ids = approvals.Select(a => a.Id).ToArray();
            }
            await using var factory = new OnboardingApiFactory(connection); using var anonymous = factory.CreateClient();
            using var adminClient = await Login(anonymous, factory, "ui-page-admin@example.test", true);
            using var ownerClient = await Login(anonymous, factory, "ui-page-owner@example.test");
            using var customerClient = await Login(anonymous, factory, "ui-page-customer@example.test");
            const string route = "/api/v1/admin/approval-requests/";
            await Problem(await ownerClient.GetAsync(route + "?paged=true"), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await customerClient.GetAsync(route + "?paged=true"), HttpStatusCode.Forbidden, "FORBIDDEN");
            var legacy = await Data(await adminClient.GetAsync(route)); Assert.Equal(100, legacy.GetArrayLength());
            var first = await Data(await adminClient.GetAsync(route + "?paged=true&limit=20"));
            Assert.Equal(103, first.GetProperty("pendingCount").GetInt32()); Assert.Equal(103, first.GetProperty("totalCount").GetInt32());
            Assert.Equal(ids.Take(20), first.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
            var before = first.GetProperty("nextCursor").GetString()!;
            // A decision on already-read rows must not shift and skip the next unseen row as OFFSET would.
            await using (var db = context())
            {
                var row = await db.ApprovalRequests.SingleAsync(x => x.Id == ids[0]);
                row.Status = "CHANGES_REQUESTED"; row.Reason = "Update test profile"; row.ReviewedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
            var seen = new List<Guid>();
            while (before is not null)
            {
                var page = await Data(await adminClient.GetAsync(route + "?paged=true&limit=20&before=" + Uri.EscapeDataString(before)));
                seen.AddRange(page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
                Assert.Equal(102, page.GetProperty("pendingCount").GetInt32());
                before = page.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextCursor").GetString();
            }
            Assert.Equal(ids.Skip(20), seen); Assert.Equal(seen.Count, seen.Distinct().Count());
            var filtered = await Data(await adminClient.GetAsync(route + "?paged=true&q=Queue%20Club%2002&kind=ONBOARDING"));
            Assert.Equal(10, filtered.GetProperty("totalCount").GetInt32()); Assert.Equal(102, filtered.GetProperty("pendingCount").GetInt32());
            var revisions = await Data(await adminClient.GetAsync(route + "?paged=true&kind=VENUE_REVISION"));
            Assert.Equal(0, revisions.GetProperty("totalCount").GetInt32());
            foreach (var query in new[] { "paged=true&limit=0", "paged=true&limit=101", "paged=true&kind=ALL", "paged=true&before=invalid", "paged=true&extra=x", "paged=true&limit=20&limit=30", "paged=true&q=" + new string('x', 121) })
                await Problem(await adminClient.GetAsync(route + "?" + query), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var initialCursor = first.GetProperty("nextCursor").GetString()!;
            await Problem(await adminClient.GetAsync(route + "?paged=true&q=Queue&before=" + Uri.EscapeDataString(initialCursor)), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            // Wildcard text must be literal and not expand to every business.
            var literal = await Data(await adminClient.GetAsync(route + "?paged=true&q=%25"));
            Assert.Empty(literal.GetProperty("items").EnumerateArray());
        });
    }

    [Fact]
    public async Task UI_admin_revision_returns_published_current_without_mutating_it_or_exposing_to_customer()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            Guid approvalId, venueId, oldQr;
            var when = DateTimeOffset.UtcNow;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var admin = User("ui-diff-admin@example.test", AccountType.Admin, UserStatus.Active);
                var owner = User("ui-diff-owner@example.test", AccountType.VenueOperator, UserStatus.Active);
                var customer = User("ui-diff-customer@example.test", AccountType.Customer, UserStatus.Active);
                db.Users.AddRange(admin, owner, customer);
                var business = new Business { Name = "Diff Club", LegalName = "Diff Company", Contact = "Test contact", Status = "ACTIVE", CreatedAt = when, UpdatedAt = when };
                db.Businesses.Add(business);
                var venue = new Venue { BusinessId = business.Id, Name = "Published Venue", Address = "Published address", Contact = "Published contact",
                    Timezone = "Asia/Ho_Chi_Minh", Latitude = 21, Longitude = 105, Status = "PUBLISHED", Version = 7, CreatedAt = when, UpdatedAt = when };
                venueId = venue.Id; db.Venues.Add(venue); await db.SaveChangesAsync();
                var qr = new MediaUpload { OwnerUserId = owner.Id, VenueId = venue.Id, Purpose = "QR", ObjectKey = "ui-diff-test-qr",
                    ContentType = "image/png", SizeBytes = Png.Length, Sha256Base64 = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Png)), Status = "READY", CreatedAt = when };
                oldQr = qr.Id; db.MediaUploads.Add(qr); await db.SaveChangesAsync();
                db.VenuePaymentAccounts.Add(new VenuePaymentAccount { VenueId = venue.Id, BankCode = "OLD", AccountName = "Published Owner", AccountNumber = "000123", QrUploadId = qr.Id });
                var approval = new ApprovalRequest { BusinessId = business.Id, VenueId = venue.Id, Kind = "VENUE_REVISION", SubmittedBy = owner.Id, SubmittedAt = when,
                    Snapshot = JsonSerializer.Serialize(new { version = 7, address = "Proposed address", contact = "Proposed contact", timezone = "Asia/Ho_Chi_Minh", latitude = 21.1, longitude = 105.1,
                        bankCode = "NEW", accountName = "Proposed Owner", accountNumber = "000456", qrUploadId = Guid.NewGuid() }) };
                approvalId = approval.Id; db.ApprovalRequests.Add(approval); await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection); using var anonymous = factory.CreateClient();
            using var adminClient = await Login(anonymous, factory, "ui-diff-admin@example.test", true);
            using var customerClient = await Login(anonymous, factory, "ui-diff-customer@example.test");
            int auditsBeforeRead; await using (var before = context()) auditsBeforeRead = await before.AuditEvents.CountAsync();
            var path = $"/api/v1/admin/approval-requests/{approvalId}";
            await Problem(await customerClient.GetAsync(path), HttpStatusCode.Forbidden, "FORBIDDEN");
            var detail = await Data(await adminClient.GetAsync(path)); var current = detail.GetProperty("current");
            Assert.Equal("Published Venue", current.GetProperty("venueName").GetString()); Assert.Equal(7, current.GetProperty("version").GetInt64());
            Assert.Equal("Published address", current.GetProperty("address").GetString()); Assert.Equal("Published contact", current.GetProperty("contact").GetString());
            Assert.Equal("OLD", current.GetProperty("bankCode").GetString()); Assert.Equal("000123", current.GetProperty("accountNumber").GetString()); Assert.Equal(oldQr, current.GetProperty("qrUploadId").GetGuid());
            using var snapshot = JsonDocument.Parse(detail.GetProperty("snapshot").GetString()!); Assert.Equal("Proposed address", snapshot.RootElement.GetProperty("address").GetString());
            await using var check = context(); Assert.Equal("Published address", (await check.Venues.SingleAsync(x => x.Id == venueId)).Address);
            Assert.Equal("PENDING", (await check.ApprovalRequests.SingleAsync(x => x.Id == approvalId)).Status);
            Assert.Equal(auditsBeforeRead, await check.AuditEvents.CountAsync());
        });
    }
}
