using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task F03_upgrade_preserves_f02_schedule_and_database_exclusion_constraints()
    {
        await WithDatabaseAsync(async (_, context) =>
        {
            Guid courtId;
            Guid qrUploadId;
            await using (var db = context())
            {
                await db.Database.MigrateAsync("20261005105721_F02VenueContact");
                var businessId = Guid.CreateVersion7();
                var venueId = Guid.CreateVersion7();
                courtId = Guid.CreateVersion7();
                qrUploadId = Guid.CreateVersion7();
                var now = DateTimeOffset.UtcNow;
                var owner = User("f03-upgrade-owner@example.test", AccountType.VenueOperator, UserStatus.Active);
                db.Users.Add(owner);
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO businesses (id,name,legal_name,contact,status,version,created_at,updated_at)
                    VALUES ({businessId},'F03 Upgrade','F03 Upgrade Company','Test contact','ACTIVE',1,{now},{now})
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO venues (id,business_id,name,address,contact,timezone,latitude,longitude,status,version,created_at,updated_at)
                    VALUES ({venueId},{businessId},'F03 Venue','Test address in District One','Test venue contact',
                        'Asia/Ho_Chi_Minh',10.8,106.7,'PUBLISHED',1,{now},{now})
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO courts (id,venue_id,name,status,created_at)
                    VALUES ({courtId},{venueId},'F03 Court','ACTIVE',{now})
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO court_operating_hours (id,court_id,day_of_week,opens_at,closes_at)
                    VALUES ({Guid.CreateVersion7()},{courtId},1,TIME '08:00',TIME '10:00')
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO pricing_rules (id,court_id,day_of_week,starts_at,ends_at,price_per_slot)
                    VALUES ({Guid.CreateVersion7()},{courtId},1,TIME '08:00',TIME '10:00',100000)
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO media_uploads (id,owner_user_id,venue_id,purpose,object_key,content_type,
                        size_bytes,sha256_base64,status,created_at)
                    VALUES ({qrUploadId},{owner.Id},{venueId},'PAYMENT_QR','f03-upgrade-qr','image/png',
                        100,'test-checksum','READY',{now})
                    """);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO venue_payment_accounts (id,venue_id,bank_code,account_name,account_number,qr_upload_id)
                    VALUES ({Guid.CreateVersion7()},{venueId},'TEST','F03 Upgrade','123456',{qrUploadId})
                    """);
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
            await using (var db = context())
            {
                Assert.Equal(1, (await db.Courts.SingleAsync(x => x.Id == courtId)).Version);
                var price = await db.PricingRules.SingleAsync(x => x.CourtId == courtId);
                Assert.Equal(DateOnly.MinValue, price.StartsOn);
                Assert.Equal(DateOnly.MaxValue, price.EndsOn);
                Assert.Equal(0, price.Priority);
                Assert.Equal(100000, price.PricePerSlot);
                Assert.Equal(1, await db.CourtOperatingHours.CountAsync(x => x.CourtId == courtId));
                Assert.Equal(qrUploadId, (await db.VenuePaymentAccounts.SingleAsync()).QrUploadId);
                Assert.Equal("READY", (await db.MediaUploads.SingleAsync(x => x.Id == qrUploadId)).Status);
            }
            var monday = NextMonday();
            await using (var db = context())
            {
                db.PricingRules.AddRange(
                    new PricingRule { CourtId = courtId, DayOfWeek = 1, StartsOn = monday,
                        EndsOn = monday.AddDays(7), StartsAt = new TimeOnly(8, 0),
                        EndsAt = new TimeOnly(9, 0), PricePerSlot = 200000, Priority = 1 },
                    new PricingRule { CourtId = courtId, DayOfWeek = 1, StartsOn = monday,
                        EndsOn = monday.AddDays(7), StartsAt = new TimeOnly(8, 30),
                        EndsAt = new TimeOnly(9, 30), PricePerSlot = 300000, Priority = 1 });
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal("23P01", (error.InnerException as PostgresException)?.SqlState);
            }
            await using (var db = context())
            {
                var start = new DateTimeOffset(monday.ToDateTime(new TimeOnly(1, 0)), TimeSpan.Zero);
                db.CourtAllocations.AddRange(
                    new CourtAllocation { CourtId = courtId, StartsAt = start, EndsAt = start.AddHours(1),
                        CreatedAt = DateTimeOffset.UtcNow },
                    new CourtAllocation { CourtId = courtId, Kind = "BOOKING", StartsAt = start.AddMinutes(30),
                        EndsAt = start.AddHours(2), CreatedAt = DateTimeOffset.UtcNow });
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal("23P01", (error.InnerException as PostgresException)?.SqlState);
            }
        });
    }

    [Fact]
    public async Task F03_active_owner_configures_prices_and_maintenance_with_scope_and_version()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            Guid courtId;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var owner = User("f03-owner@example.test", AccountType.VenueOperator, UserStatus.Active);
                var other = User("f03-other@example.test", AccountType.VenueOperator, UserStatus.Active);
                var pending = User("f03-pending@example.test", AccountType.VenueOperator, UserStatus.PendingOnboarding);
                var customer = User("f03-customer@example.test", AccountType.Customer, UserStatus.Active);
                var business = new Business { Name = "F03 Club", LegalName = "F03 Club Company",
                    Contact = "Test contact", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow };
                var otherBusiness = new Business { Name = "Other Club", LegalName = "Other Club Company",
                    Contact = "Test contact", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow };
                var venue = new Venue { BusinessId = business.Id, Name = "F03 Venue",
                    Address = "Test address in District One", Contact = "Test venue contact",
                    Latitude = 10.8, Longitude = 106.7, Status = "PUBLISHED",
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                var court = new Court { VenueId = venue.Id, Name = "F03 Court", Status = "ACTIVE",
                    CreatedAt = DateTimeOffset.UtcNow };
                courtId = court.Id;
                db.Users.AddRange(owner, other, pending, customer);
                db.Businesses.AddRange(business, otherBusiness); db.Venues.Add(venue); db.Courts.Add(court);
                db.BusinessMemberships.AddRange(
                    new BusinessMembership { BusinessId = business.Id, UserId = owner.Id,
                        Role = "OWNER", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow },
                    new BusinessMembership { BusinessId = otherBusiness.Id, UserId = other.Id,
                        Role = "OWNER", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow });
                db.CourtOperatingHours.Add(new CourtOperatingHour { CourtId = courtId, DayOfWeek = 1,
                    OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(10, 0) });
                db.PricingRules.Add(new PricingRule { CourtId = courtId, DayOfWeek = 1,
                    StartsAt = new TimeOnly(8, 0), EndsAt = new TimeOnly(10, 0), PricePerSlot = 100000 });
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var ownerClient = await Login(anonymous, factory, "f03-owner@example.test");
            using var otherClient = await Login(anonymous, factory, "f03-other@example.test");
            using var pendingClient = await Login(anonymous, factory, "f03-pending@example.test");
            using var customerClient = await Login(anonymous, factory, "f03-customer@example.test");
            var root = $"/api/v1/operator/courts/{courtId}";
            await Problem(await anonymous.GetAsync($"{root}/operations"), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
            await Problem(await otherClient.GetAsync($"{root}/operations"), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await pendingClient.GetAsync($"{root}/operations"), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await customerClient.GetAsync($"{root}/operations"), HttpStatusCode.Forbidden, "FORBIDDEN");
            var operations = await Data(await ownerClient.GetAsync($"{root}/operations"));
            Assert.Equal(1, operations.GetProperty("version").GetInt64());

            var schedule = new { hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "10:00" } },
                prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "09:00", pricePerSlot = 100000 },
                    new { dayOfWeek = 1, startsAt = "09:00", endsAt = "10:00", pricePerSlot = 200000 } } };
            await Problem(await ownerClient.PutAsJsonAsync($"{root}/schedule", schedule),
                (HttpStatusCode)428, "PRECONDITION_REQUIRED");
            using (var request = new HttpRequestMessage(HttpMethod.Put, $"{root}/schedule")
            { Content = JsonContent.Create(schedule) })
            {
                request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
                var saved = await Data(await ownerClient.SendAsync(request));
                Assert.Equal(2, saved.GetProperty("version").GetInt64());
            }
            using (var request = new HttpRequestMessage(HttpMethod.Put, $"{root}/schedule")
            { Content = JsonContent.Create(schedule) })
            {
                request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
                await Problem(await ownerClient.SendAsync(request),
                    HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            }
            using (var request = VersionedPut($"{root}/schedule", new {
                hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "10:00" } },
                prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "09:00", pricePerSlot = 0 } }
            }, 2))
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            using (var request = VersionedPut($"{root}/schedule", new {
                hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "10:00" } },
                prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "09:30", pricePerSlot = 100000 } }
            }, 2))
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            using (var request = VersionedPut($"{root}/schedule", new {
                hours = new[] { new { dayOfWeek = 1, opensAt = "08:15", closesAt = "10:00" } },
                prices = new[] { new { dayOfWeek = 1, startsAt = "08:15", endsAt = "10:00", pricePerSlot = 100000 } }
            }, 2))
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            Assert.Equal(2, (await Data(await ownerClient.GetAsync($"{root}/operations")))
                .GetProperty("version").GetInt64());

            var monday = NextMonday();
            var rules = new { rules = new[] { new { startsOn = monday, endsOn = monday.AddDays(7),
                dayOfWeek = 1, startsAt = "08:30", endsAt = "09:30", pricePerSlot = 300000, priority = 5 } } };
            using (var request = new HttpRequestMessage(HttpMethod.Put, $"{root}/pricing-rules")
            { Content = JsonContent.Create(rules) })
            {
                request.Headers.TryAddWithoutValidation("If-Match", "\"2\"");
                var saved = await Data(await ownerClient.SendAsync(request));
                Assert.Equal(3, saved.GetProperty("version").GetInt64());
            }
            var preview = await Data(await ownerClient.GetAsync(
                $"{root}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08%3A00&endsAt=10%3A00"));
            Assert.Equal(900000m, preview.GetProperty("totalPrice").GetDecimal());
            Assert.Equal(4, preview.GetProperty("slots").GetArrayLength());
            await Problem(await ownerClient.GetAsync(
                $"{root}/price-preview?date={monday:yyyy-MM-dd}&startsAt=07%3A00&endsAt=08%3A00"),
                HttpStatusCode.Conflict, "PRICE_UNAVAILABLE");
            await Problem(await ownerClient.GetAsync(
                $"{root}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08%3A15&endsAt=10%3A00"),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var overlapping = new { rules = new[] {
                new { startsOn = monday, endsOn = monday.AddDays(7), dayOfWeek = 1,
                    startsAt = "08:00", endsAt = "09:00", pricePerSlot = 200000, priority = 5 },
                new { startsOn = monday, endsOn = monday.AddDays(7), dayOfWeek = 1,
                    startsAt = "08:30", endsAt = "09:30", pricePerSlot = 300000, priority = 5 } } };
            using (var request = new HttpRequestMessage(HttpMethod.Put, $"{root}/pricing-rules")
            { Content = JsonContent.Create(overlapping) })
            {
                request.Headers.TryAddWithoutValidation("If-Match", "\"3\"");
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            }
            using (var request = VersionedPut($"{root}/pricing-rules", new { rules = new[] {
                new { startsOn = monday, endsOn = monday.AddDays(7), dayOfWeek = 1,
                    startsAt = "07:30", endsAt = "08:30", pricePerSlot = 300000, priority = 5 }
            } }, 3))
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            using (var firstUpdate = VersionedPut($"{root}/schedule", schedule, 3))
            using (var secondUpdate = VersionedPut($"{root}/schedule", schedule, 3))
            {
                var results = await Task.WhenAll(ownerClient.SendAsync(firstUpdate), ownerClient.SendAsync(secondUpdate));
                Assert.Contains(results, x => x.StatusCode == HttpStatusCode.OK);
                Assert.Contains(results, x => x.StatusCode == HttpStatusCode.PreconditionFailed);
                foreach (var response in results) response.Dispose();
            }
            Assert.Equal(4, (await Data(await ownerClient.GetAsync($"{root}/operations")))
                .GetProperty("version").GetInt64());

            var initialPolicy = await Data(await ownerClient.GetAsync($"{root}/operations"));
            Assert.Equal(30, initialPolicy.GetProperty("bookingBlockMinutes").GetInt32());
            Assert.Equal(30, initialPolicy.GetProperty("minimumBookingMinutes").GetInt32());
            Assert.Equal(20, initialPolicy.GetProperty("holdMinutes").GetInt32());
            var policy60 = new { bookingBlockMinutes = 60, minimumBookingMinutes = 60, holdMinutes = 20 };
            using (var request = VersionedPut($"{root}/booking-policy", policy60, 4))
                await Problem(await otherClient.SendAsync(request), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await ownerClient.PutAsJsonAsync($"{root}/booking-policy", policy60),
                (HttpStatusCode)428, "PRECONDITION_REQUIRED");
            using (var request = VersionedPut($"{root}/booking-policy", new {
                bookingBlockMinutes = 45, minimumBookingMinutes = 60, holdMinutes = 20 }, 4))
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            using (var request = VersionedPut($"{root}/booking-policy", policy60, 4))
                Assert.Equal(5, (await Data(await ownerClient.SendAsync(request))).GetProperty("version").GetInt64());
            using (var request = VersionedPut($"{root}/booking-policy", policy60, 4))
                await Problem(await ownerClient.SendAsync(request), HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            await Problem(await ownerClient.GetAsync(
                $"{root}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08%3A00&endsAt=08%3A30"),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var policyPreview = await Data(await ownerClient.GetAsync(
                $"{root}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08%3A00&endsAt=09%3A00"));
            Assert.Equal(2, policyPreview.GetProperty("slots").GetArrayLength());
            using (var request = VersionedPut($"{root}/booking-policy", new {
                bookingBlockMinutes = 90, minimumBookingMinutes = 90, holdMinutes = 20 }, 5))
                Assert.Equal(6, (await Data(await ownerClient.SendAsync(request))).GetProperty("version").GetInt64());
            var preview90 = await Data(await ownerClient.GetAsync(
                $"{root}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08%3A00&endsAt=09%3A30"));
            Assert.Equal(3, preview90.GetProperty("slots").GetArrayLength());
            await using (var guard = context())
            {
                var invalid = await Assert.ThrowsAsync<PostgresException>(() => guard.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE courts SET hold_minutes = {0} WHERE id = {courtId}"));
                Assert.Equal("23514", invalid.SqlState);
            }

            var maintenance = new { date = monday, startsAt = "08:00", endsAt = "09:00",
                reason = "Test court maintenance" };
            await Problem(await ownerClient.PostAsJsonAsync($"{root}/maintenance", new {
                date = monday.AddDays(-14), startsAt = "08:00", endsAt = "09:00",
                reason = "Past court maintenance" }), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await ownerClient.PostAsJsonAsync($"{root}/maintenance", new {
                date = monday, startsAt = "08:15", endsAt = "09:00",
                reason = "Off-grid court maintenance" }), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var first = await Data(await ownerClient.PostAsJsonAsync($"{root}/maintenance", maintenance), HttpStatusCode.Created);
            var maintenanceId = first.GetProperty("id").GetGuid();
            Assert.Equal("01:00:00", first.GetProperty("startsAt").GetDateTimeOffset().UtcDateTime.ToString("HH:mm:ss"));
            await Problem(await ownerClient.PostAsJsonAsync($"{root}/maintenance", maintenance),
                HttpStatusCode.Conflict, "SLOT_CONFLICT");
            Assert.Single((await Data(await ownerClient.GetAsync($"{root}/maintenance"))).EnumerateArray());
            await Data(await ownerClient.PostAsync($"{root}/maintenance/{maintenanceId}/cancel", null));
            await Data(await ownerClient.PostAsync($"{root}/maintenance/{maintenanceId}/cancel", null));
            var raced = await Task.WhenAll(
                ownerClient.PostAsJsonAsync($"{root}/maintenance", maintenance),
                ownerClient.PostAsJsonAsync($"{root}/maintenance", maintenance));
            Assert.Contains(raced, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Contains(raced, x => x.StatusCode == HttpStatusCode.Conflict);
            foreach (var response in raced) response.Dispose();
            await using var check = context();
            Assert.Equal(1, await check.AuditEvents.CountAsync(x => x.Action == "court.maintenance_cancelled"));
            Assert.Equal(2, await check.CourtAllocations.CountAsync(x => x.CourtId == courtId));
            Assert.Equal(1, await check.CourtAllocations.CountAsync(x => x.CourtId == courtId && x.Status == "RESERVED"));
            Assert.All(await check.CourtAllocations.Where(x => x.CourtId == courtId).ToListAsync(),
                allocation => Assert.Equal("01:00:00", allocation.StartsAt.UtcDateTime.ToString("HH:mm:ss")));
            var currentOwner = await check.Users.SingleAsync(x => x.NormalizedEmail == "f03-owner@example.test");
            currentOwner.Status = UserStatus.Suspended;
            await check.SaveChangesAsync();
            await Problem(await ownerClient.GetAsync($"{root}/operations"), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
        });
    }

    private static HttpRequestMessage VersionedPut(string url, object body, long version)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return request;
    }

    private static DateOnly NextMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        return today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7);
    }
}
