using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed class PublicVenuesTests
{
    [Fact]
    public async Task Public_search_nearby_detail_and_validation_use_PostGIS_and_public_scope()
    {
        await WithDatabase(async (context, connection) =>
        {
            Guid nearId, farId, draftId, inactiveCourtVenueId, suspendedVenueId;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var now = DateTimeOffset.UtcNow;
                var business = new Business { Name = "F04 Club", LegalName = "Test", Contact = "Private business contact",
                    Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
                db.Businesses.Add(business);
                var near = Venue(business.Id, "Hoàng Cầu", 21.0278, 105.8342, "PUBLISHED");
                var far = Venue(business.Id, "Cầu Giấy", 21.041, 105.79, "PUBLISHED");
                var draft = Venue(business.Id, "Private draft", 21.028, 105.834, "DRAFT");
                var inactiveCourtVenue = Venue(business.Id, "No active court", 21.028, 105.834, "PUBLISHED");
                var suspendedBusiness = new Business { Name = "Suspended club", LegalName = "Test", Contact = "Private",
                    Status = "SUSPENDED", CreatedAt = now, UpdatedAt = now };
                db.Businesses.Add(suspendedBusiness);
                var suspendedVenue = Venue(suspendedBusiness.Id, "Suspended venue", 21.028, 105.834, "PUBLISHED");
                db.Venues.AddRange(near, far, draft, inactiveCourtVenue, suspendedVenue);
                var inactiveCourt = Court(inactiveCourtVenue.Id, "Sân tạm ngưng");
                inactiveCourt.Status = "INACTIVE";
                db.Courts.AddRange(Court(near.Id, "Sân A"), Court(far.Id, "Sân B"), Court(draft.Id, "Sân kín"),
                    inactiveCourt, Court(suspendedVenue.Id, "Sân suspended"));
                await db.SaveChangesAsync();
                nearId = near.Id; farId = far.Id; draftId = draft.Id;
                inactiveCourtVenueId = inactiveCourtVenue.Id; suspendedVenueId = suspendedVenue.Id;
            }
            using var factory = new ApiFactory(connection);
            using var client = factory.CreateClient();
            var list = await Data(client, "/api/v1/venues?q=Ho%C3%A0ng");
            Assert.Equal(1, list.GetProperty("items").GetArrayLength());
            Assert.Equal(nearId, list.GetProperty("items")[0].GetProperty("id").GetGuid());
            var nearby = await Data(client, "/api/v1/venues/nearby?latitude=21.0278&longitude=105.8342&radiusMeters=10000");
            var items = nearby.GetProperty("items");
            Assert.Equal(2, items.GetArrayLength());
            Assert.Equal(nearId, items[0].GetProperty("id").GetGuid());
            Assert.Equal(farId, items[1].GetProperty("id").GetGuid());
            Assert.True(items[0].GetProperty("distanceMeters").GetDouble() <
                items[1].GetProperty("distanceMeters").GetDouble());
            var tightRadius = await Data(client,
                "/api/v1/venues/nearby?latitude=21.0278&longitude=105.8342&radiusMeters=1000");
            Assert.Equal(nearId, Assert.Single(tightRadius.GetProperty("items").EnumerateArray())
                .GetProperty("id").GetGuid());
            var page = await Data(client, "/api/v1/venues/nearby?latitude=21.0278&longitude=105.8342&radiusMeters=10000&limit=1");
            var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
            var second = await Data(client, $"/api/v1/venues/nearby?latitude=21.0278&longitude=105.8342&radiusMeters=10000&limit=1&cursor={cursor}");
            Assert.Equal(farId, second.GetProperty("items")[0].GetProperty("id").GetGuid());
            var detail = await Data(client, $"/api/v1/venues/{nearId}");
            Assert.Equal("Venue service contact", detail.GetProperty("contact").GetString());
            Assert.False(detail.TryGetProperty("bankCode", out _));
            Assert.DoesNotContain("Private business contact", detail.GetRawText());
            await Problem(client, $"/api/v1/venues/{draftId}", HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(client, $"/api/v1/venues/{inactiveCourtVenueId}", HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(client, $"/api/v1/venues/{suspendedVenueId}", HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(client, "/api/v1/venues/nearby?latitude=91&longitude=105", HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(client, "/api/v1/venues/nearby?latitude=21", HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(client, "/api/v1/venues?limit=0", HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(client, "/api/v1/venues?cursor=invalid", HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(client, "/api/v1/venues/nearby?latitude=21&longitude=105&date=2026-10-07", HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        });
    }

    [Fact]
    public async Task Schedule_reads_every_court_price_priority_half_open_allocation_and_private_image()
    {
        await WithDatabase(async (context, connection) =>
        {
            Guid venueId, courtAId, imageId, qrId;
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);
            var monday = today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7 + 7);
            var now = DateTimeOffset.UtcNow;
            await using (var db = context())
            {
                await db.Database.MigrateAsync();
                var owner = new User { AccountType = AccountType.VenueOperator, Status = UserStatus.Active,
                    Email = "f04-owner@example.test", NormalizedEmail = "f04-owner@example.test",
                    PasswordHash = "test-hash", CreatedAt = now, UpdatedAt = now };
                db.Users.Add(owner);
                var business = new Business { Name = "F04 Schedule", LegalName = "Test", Contact = "Private",
                    Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
                db.Businesses.Add(business);
                var venue = Venue(business.Id, "Timeline", 21.0278, 105.8342, "PUBLISHED");
                db.Venues.Add(venue);
                var a = Court(venue.Id, "Sân 1");
                var b = Court(venue.Id, "Sân 2");
                b.BookingBlockMinutes = 60; b.MinimumBookingMinutes = 60;
                db.Courts.AddRange(a, b);
                db.CourtOperatingHours.AddRange(
                    new CourtOperatingHour { CourtId = a.Id, DayOfWeek = 1, OpensAt = new(17, 0), ClosesAt = new(19, 0) },
                    new CourtOperatingHour { CourtId = b.Id, DayOfWeek = 1, OpensAt = new(18, 0), ClosesAt = new(20, 0) });
                db.PricingRules.AddRange(
                    new PricingRule { CourtId = a.Id, DayOfWeek = 1, StartsAt = new(17, 0), EndsAt = new(19, 0), PricePerSlot = 100000 },
                    new PricingRule { CourtId = a.Id, DayOfWeek = 1, StartsOn = monday, EndsOn = monday,
                        StartsAt = new(17, 30), EndsAt = new(18, 30), PricePerSlot = 150000, Priority = 1 },
                    new PricingRule { CourtId = b.Id, DayOfWeek = 1, StartsAt = new(18, 0), EndsAt = new(19, 0), PricePerSlot = 80000 });
                var blocked = LocalUtc(monday, new TimeOnly(18, 0), zone);
                db.CourtAllocations.AddRange(
                    new CourtAllocation { CourtId = a.Id, Kind = "MAINTENANCE", StartsAt = blocked,
                        EndsAt = blocked.AddMinutes(30), CreatedAt = now },
                    new CourtAllocation { CourtId = b.Id, Kind = "BOOKING", StartsAt = blocked,
                        EndsAt = blocked.AddMinutes(30), CreatedAt = now },
                    new CourtAllocation { CourtId = b.Id, Kind = "BOOKING", StartsAt = blocked.AddMinutes(30),
                        EndsAt = blocked.AddHours(1), Status = "RELEASED", CreatedAt = now, ReleasedAt = now });
                var qr = new MediaUpload { OwnerUserId = owner.Id, VenueId = venue.Id,
                    Purpose = "QR", ObjectKey = "f04-qr", ContentType = "image/png", SizeBytes = 1,
                    Sha256Base64 = Convert.ToBase64String(SHA256.HashData([0])), Status = "READY", CreatedAt = now };
                var image = new MediaUpload { OwnerUserId = owner.Id, VenueId = venue.Id,
                    Purpose = "VENUE_IMAGE", ObjectKey = "f04-image", ContentType = "image/png", SizeBytes = 1,
                    Sha256Base64 = Convert.ToBase64String(SHA256.HashData([42])), Status = "READY", CreatedAt = now };
                db.MediaUploads.AddRange(qr, image);
                await db.SaveChangesAsync();
                venueId = venue.Id; courtAId = a.Id; imageId = image.Id; qrId = qr.Id;
                venue.ImageUploadId = qr.Id;
                await db.SaveChangesAsync();
            }
            using var factory = new ApiFactory(connection);
            using var client = factory.CreateClient();
            var date = monday.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var schedule = await Data(client, $"/api/v1/venues/{venueId}/availability?date={date}");
            Assert.Equal(2, schedule.GetProperty("courts").GetArrayLength());
            var first = schedule.GetProperty("courts").EnumerateArray()
                .Single(c => c.GetProperty("courtId").GetGuid() == courtAId);
            var slots = first.GetProperty("slots");
            Assert.Equal(4, slots.GetArrayLength());
            Assert.Equal("17:00", slots[0].GetProperty("startsAt").GetString());
            Assert.Equal("AVAILABLE", slots[0].GetProperty("status").GetString());
            Assert.Equal(150000, slots[1].GetProperty("pricePerSlot").GetInt64());
            Assert.Equal("RESERVED", slots[2].GetProperty("status").GetString());
            Assert.Equal("AVAILABLE", slots[3].GetProperty("status").GetString());
            Assert.Equal("11:00", slots[2].GetProperty("startsAtUtc").GetDateTimeOffset().ToString("HH:mm"));
            var second = schedule.GetProperty("courts").EnumerateArray()
                .Single(c => c.GetProperty("courtId").GetGuid() != courtAId);
            Assert.Equal(60, second.GetProperty("bookingBlockMinutes").GetInt32());
            Assert.Equal("RESERVED", second.GetProperty("slots")[0].GetProperty("status").GetString());
            Assert.Equal("AVAILABLE", second.GetProperty("slots")[1].GetProperty("status").GetString());
            Assert.Equal("NO_PRICE", second.GetProperty("slots")[2].GetProperty("status").GetString());
            await Problem(client, $"/api/v1/venues/{venueId}/availability?date={date}&courtId={Guid.NewGuid()}",
                HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(client, $"/api/v1/venues/{venueId}/availability?date={today.AddDays(-1):yyyy-MM-dd}",
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(client, $"/api/v1/venues/{venueId}/image", HttpStatusCode.NotFound, "NOT_FOUND");
            await using (var db = context())
            {
                var venue = await db.Venues.SingleAsync(v => v.Id == venueId);
                venue.ImageUploadId = imageId;
                await db.SaveChangesAsync();
            }
            var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
            var folder = Path.Combine(environment.ContentRootPath, ".media-local");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, imageId.ToString("N"));
            await File.WriteAllBytesAsync(path, [42]);
            try
            {
                using var response = await client.GetAsync($"/api/v1/venues/{venueId}/image");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal([42], await response.Content.ReadAsByteArrayAsync());
            }
            finally { File.Delete(path); }
            await using (var db = context())
            {
                var allocation = await db.CourtAllocations.SingleAsync(x => x.CourtId == courtAId);
                allocation.Status = "RELEASED"; allocation.ReleasedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
            var refreshed = await Data(client, $"/api/v1/venues/{venueId}/availability?date={date}");
            Assert.Equal("AVAILABLE", refreshed.GetProperty("courts").EnumerateArray()
                .Single(c => c.GetProperty("courtId").GetGuid() == courtAId)
                .GetProperty("slots")[2].GetProperty("status").GetString());
        });
    }

    private static Venue Venue(Guid businessId, string name, double latitude, double longitude, string status) =>
        new() { BusinessId = businessId, Name = name, Address = "Hà Nội", Contact = "Venue service contact",
            Latitude = latitude, Longitude = longitude, Timezone = "Asia/Ho_Chi_Minh", Status = status,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    private static Court Court(Guid venueId, string name) =>
        new() { VenueId = venueId, Name = name, Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
    private static DateTimeOffset LocalUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    private static async Task<JsonElement> Data(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {response.StatusCode} {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }
    private static async Task Problem(HttpClient client, string path, HttpStatusCode status, string code)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
    private static async Task WithDatabase(Func<Func<ShuttleBookDbContext>, string, Task> run)
    {
        var supplied = Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(supplied));
        var settings = LocalPostgresTestServer.Require(supplied);
        settings.Timeout = 15; settings.CommandTimeout = 30;
        var name = $"shuttlebook_f04_test_{Guid.NewGuid():N}";
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
            await run(Context, connection);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {quoted} WITH (FORCE)", control);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class ApiFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShuttleBook"] = connection,
                ["Identity:JwtSigningKey"] = "f04-test-jwt-signing-key-more-than-32-bytes",
                ["Identity:OtpPepper"] = "f04-test-otp-pepper-more-than-32-bytes"
            }));
            builder.ConfigureLogging(log => log.ClearProviders());
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
