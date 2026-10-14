using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task Court_numbers_are_scoped_to_each_venue_and_aliases_cannot_duplicate()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (businessId, firstVenue, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            var secondVenue = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses/{businessId}/venues", VenueBody()), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            foreach (var venue in new[] { firstVenue, secondVenue })
            {
                foreach (var raw in new[] { "01", "sÂn 02", "10" })
                {
                    var created = await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = raw }), HttpStatusCode.Created);
                    Assert.Equal(venue, created.GetProperty("venueId").GetGuid());
                    Assert.Equal(CourtNumbers.Name(raw == "01" ? 1 : raw == "10" ? 10 : 2), created.GetProperty("name").GetString());
                }
                foreach (var raw in new[] { "1", "Sân 01", "0001" })
                    await Problem(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = raw }), HttpStatusCode.Conflict, "COURT_NUMBER_EXISTS");
            }
            await using (var db = context())
            {
                db.Courts.Add(new Court { VenueId = firstVenue, Name = "Sân 0003", CreatedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            await Problem(await owner.PostAsJsonAsync($"{Root}/venues/{firstVenue}/courts", new { name = "3" }), HttpStatusCode.Conflict, "COURT_NUMBER_EXISTS");
            var profile = await Data(await owner.GetAsync($"{Root}/businesses/{businessId}"));
            var venues = profile.GetProperty("venues").EnumerateArray().ToArray();
            Assert.Equal(2, venues.Length);
            Assert.Equal(new[] { "Sân 1", "Sân 2", "Sân 0003", "Sân 10", "Existing Court" }, Names(venues.Single(v => v.GetProperty("id").GetGuid() == firstVenue).GetProperty("courts")));
            Assert.Equal(new[] { "Sân 1", "Sân 2", "Sân 10" }, Names(venues.Single(v => v.GetProperty("id").GetGuid() == secondVenue).GetProperty("courts")));
        });
    }

    [Fact]
    public async Task Court_number_validation_rename_and_state_guards_preserve_identity_and_booking_snapshot()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (businessId, venueId, courtId) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            using var other = await Login(anonymous, factory, "addition-other@example.test");
            using var customer = await Login(anonymous, factory, "addition-customer@example.test");
            foreach (var invalid in new string?[] { null, "", "Court A", "Sân A", "0", "-1", "1.5", "+1", "1e2", "١", "１", "2147483648" })
            {
                await Problem(await owner.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = invalid }), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
                using var rename = VersionedPut($"{Root}/courts/{courtId}", new { name = invalid }, 1);
                await Problem(await owner.SendAsync(rename), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            }
            await Problem(await other.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "1" }), HttpStatusCode.NotFound, "NOT_FOUND");
            using (var rename = VersionedPut($"{Root}/courts/{courtId}", new { name = "1" }, 1))
                await Problem(await other.SendAsync(rename), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await customer.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "1" }), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await owner.PutAsJsonAsync($"{Root}/courts/{courtId}", new { name = "1" }), (HttpStatusCode)428, "PRECONDITION_REQUIRED");
            using (var rename = VersionedPut($"{Root}/courts/{courtId}", new { name = "1" }, 99))
                await Problem(await owner.SendAsync(rename), HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
            using (var schedule = VersionedPut($"/api/v1/operator/courts/{courtId}/schedule", ScheduleBody(), 1))
                await Data(await owner.SendAsync(schedule));
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").Date).AddDays(1);
            while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
            var quote = await Data(await customer.PostAsJsonAsync("/api/v1/availability/quote", new { courtId, date = date.ToString("yyyy-MM-dd"), startsAt = "08:00", endsAt = "10:00" }));
            using var createBooking = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings")
            { Content = JsonContent.Create(new { courtId, quoteId = quote.GetProperty("quoteId").GetGuid(), startsAt = quote.GetProperty("startsAt").GetDateTimeOffset(), endsAt = quote.GetProperty("endsAt").GetDateTimeOffset() }) };
            createBooking.Headers.Add("Idempotency-Key", "court-number-identity");
            await Data(await customer.SendAsync(createBooking), HttpStatusCode.Created);
            using (var rename = VersionedPut($"{Root}/courts/{courtId}", new { name = " SÂN 001 " }, 2))
            {
                var changed = await Data(await owner.SendAsync(rename));
                Assert.Equal(courtId, changed.GetProperty("id").GetGuid());
                Assert.Equal("Sân 1", changed.GetProperty("name").GetString());
            }
            var second = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venueId}/courts", new { name = "2" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            await Problem(await owner.PutAsJsonAsync($"{Root}/courts/{second}", new { name = "01" }), HttpStatusCode.Conflict, "COURT_NUMBER_EXISTS");
            using (var rename = VersionedPut($"{Root}/courts/{courtId}", new { name = "2" }, 3))
                await Problem(await owner.SendAsync(rename), HttpStatusCode.Conflict, "COURT_NUMBER_EXISTS");
            using (var rename = VersionedPut($"{Root}/courts/{courtId}", new { name = "01" }, 3))
                await Data(await owner.SendAsync(rename));
            await Data(await DraftSchedulePut(owner, second, ScheduleBody(), 1));
            await using (var pending = context()) { (await pending.Courts.FindAsync(second))!.Status = "PENDING_APPROVAL"; await pending.SaveChangesAsync(); }
            await Problem(await owner.PutAsJsonAsync($"{Root}/courts/{second}", new { name = "3" }), HttpStatusCode.Conflict, "STATE_CONFLICT");
            await using var db = context();
            var original = (await db.Courts.FindAsync(courtId))!;
            Assert.Equal(venueId, original.VenueId);
            Assert.Equal(businessId, (await db.Venues.FindAsync(original.VenueId))!.BusinessId);
            var booking = Assert.Single(await db.Bookings.ToListAsync());
            Assert.Equal(courtId, booking.CourtId); Assert.Equal(venueId, booking.VenueId);
            Assert.Equal("Existing Court", booking.CourtName);
        });
    }

    [Fact]
    public async Task Concurrent_alias_creates_and_renames_only_keep_one_court_number()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, venue, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            var creates = await Task.WhenAll(owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "05" }), owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "Sân 5" }));
            await Data(Assert.Single(creates, r => r.StatusCode == HttpStatusCode.Created), HttpStatusCode.Created);
            await Problem(Assert.Single(creates, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "COURT_NUMBER_EXISTS");
            var first = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "1" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var second = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts", new { name = "2" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var renames = await Task.WhenAll(owner.PutAsJsonAsync($"{Root}/courts/{first}", new { name = "3" }), owner.PutAsJsonAsync($"{Root}/courts/{second}", new { name = "Sân 03" }));
            await Data(Assert.Single(renames, r => r.StatusCode == HttpStatusCode.OK));
            await Problem(Assert.Single(renames, r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "COURT_NUMBER_EXISTS");
            await using var db = context();
            Assert.Equal(1, await db.Courts.CountAsync(c => c.VenueId == venue && c.Name == "Sân 3"));
            Assert.Equal(1, await db.Courts.CountAsync(c => c.VenueId == venue && c.Name == "Sân 5"));
        });
    }

    [Fact]
    public async Task Public_detail_and_availability_sort_numeric_courts_and_preserve_legacy_names()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, venue, _) = await ActiveInventory(context);
            await using (var db = context())
            {
                foreach (var name in new[] { "Sân 10", "Sân 2", "Sân 1" }) db.Courts.Add(new Court { VenueId = venue, Name = name, Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow });
                db.Courts.Add(new Court { VenueId = venue, Name = "Sân 3", Status = "INACTIVE", CreatedAt = DateTimeOffset.UtcNow });
                db.Courts.Add(new Court { VenueId = venue, Name = "Sân 4", Status = "PENDING_APPROVAL", CreatedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var publicClient = factory.CreateClient();
            var detail = await Data(await publicClient.GetAsync($"/api/v1/venues/{venue}"));
            Assert.Equal(new[] { "Sân 1", "Sân 2", "Sân 10", "Existing Court" }, Names(detail.GetProperty("courts")));
            var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd");
            var availability = await Data(await publicClient.GetAsync($"/api/v1/venues/{venue}/availability?date={date}"));
            Assert.Equal(new[] { "Sân 1", "Sân 2", "Sân 10", "Existing Court" }, Names(availability.GetProperty("courts")));
        });
    }

    private static string[] Names(JsonElement courts) => courts.EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToArray();
}
