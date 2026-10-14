using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task Active_court_name_and_price_changes_share_one_version_under_concurrency()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, _, courtId) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            using var name = VersionedPut($"{Root}/courts/{courtId}", new { name = "2" }, 1);
            using var price = VersionedPut($"/api/v1/operator/courts/{courtId}/schedule", ScheduleBody(), 1);
            var results = await Task.WhenAll(owner.SendAsync(name), owner.SendAsync(price));
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.PreconditionFailed);
            foreach (var result in results) result.Dispose();
            await using var db = context();
            Assert.Equal(2, (await db.Courts.FindAsync(courtId))!.Version);
            Assert.Empty(await db.ApprovalRequests.ToListAsync());
        });
    }

    [Fact]
    public async Task Owner_publishes_court_by_schedule_and_admin_only_publishes_new_venues()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (businessId, venueId, oldCourtId) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            using var admin = await Login(anon, factory, "addition-admin@example.test", true);
            using var customer = await Login(anon, factory, "addition-customer@example.test");
            await Data(await MediaMutation(owner, $"venues/{venueId}", 1, null));
            var courtId = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "1" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").Date).AddDays(1);
            while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
            var quote = new { courtId, date = date.ToString("yyyy-MM-dd"), startsAt = "08:00", endsAt = "10:00" };
            await Problem(await customer.PostAsJsonAsync("/api/v1/availability/quote", quote), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await anon.GetAsync($"/api/v1/venues/{venueId}/availability?date={quote.date}&courtId={courtId}"), HttpStatusCode.NotFound, "NOT_FOUND");
            var published = await Data(await DraftSchedulePut(owner, courtId, ScheduleBody(), 1));
            Assert.Equal("ACTIVE", published.GetProperty("status").GetString());
            Assert.Equal(2, (await Data(await anon.GetAsync($"/api/v1/venues/{venueId}"))).GetProperty("courts").GetArrayLength());
            var extra = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "3" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var allowedQuote = await Data(await customer.PostAsJsonAsync("/api/v1/availability/quote", quote));
            Assert.Equal(courtId, allowedQuote.GetProperty("courtId").GetGuid());
            Assert.NotEqual(Guid.Empty, allowedQuote.GetProperty("quoteId").GetGuid());
            await using (var db = context())
            {
                Assert.Equal("ACTIVE", (await db.Businesses.FindAsync(businessId))!.Status);
                Assert.Equal("ACTIVE", (await db.Courts.FindAsync(oldCourtId))!.Status);
                Assert.Equal("ACTIVE", (await db.Courts.FindAsync(courtId))!.Status);
                Assert.Equal("INACTIVE", (await db.Courts.FindAsync(extra))!.Status);
                Assert.Empty(await db.ApprovalRequests.ToListAsync());
                Assert.Empty(await db.OutboxMessages.Where(m => m.EventType == "APPROVAL_SUBMITTED").ToListAsync());
                Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.EntityId == courtId && a.Action == "court.auto_published"));
            }
            var newVenue = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses/{businessId}/venues", VenueBody()), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var newCourt = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{newVenue}/courts", new { name = "1" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            await Data(await DraftSchedulePut(owner, newCourt, ScheduleBody(), 1));
            await Problem(await CourtConfirm(owner, newCourt, 2), HttpStatusCode.Conflict, "STATE_CONFLICT");
            var qr = await Upload(owner, newVenue, "QR");
            await Data(await owner.PutAsJsonAsync($"{Root}/venues/{newVenue}/payment-account", new { bankCode = "TEST", accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = qr }));
            var approval = (await Data(await owner.PostAsync($"{Root}/venues/{newVenue}/submit", null))).GetProperty("id").GetGuid();
            await Problem(await anon.GetAsync($"/api/v1/venues/{newVenue}"), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await CourtConfirm(owner, newCourt, 2), HttpStatusCode.Conflict, "STATE_CONFLICT");
            // Stored venue approvals with a removed legacy court image field are still reviewable.
            await using (var db = context())
            { var old = (await db.ApprovalRequests.FindAsync(approval))!; var snapshot = System.Text.Json.Nodes.JsonNode.Parse(old.Snapshot)!; Assert.Null(snapshot["venues"]![0]!["courts"]![0]!["imageUploadId"]); snapshot["venues"]![0]!["courts"]![0]!["imageUploadId"] = Guid.NewGuid().ToString(); old.Snapshot = snapshot.ToJsonString(); await db.SaveChangesAsync(); }
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{approval}/approve", null));
            Assert.Single((await Data(await anon.GetAsync($"/api/v1/venues/{newVenue}"))).GetProperty("courts").EnumerateArray());
            Assert.Equal(2, (await Data(await anon.GetAsync($"/api/v1/venues/{venueId}"))).GetProperty("courts").GetArrayLength());
        });
    }
    [Fact]
    public async Task Sensitive_revisions_preserve_direct_photo_changes_and_business_waits_for_admin()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (businessId, venueId, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            using var admin = await Login(anon, factory, "addition-admin@example.test", true);
            var qr = await Upload(owner, venueId, "QR");
            var approval = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venueId}/revisions", new {
                address = "New reviewed address in District One", contact = "New Contact", timezone = "Asia/Ho_Chi_Minh",
                latitude = 10.9, longitude = 106.8, bankCode = "TEST", accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = qr
            }))).GetProperty("id").GetGuid();
            var image = await Upload(owner, venueId, "VENUE_IMAGE");
            using var imageRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/operator/venues/{venueId}/image") {
                Content = JsonContent.Create(new { uploadId = image }) };
            imageRequest.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
            await Data(await owner.SendAsync(imageRequest));
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{approval}/approve", null));
            await using (var db = context())
            {
                var venue = (await db.Venues.FindAsync(venueId))!;
                Assert.Equal(image, venue.ImageUploadId); Assert.Equal("New reviewed address in District One", venue.Address);
            }
            using var legal = new HttpRequestMessage(HttpMethod.Post, $"{Root}/businesses/{businessId}/revisions") {
                Content = JsonContent.Create(new { name = "Renamed Club", legalName = "Renamed Company", contact = "Renamed Contact" }) };
            legal.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
            approval = (await Data(await owner.SendAsync(legal))).GetProperty("id").GetGuid();
            await using (var db = context()) Assert.Equal("Existing Club", (await db.Businesses.FindAsync(businessId))!.Name);
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{approval}/approve", null));
            await using (var db = context()) Assert.Equal("Renamed Company", (await db.Businesses.FindAsync(businessId))!.LegalName);
        });
    }

    private static async Task<(Guid Business, Guid Venue, Guid Court)> ActiveInventory(Func<ShuttleBookDbContext> context)
    {
        await using var db = context(); await db.Database.MigrateAsync();
        var owner = User("addition-owner@example.test", AccountType.VenueOperator, UserStatus.Active);
        db.Users.AddRange(owner, User("addition-admin@example.test", AccountType.Admin, UserStatus.Active),
            User("addition-other@example.test", AccountType.VenueOperator, UserStatus.Active),
            User("addition-customer@example.test", AccountType.Customer, UserStatus.Active));
        var business = new Business { Name = "Existing Club", LegalName = "Existing Company", Contact = "Existing Contact", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var venue = new Venue { BusinessId = business.Id, Name = "Existing Venue", Address = "Existing Address", Contact = "Existing Contact", Status = "PUBLISHED", Latitude = 10.8, Longitude = 106.7, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var court = new Court { VenueId = venue.Id, Name = "Existing Court", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
        db.Businesses.Add(business); db.Venues.Add(venue); db.Courts.Add(court);
        db.BusinessMemberships.Add(new() { BusinessId = business.Id, UserId = owner.Id, Role = "OWNER", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var image = new MediaUpload { OwnerUserId = owner.Id, VenueId = venue.Id, Purpose = "VENUE_IMAGE", Status = "READY", ObjectKey = "fixture-image", ContentType = "image/png", SizeBytes = Png.Length, Sha256Base64 = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Png)), CreatedAt = DateTimeOffset.UtcNow };
        var qr = new MediaUpload { OwnerUserId = owner.Id, VenueId = venue.Id, Purpose = "QR", Status = "READY", ObjectKey = "fixture-qr", ContentType = "image/png", SizeBytes = Png.Length, Sha256Base64 = image.Sha256Base64, CreatedAt = DateTimeOffset.UtcNow };
        db.MediaUploads.AddRange(image, qr); await db.SaveChangesAsync(); venue.ImageUploadId = image.Id;
        db.VenuePaymentAccounts.Add(new() { VenueId = venue.Id, BankCode = "TEST", AccountName = "TEST OWNER", AccountNumber = "1234567890", QrUploadId = qr.Id });
        await db.SaveChangesAsync(); return (business.Id, venue.Id, court.Id);
    }
}
