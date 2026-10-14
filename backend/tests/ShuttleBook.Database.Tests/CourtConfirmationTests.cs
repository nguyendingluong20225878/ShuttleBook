using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task Saving_new_court_schedule_publishes_atomically_only_with_ready_venue_qr()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, venue, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            using var other = await Login(anonymous, factory, "addition-other@example.test");
            var court = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "2" }),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            await Problem(await DraftSchedulePut(other, court, ScheduleBody(), 1), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await DraftSchedulePut(owner, court, ScheduleBody(), 99),
                HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            await using (var db = context())
            {
                (await db.MediaUploads.SingleAsync(m => m.VenueId == venue && m.Purpose == "QR")).Status = "PENDING";
                await db.SaveChangesAsync();
            }
            await Problem(await DraftSchedulePut(owner, court, ScheduleBody(), 1), HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            await using (var db = context())
            {
                Assert.Equal("INACTIVE", (await db.Courts.FindAsync(court))!.Status);
                Assert.Equal(1, (await db.Courts.FindAsync(court))!.Version);
                Assert.Empty(await db.CourtOperatingHours.Where(h => h.CourtId == court).ToListAsync());
                (await db.MediaUploads.SingleAsync(m => m.VenueId == venue && m.Purpose == "QR")).Status = "READY";
                await db.SaveChangesAsync();
            }
            var saved = await Data(await DraftSchedulePut(owner, court, ScheduleBody(), 1));
            Assert.Equal("ACTIVE", saved.GetProperty("status").GetString());
            Assert.Equal(2, saved.GetProperty("version").GetInt64());
            await Problem(await DraftSchedulePut(owner, court, ScheduleBody(), 1), HttpStatusCode.Conflict, "STATE_CONFLICT");
            var publicCourt = (await Data(await anonymous.GetAsync($"/api/v1/venues/{venue}")))
                .GetProperty("courts").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == court);
            Assert.Equal("Sân 2", publicCourt.GetProperty("name").GetString());
            await using var check = context();
            Assert.Equal("ACTIVE", (await check.Courts.FindAsync(court))!.Status);
            Assert.Equal(1, await check.AuditEvents.CountAsync(a => a.EntityId == court && a.Action == "court.auto_published"));
            Assert.Empty(await check.ApprovalRequests.ToListAsync());
        });
    }

    [Fact]
    public async Task Unrelated_pending_business_revision_does_not_block_new_court_publication()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (business, venue, original) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            var court = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "2" }),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            using var revision = VersionedPost($"{Root}/businesses/{business}/revisions",
                new { name = "Later name", legalName = "Later legal", contact = "Later contact" }, 1);
            var request = (await Data(await owner.SendAsync(revision))).GetProperty("id").GetGuid();
            await Data(await DraftSchedulePut(owner, court, ScheduleBody(), 1));
            await using var db = context();
            Assert.Equal("PENDING", (await db.ApprovalRequests.FindAsync(request))!.Status);
            Assert.Equal("ACTIVE", (await db.Courts.FindAsync(court))!.Status);
            Assert.Equal("ACTIVE", (await db.Courts.FindAsync(original))!.Status);
            Assert.Equal("Existing Club", (await db.Businesses.FindAsync(business))!.Name);
            Assert.Equal("PUBLISHED", (await db.Venues.FindAsync(venue))!.Status);
        });
    }

    [Fact]
    public async Task Saving_legacy_pending_court_supersedes_obsolete_approval_without_admin_review()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (business, venue, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            using var admin = await Login(anonymous, factory, "addition-admin@example.test", true);
            var court = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "2" }),
                HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var legacyId = Guid.CreateVersion7(); const string snapshot = "{\"legacy\":\"unchanged\"}";
            Guid submittedBy;
            await using (var db = context())
            {
                submittedBy = (await db.Users.SingleAsync(u => u.NormalizedEmail == "addition-owner@example.test")).Id;
                await db.Database.MigrateAsync("20261012095502_F02OwnerSelfService");
                (await db.Courts.FindAsync(court))!.Status = "PENDING_APPROVAL";
                db.ApprovalRequests.Add(new() { Id = legacyId, BusinessId = business, VenueId = venue, CourtId = court,
                    Kind = "COURT_ADDITION", SubmittedBy = submittedBy, Snapshot = snapshot, SubmittedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync(); await db.Database.MigrateAsync();
            }
            Assert.Empty((await Data(await admin.GetAsync("/api/v1/admin/approval-requests"))).EnumerateArray());
            await Problem(await admin.PostAsync($"/api/v1/admin/approval-requests/{legacyId}/approve", null),
                HttpStatusCode.Conflict, "STATE_CONFLICT");
            await Problem(await DraftSchedulePut(owner, court, ScheduleBody(), 99),
                HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            var saved = await Data(await DraftSchedulePut(owner, court, ScheduleBody(), 1));
            Assert.Equal("ACTIVE", saved.GetProperty("status").GetString());
            await using var check = context();
            var old = (await check.ApprovalRequests.FindAsync(legacyId))!;
            Assert.Equal("SUPERSEDED", old.Status);
            Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
                System.Text.Json.Nodes.JsonNode.Parse(snapshot), System.Text.Json.Nodes.JsonNode.Parse(old.Snapshot)));
            Assert.Equal(submittedBy, old.SubmittedBy); Assert.Null(old.ReviewedBy);
            Assert.Equal(1, await check.ApprovalRequests.CountAsync());
            Assert.Equal(1, await check.AuditEvents.CountAsync(a => a.Action == "approval.court_superseded" && a.EntityId == legacyId));
            Assert.Equal("ACTIVE", (await check.Courts.FindAsync(court))!.Status);
            var error = await Assert.ThrowsAsync<PostgresException>(() => check.Database.MigrateAsync("20261012095502_F02OwnerSelfService"));
            Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        });
    }

    private static HttpRequestMessage VersionedPost(string path, object body, long version)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        return request;
    }

    private static Task<HttpResponseMessage> CourtConfirm(HttpClient owner, Guid court, long? version)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Root}/courts/{court}/confirm");
        if (version.HasValue) request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        return owner.SendAsync(request);
    }
}
