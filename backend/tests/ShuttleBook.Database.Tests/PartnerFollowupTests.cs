using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task Partner_followup_court_numbers_save_atomically_and_contact_updates_public_venue()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (business, venue, first) = await ActiveInventory(context);
            Guid second;
            await using (var db = context())
            {
                var court = new Court { VenueId = venue, Name = "Sân 2", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
                second = court.Id; db.Courts.Add(court);
                (await db.Courts.FindAsync(first))!.Name = "Sân 1";
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            using var other = await Login(anon, factory, "addition-other@example.test");
            var path = $"{Root}/venues/{venue}/court-numbers";
            object Swap(long firstVersion = 1, int secondNumber = 1) => new
            { courts = new[] { new { id = first, version = firstVersion, number = 2 },
                new { id = second, version = 1L, number = secondNumber } } };
            await Problem(await other.PutAsJsonAsync(path, Swap()), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await owner.PutAsJsonAsync(path, Swap(secondNumber: 2)), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await owner.PutAsJsonAsync(path, Swap(firstVersion: 999)), HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            var changed = await Data(await owner.PutAsJsonAsync(path, Swap()));
            Assert.Equal(2, changed.GetProperty("courts").GetArrayLength());
            await using (var check = context())
            {
                Assert.Equal("Sân 2", (await check.Courts.FindAsync(first))!.Name);
                Assert.Equal("Sân 1", (await check.Courts.FindAsync(second))!.Name);
            }
            await Problem(await owner.PutAsJsonAsync(path, Swap()), HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            var detail = await Data(await owner.GetAsync($"{Root}/businesses/{business}"));
            var currentVenue = detail.GetProperty("venues").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == venue);
            var version = currentVenue.GetProperty("version").GetInt64();
            using (var update = VersionedPut($"{Root}/venues/{venue}/contact", new { contact = "0901234567" }, version))
                await Data(await owner.SendAsync(update));
            var publicVenue = await Data(await anon.GetAsync($"/api/v1/venues/{venue}"));
            Assert.Equal("0901234567", publicVenue.GetProperty("contact").GetString());
        });
    }

    [Fact]
    public async Task Partner_followup_weekly_and_single_special_price_match_public_each_court()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, venue, first) = await ActiveInventory(context);
            Guid second;
            await using (var db = context())
            {
                var court = new Court { VenueId = venue, Name = "Sân 2", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
                second = court.Id; db.Courts.Add(court); await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            async Task SaveSchedule(Guid court, long wednesdayPrice)
            {
                using var put = VersionedPut($"/api/v1/operator/courts/{court}/schedule", new
                {
                    hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "10:00" },
                        new { dayOfWeek = 3, opensAt = "08:00", closesAt = "10:00" } },
                    prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "10:00", pricePerSlot = 100000L },
                        new { dayOfWeek = 3, startsAt = "08:00", endsAt = "10:00", pricePerSlot = wednesdayPrice } }
                }, 1);
                await Data(await owner.SendAsync(put));
            }
            await SaveSchedule(first, 40000);
            await SaveSchedule(second, 100000);
            var monday = NextMonday();
            var wednesday = monday.AddDays(2);
            async Task<long> PublicPrice(Guid court, DateOnly date)
            {
                var availability = await Data(await anon.GetAsync($"/api/v1/venues/{venue}/availability?date={date:yyyy-MM-dd}"));
                var row = availability.GetProperty("courts").EnumerateArray().Single(x => x.GetProperty("courtId").GetGuid() == court);
                return row.GetProperty("slots").EnumerateArray().First(x => x.GetProperty("startsAt").GetString() == "08:00").GetProperty("pricePerSlot").GetInt64();
            }
            Assert.Equal(40000, await PublicPrice(first, wednesday));
            Assert.Equal(100000, await PublicPrice(second, wednesday));
            using (var update = VersionedPut($"/api/v1/operator/courts/{first}/schedule", new
            {
                hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "10:00" },
                    new { dayOfWeek = 3, opensAt = "08:00", closesAt = "10:00" } },
                prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "10:00", pricePerSlot = 100000L },
                    new { dayOfWeek = 3, startsAt = "08:00", endsAt = "10:00", pricePerSlot = 100000L } }
            }, 2)) await Data(await owner.SendAsync(update));
            Assert.Equal(100000, await PublicPrice(first, wednesday));
            var path = $"/api/v1/operator/courts/{first}/special-price";
            using (var special = VersionedPut(path, new { specialPrice = new
            { startsOn = monday, endsOn = monday.AddDays(6), startsAt = "08:00", endsAt = "10:00", pricePerSlot = 120000 } }, 3))
                await Data(await owner.SendAsync(special));
            Assert.Equal(120000, await PublicPrice(first, monday));
            Assert.Equal(120000, await PublicPrice(first, wednesday));
            Assert.Equal(100000, await PublicPrice(first, wednesday.AddDays(7)));
            Assert.Equal(100000, await PublicPrice(second, wednesday));
            var specialAvailability = await Data(await anon.GetAsync($"/api/v1/venues/{venue}/availability?date={wednesday:yyyy-MM-dd}"));
            var firstSlots = specialAvailability.GetProperty("courts").EnumerateArray()
                .Single(x => x.GetProperty("courtId").GetGuid() == first).GetProperty("slots");
            Assert.Equal("SPECIAL", firstSlots.EnumerateArray().First().GetProperty("priceSource").GetString());
            var operations = await Data(await owner.GetAsync($"/api/v1/operator/courts/{first}/operations"));
            Assert.False(operations.GetProperty("legacySpecialPrices").GetBoolean());
            Assert.Equal(monday.ToString("yyyy-MM-dd"), operations.GetProperty("specialPrice").GetProperty("startsOn").GetString());
            using (var invalid = VersionedPut(path, new { specialPrice = new
            { startsOn = monday, endsOn = monday.AddDays(6), startsAt = "07:00", endsAt = "10:00", pricePerSlot = 99999 } }, 4))
                await Problem(await owner.SendAsync(invalid), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            Assert.Equal(120000, await PublicPrice(first, wednesday));
        });
    }

    [Fact]
    public async Task Partner_followup_personal_profile_persists_only_display_name()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var customer = await Login(anon, factory, "addition-customer@example.test");
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            using var admin = await Login(anon, factory, "addition-admin@example.test", true);
            var path = "/api/v1/auth/profile";
            await Problem(await anon.GetAsync(path), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
            await Problem(await admin.GetAsync(path), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await customer.PutAsJsonAsync(path, new { displayName = "A", phone = "0123456789" }),
                HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
            var before = await Data(await customer.GetAsync(path));
            var saved = await Data(await customer.PutAsJsonAsync(path, new { displayName = "Khách thử nghiệm" }));
            Assert.Equal(before.GetProperty("contact").GetString(), saved.GetProperty("contact").GetString());
            Assert.Equal("Khách thử nghiệm", (await Data(await customer.GetAsync(path))).GetProperty("displayName").GetString());
            await Data(await owner.PutAsJsonAsync(path, new { displayName = "Chủ sân thử nghiệm" }));
            Assert.Equal("Chủ sân thử nghiệm", (await Data(await owner.GetAsync(path))).GetProperty("displayName").GetString());
        });
    }
}
