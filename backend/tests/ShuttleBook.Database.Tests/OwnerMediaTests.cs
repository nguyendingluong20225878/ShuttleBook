using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_media_legacy_pending_revision_upgrade_preserves_photo_and_does_not_revive_stale_snapshot(bool stale)
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (businessId, venueId, _) = await ActiveInventory(context);
            var approvalId = Guid.CreateVersion7();
            await using (var db = context())
            {
                await db.Database.MigrateAsync("20261008103840_F07QuoteReservations");
                var ownerId = await db.Users.Where(x => x.NormalizedEmail == "addition-owner@example.test").Select(x => x.Id).SingleAsync();
                var payment = await db.VenuePaymentAccounts.SingleAsync(x => x.VenueId == venueId);
                var oldSnapshot = JsonSerializer.Serialize(new
                {
                    version = 1, address = "Legacy reviewed address", contact = "Legacy reviewed contact",
                    timezone = "Asia/Ho_Chi_Minh", latitude = 10.9, longitude = 106.8,
                    payment.BankCode, payment.AccountName, payment.AccountNumber, payment.QrUploadId
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var now = DateTimeOffset.UtcNow;
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO approval_requests (id,business_id,venue_id,kind,submitted_by,status,snapshot,submitted_at)
                    VALUES ({approvalId},{businessId},{venueId},'VENUE_REVISION',{ownerId},'PENDING',{oldSnapshot}::jsonb,{now})
                    """);
                if (stale) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE venues SET version=2 WHERE id={venueId}");
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
                var stored = await db.ApprovalRequests.AsNoTracking().SingleAsync(x => x.Id == approvalId);
                using var parsed = JsonDocument.Parse(stored.Snapshot);
                Assert.Equal(!stale, parsed.RootElement.TryGetProperty("baseline", out _));
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            using var admin = await Login(anonymous, factory, "addition-admin@example.test", true);
            var image = await Upload(owner, venueId, "VENUE_IMAGE");
            await Data(await MediaMutation(owner, $"venues/{venueId}", stale ? 2 : 1, image));
            var response = await admin.PostAsync($"/api/v1/admin/approval-requests/{approvalId}/approve", null);
            if (stale) await Problem(response, HttpStatusCode.Conflict, "VERSION_CONFLICT");
            else await Data(response);
            await using var check = context();
            var venue = await check.Venues.SingleAsync(x => x.Id == venueId);
            Assert.Equal(image, venue.ImageUploadId);
            Assert.Equal(stale ? "Existing Address" : "Legacy reviewed address", venue.Address);
            Assert.Equal(stale ? "PENDING" : "APPROVED", (await check.ApprovalRequests.SingleAsync(x => x.Id == approvalId)).Status);
        });
    }

    [Fact]
    public async Task Owner_media_migration_upgrade_preserves_existing_media_court_and_pending_revision()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            var businessId = Guid.CreateVersion7(); var venueId = Guid.CreateVersion7();
            var courtId = Guid.CreateVersion7(); var imageId = Guid.CreateVersion7();
            var approvalId = Guid.CreateVersion7(); var now = DateTimeOffset.UtcNow;
            var snapshot = "{}";
            var owner = User("media-upgrade@example.test", AccountType.VenueOperator, UserStatus.Active);
            await using (var db = context())
            {
                await db.Database.MigrateAsync("20261008103840_F07QuoteReservations");
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO users (id,account_type,status,email,normalized_email,password_hash,
                        email_verified_at,created_at,updated_at)
                    VALUES ({owner.Id},{owner.AccountType.ToString()},{owner.Status.ToString()},
                        {owner.Email},{owner.NormalizedEmail},{owner.PasswordHash},
                        {owner.EmailVerifiedAt},{owner.CreatedAt},{owner.UpdatedAt})
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO businesses (id,name,legal_name,contact,status,version,created_at,updated_at)
                    VALUES ({businessId},'Old','Old','Test','ACTIVE',1,{now},{now});
                    INSERT INTO venues (id,business_id,name,address,contact,timezone,latitude,longitude,status,version,created_at,updated_at)
                    VALUES ({venueId},{businessId},'Old venue','Test','Test','Asia/Ho_Chi_Minh',21,105,'PUBLISHED',7,{now},{now});
                    INSERT INTO courts (id,venue_id,name,status,version,booking_block_minutes,minimum_booking_minutes,hold_minutes,created_at)
                    VALUES ({courtId},{venueId},'Old court','ACTIVE',4,30,30,20,{now});
                    INSERT INTO media_uploads (id,owner_user_id,venue_id,purpose,object_key,content_type,size_bytes,sha256_base64,status,created_at)
                    VALUES ({imageId},{owner.Id},{venueId},'VENUE_IMAGE','upgrade/old-image','image/png',68,'unused','READY',{now});
                    UPDATE venues SET image_upload_id={imageId} WHERE id={venueId};
                    INSERT INTO approval_requests (id,business_id,venue_id,kind,submitted_by,status,snapshot,submitted_at)
                    VALUES ({approvalId},{businessId},{venueId},'VENUE_REVISION',{owner.Id},'PENDING',{snapshot}::jsonb,{now});
                    """);
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
            await using var check = context();
            var court = await check.Courts.SingleAsync(x => x.Id == courtId);
            Assert.Null(court.ImageUploadId);
            Assert.Equal(4, court.Version);
            var venue = await check.Venues.SingleAsync(x => x.Id == venueId);
            Assert.Equal(imageId, venue.ImageUploadId);
            Assert.Equal(7, venue.Version);
            Assert.Null((await check.MediaUploads.SingleAsync(x => x.Id == imageId)).CourtId);
            var revision = await check.ApprovalRequests.SingleAsync(x => x.Id == approvalId);
            Assert.Equal("VENUE_REVISION", revision.Kind);
            Assert.Equal("PENDING", revision.Status);
            Assert.Null(revision.CourtId);
        });
    }

    [Fact]
    public async Task Owner_optional_venue_image_can_add_replace_remove_without_approval_or_court_changes()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var fixture = await SeedMediaOwner(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            Assert.Equal(JsonValueKind.Null, (await Data(await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}"))).GetProperty("imageUrl").ValueKind);
            var first = await Upload(owner, fixture.VenueId, "VENUE_IMAGE");
            await Data(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, first));
            using (var image = await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}/image"))
            {
                Assert.Equal(HttpStatusCode.OK, image.StatusCode);
                Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
                Assert.Contains("no-store", image.Headers.CacheControl!.ToString());
            }
            var second = await Upload(owner, fixture.VenueId, "VENUE_IMAGE");
            await Data(await MediaMutation(owner, $"venues/{fixture.VenueId}", 2, second));
            await Data(await MediaMutation(owner, $"venues/{fixture.VenueId}", 3, null));
            var detail = await Data(await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}"));
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("imageUrl").ValueKind);
            Assert.False(detail.GetProperty("courts")[0].TryGetProperty("imageUrl", out _));
            await Problem(await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}/image"), HttpStatusCode.NotFound, "NOT_FOUND");
            await using var db = context();
            Assert.Equal(4, (await db.Venues.FindAsync(fixture.VenueId))!.Version);
            Assert.Equal(1, (await db.Courts.FindAsync(fixture.CourtId))!.Version);
            Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.Action == "venue.image_updated"));
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "venue.image_removed"));
            Assert.Empty(await db.ApprovalRequests.ToListAsync());
            Assert.Equal(2, await db.MediaUploads.CountAsync(x => x.Status == "READY"));
        });
    }

    [Fact]
    public async Task Owner_venue_image_rejects_unready_purpose_scope_and_missing_stale_version()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var fixture = await SeedMediaOwner(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            var ready = await Upload(owner, fixture.VenueId, "VENUE_IMAGE");
            var pending = await Data(await owner.PostAsJsonAsync("/api/v1/uploads/presign", VenueUploadBody(fixture.VenueId)));
            var qr = await Upload(owner, fixture.VenueId, "QR");
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", null, ready), (HttpStatusCode)428, "PRECONDITION_REQUIRED");
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 9, ready), HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, pending.GetProperty("id").GetGuid()), HttpStatusCode.BadRequest, "UPLOAD_INVALID");
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, qr), HttpStatusCode.BadRequest, "UPLOAD_INVALID");
            Guid foreignId, otherVenueId;
            await using (var db = context())
            {
                var other = User("media-other@example.test", AccountType.VenueOperator, UserStatus.Active);
                db.Users.Add(other);
                var otherVenue = new Venue { BusinessId = (await db.Venues.FindAsync(fixture.VenueId))!.BusinessId, Name = "Other venue", Address = "Other address", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                db.Venues.Add(otherVenue);
                var foreign = MediaRow(fixture.VenueId, null, other.Id, "VENUE_IMAGE");
                db.MediaUploads.Add(foreign);
                await db.SaveChangesAsync(); foreignId = foreign.Id; otherVenueId = otherVenue.Id;
            }
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, foreignId), HttpStatusCode.BadRequest, "UPLOAD_INVALID");
            await Problem(await MediaMutation(owner, $"venues/{otherVenueId}", 1, ready), HttpStatusCode.BadRequest, "UPLOAD_INVALID");
            using var bodyDelete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/operator/venues/{fixture.VenueId}/image") { Content = JsonContent.Create(new { uploadId = ready }) };
            bodyDelete.Headers.IfMatch.Add(new EntityTagHeaderValue("\"1\""));
            await Problem(await owner.SendAsync(bodyDelete), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await using var check = context();
            Assert.Null((await check.Venues.FindAsync(fixture.VenueId))!.ImageUploadId);
            Assert.Equal(1, (await check.Venues.FindAsync(fixture.VenueId))!.Version);
        });
    }

    [Fact]
    public async Task Owner_venue_image_draft_is_private_and_pending_venue_locks_put_delete_upload()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var fixture = await SeedMediaOwner(context);
            await using (var db = context()) { (await db.Venues.FindAsync(fixture.VenueId))!.Status = "DRAFT"; await db.SaveChangesAsync(); }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            var image = await Upload(owner, fixture.VenueId, "VENUE_IMAGE");
            await Data(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, image));
            await Problem(await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}/image"), HttpStatusCode.NotFound, "NOT_FOUND");
            await using (var db = context()) { (await db.Venues.FindAsync(fixture.VenueId))!.Status = "PENDING_APPROVAL"; await db.SaveChangesAsync(); }
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 2, image), HttpStatusCode.Conflict, "STATE_CONFLICT");
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 2, null), HttpStatusCode.Conflict, "STATE_CONFLICT");
            await Problem(await owner.PostAsJsonAsync("/api/v1/uploads/presign", VenueUploadBody(fixture.VenueId)), HttpStatusCode.Conflict, "STATE_CONFLICT");
            await using (var db = context()) { (await db.Venues.FindAsync(fixture.VenueId))!.Status = "PUBLISHED"; await db.SaveChangesAsync(); }
            using var published = await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}/image");
            Assert.Equal(HttpStatusCode.OK, published.StatusCode);
            Assert.Equal(Png, await published.Content.ReadAsByteArrayAsync());
        });
    }

    [Fact]
    public async Task Owner_venue_image_concurrent_same_version_commits_once()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var fixture = await SeedMediaOwner(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            var first = await Upload(owner, fixture.VenueId, "VENUE_IMAGE");
            var second = await Upload(owner, fixture.VenueId, "VENUE_IMAGE");
            var results = await Task.WhenAll(MediaMutation(owner, $"venues/{fixture.VenueId}", 1, first), MediaMutation(owner, $"venues/{fixture.VenueId}", 1, second));
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.PreconditionFailed);
            foreach (var result in results) result.Dispose();
            await using var db = context();
            Assert.Equal(2, (await db.Venues.FindAsync(fixture.VenueId))!.Version);
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "venue.image_updated"));
        });
    }

    [Fact]
    public async Task Owner_venue_image_rejects_customer_other_owner_suspended_and_revoked_membership()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var fixture = await SeedMediaOwner(context);
            await using (var db = context()) { db.Users.AddRange(User("media-customer@example.test", AccountType.Customer, UserStatus.Active), User("media-other@example.test", AccountType.VenueOperator, UserStatus.Active)); await db.SaveChangesAsync(); }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            using var other = await Login(anonymous, factory, "media-other@example.test");
            using var customer = await Login(anonymous, factory, "media-customer@example.test");
            await Problem(await MediaMutation(customer, $"venues/{fixture.VenueId}", 1, null), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await MediaMutation(other, $"venues/{fixture.VenueId}", 1, null), HttpStatusCode.NotFound, "NOT_FOUND");
            await using (var db = context()) { (await db.Users.FindAsync(fixture.OwnerId))!.Status = UserStatus.Suspended; await db.SaveChangesAsync(); }
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, null), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
            await using (var db = context()) { (await db.Users.FindAsync(fixture.OwnerId))!.Status = UserStatus.Active; (await db.BusinessMemberships.SingleAsync(x => x.UserId == fixture.OwnerId)).Status = "REVOKED"; await db.SaveChangesAsync(); }
            await Problem(await MediaMutation(owner, $"venues/{fixture.VenueId}", 1, null), HttpStatusCode.NotFound, "NOT_FOUND");
        });
    }

    [Fact]
    public async Task Court_media_routes_and_presign_are_disabled_and_legacy_media_remains_private()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var fixture = await SeedMediaOwner(context);
            Guid readyId, pendingId;
            await using (var db = context())
            {
                var ready = MediaRow(fixture.VenueId, fixture.CourtId, fixture.OwnerId, "COURT_IMAGE");
                var pending = MediaRow(fixture.VenueId, fixture.CourtId, fixture.OwnerId, "COURT_IMAGE"); pending.Status = "PENDING";
                db.MediaUploads.AddRange(ready, pending); await db.SaveChangesAsync();
                (await db.Courts.FindAsync(fixture.CourtId))!.ImageUploadId = ready.Id; await db.SaveChangesAsync();
                db.Users.Add(User("media-admin@example.test", AccountType.Admin, UserStatus.Active)); await db.SaveChangesAsync();
                readyId = ready.Id; pendingId = pending.Id;
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "media-owner@example.test");
            using var admin = await Login(anonymous, factory, "media-admin@example.test", true);
            foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
            { using var request = new HttpRequestMessage(method, $"/api/v1/operator/courts/{fixture.CourtId}/image"); using var response = await owner.SendAsync(request); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
            using (var response = await anonymous.GetAsync($"/api/v1/courts/{fixture.CourtId}/image")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await Problem(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new { venueId = fixture.VenueId, purpose = "COURT_IMAGE", contentType = "image/png", sizeBytes = Png.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(Png)) }), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await owner.PostAsJsonAsync("/api/v1/uploads/presign", new { venueId = fixture.VenueId, courtId = fixture.CourtId, purpose = "VENUE_IMAGE", contentType = "image/png", sizeBytes = Png.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(Png)) }), HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
            foreach (var client in new[] { owner, admin }) foreach (var id in new[] { readyId, pendingId })
            {
                await Problem(await client.GetAsync($"/api/v1/uploads/{id}/view"), HttpStatusCode.NotFound, "NOT_FOUND");
                await Problem(await client.PostAsync($"/api/v1/uploads/{id}/complete", null), HttpStatusCode.NotFound, "NOT_FOUND");
            }
            var publicCourt = (await Data(await anonymous.GetAsync($"/api/v1/venues/{fixture.VenueId}"))).GetProperty("courts")[0];
            Assert.False(publicCourt.TryGetProperty("imageUrl", out _)); Assert.False(publicCourt.TryGetProperty("imageUploadId", out _));
            var businessId = await GetBusinessId(context, fixture.VenueId);
            var privateCourt = (await Data(await owner.GetAsync($"{Root}/businesses/{businessId}"))).GetProperty("venues")[0].GetProperty("courts")[0];
            Assert.False(privateCourt.TryGetProperty("imageUploadId", out _));
            await using var check = context(); Assert.Equal(readyId, (await check.Courts.FindAsync(fixture.CourtId))!.ImageUploadId);
            Assert.Equal(2, await check.MediaUploads.CountAsync(x => x.Purpose == "COURT_IMAGE"));
        });
    }

    [Fact]
    public async Task Initial_profile_without_optional_image_can_submit_and_approve_but_qr_and_schedule_remain_required()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await using (var db = context()) { await db.Database.MigrateAsync(); db.Users.AddRange(User("optional-owner@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding), User("optional-admin@example.test", AccountType.Admin, UserStatus.Active)); await db.SaveChangesAsync(); }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "optional-owner@example.test");
            using var admin = await Login(anonymous, factory, "optional-admin@example.test", true);
            var business = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses", BusinessBody()), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var venue = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses/{business}/venues", VenueBody()), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var court = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "1" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            await Problem(await owner.PostAsync($"{Root}/businesses/{business}/submit", null), HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            var qr = await Upload(owner, venue, "QR");
            var paymentSetup = await Data(await owner.PutAsJsonAsync($"{Root}/venues/{venue}/payment-account", new { accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = qr }));
            Assert.False(paymentSetup.TryGetProperty("bankCode", out _));
            await using (var paymentCheck = context()) Assert.Equal("", (await paymentCheck.VenuePaymentAccounts.SingleAsync(x => x.VenueId == venue)).BankCode);
            await Problem(await owner.PostAsync($"{Root}/businesses/{business}/submit", null), HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            await Data(await DraftSchedulePut(owner, court, ScheduleBody(), 1));
            // An attached invalid venue image still blocks; omitting it is valid.
            await using (var db = context()) { (await db.Venues.FindAsync(venue))!.ImageUploadId = qr; await db.SaveChangesAsync(); }
            await Problem(await owner.PostAsync($"{Root}/businesses/{business}/submit", null), HttpStatusCode.Conflict, "INCOMPLETE_PROFILE");
            await Data(await MediaMutation(owner, $"venues/{venue}", 1, null));
            var approval = (await Data(await owner.PostAsync($"{Root}/businesses/{business}/submit", null))).GetProperty("id").GetGuid();
            var review = await Data(await admin.GetAsync($"/api/v1/admin/approval-requests/{approval}"));
            using var snapshot = JsonDocument.Parse(review.GetProperty("snapshot").GetString()!);
            Assert.DoesNotContain("bankCode", review.GetProperty("snapshot").GetString());
            Assert.False(snapshot.RootElement.GetProperty("venues")[0].GetProperty("courts")[0].TryGetProperty("imageUploadId", out _));
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{approval}/approve", null));
            var detail = await Data(await anonymous.GetAsync($"/api/v1/venues/{venue}"));
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("imageUrl").ValueKind);
            Assert.Single(detail.GetProperty("courts").EnumerateArray());
        });
    }

    private sealed record MediaFixture(Guid OwnerId, Guid VenueId, Guid CourtId);
    private static async Task<MediaFixture> SeedMediaOwner(Func<ShuttleBook.Infrastructure.Data.ShuttleBookDbContext> context)
    {
        await using var db = context(); await db.Database.MigrateAsync();
        var owner = User("media-owner@example.test", AccountType.VenueOperator, UserStatus.Active);
        var business = new Business { Name = "Media business", LegalName = "Media business", Contact = "Test", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var venue = new Venue { BusinessId = business.Id, Name = "Media venue", Address = "Test address", Contact = "Test contact", Status = "PUBLISHED", Latitude = 21, Longitude = 105, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var court = new Court { VenueId = venue.Id, Name = "Sân 1", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
        db.Users.Add(owner); db.Businesses.Add(business); db.Venues.Add(venue); db.Courts.Add(court);
        db.BusinessMemberships.Add(new BusinessMembership { BusinessId = business.Id, UserId = owner.Id, Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); return new(owner.Id, venue.Id, court.Id);
    }
    private static async Task<Guid> GetBusinessId(Func<ShuttleBook.Infrastructure.Data.ShuttleBookDbContext> context, Guid venue)
    { await using var db = context(); return (await db.Venues.FindAsync(venue))!.BusinessId; }
    private static MediaUpload MediaRow(Guid venueId, Guid? courtId, Guid ownerId, string purpose) => new()
    {
        VenueId = venueId, CourtId = courtId, OwnerUserId = ownerId, Purpose = purpose, Status = "READY",
        ObjectKey = $"media-tests/{Guid.NewGuid():N}", ContentType = "image/png", SizeBytes = Png.Length,
        Sha256Base64 = Convert.ToBase64String(SHA256.HashData(Png)), CreatedAt = DateTimeOffset.UtcNow
    };
    private static object VenueUploadBody(Guid venueId) => new { venueId, purpose = "VENUE_IMAGE", contentType = "image/png", sizeBytes = Png.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(Png)) };
    private static Task<HttpResponseMessage> MediaMutation(HttpClient owner, string target, long? version, Guid? upload)
    {
        var request = new HttpRequestMessage(upload.HasValue ? HttpMethod.Put : HttpMethod.Delete, $"/api/v1/operator/{target}/image");
        if (version.HasValue) request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        if (upload.HasValue) request.Content = JsonContent.Create(new { uploadId = upload.Value });
        return owner.SendAsync(request);
    }
}
