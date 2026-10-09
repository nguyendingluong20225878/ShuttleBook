using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class FixedSeriesTests
{
    [Fact]
    public async Task Casual_total_accepts_ten_million_and_rejects_one_dong_over_without_new_writes()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        await using (var db = f.Context())
        {
            foreach (var rule in await db.PricingRules.ToArrayAsync()) rule.PricePerSlot = 5_000_000;
            await db.SaveChangesAsync();
        }
        var atLimit = await CasualQuote(f, customer, "20:00", "21:00");
        Assert.Equal("10000000", atLimit.GetProperty("amountExact").GetString());
        Assert.All(atLimit.GetProperty("slots").EnumerateArray(), slot => Assert.Equal("5000000", slot.GetProperty("pricePerSlotExact").GetString()));
        await Data(await CasualCreate(customer, atLimit, "limit-boundary"), 201);
        await using (var db = f.Context())
        {
            foreach (var rule in await db.PricingRules.ToArrayAsync()) rule.PricePerSlot = 5_000_001;
            await db.SaveChangesAsync();
        }
        await Code(await CasualQuoteResponse(f, customer, "18:00", "19:00"), 409, "AMOUNT_LIMIT_EXCEEDED");
        await using var check = f.Context(); Assert.Single(await check.Bookings.ToArrayAsync());
        Assert.Single(await check.BookingPayments.ToArrayAsync());
        Assert.Single(await check.QuoteReservations.ToArrayAsync());
    }

    [Fact]
    public async Task Fixed_total_over_limit_is_rejected_even_when_each_occurrence_is_below_limit()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        await using (var db = f.Context())
        {
            foreach (var rule in await db.PricingRules.ToArrayAsync()) rule.PricePerSlot = 700_000;
            await db.SaveChangesAsync();
        }
        await Code(await customer.PostAsJsonAsync("/api/v1/booking-series/quote", Input(f)), 409, "AMOUNT_LIMIT_EXCEEDED");
        await using var check = f.Context(); Assert.Empty(await check.QuoteReservations.ToArrayAsync());
        Assert.Empty(await check.CourtAllocations.ToArrayAsync()); Assert.Empty(await check.BookingPayments.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_quotes_on_two_courts_for_same_customer_create_only_one_hold(bool fixedSecond)
    {
        await using var f = await Fixture.Create(); using var first = await f.Client("customer");
        using var secondHost = new ApiFactory(f.Connection, f.Clock); using var second = secondHost.CreateClient();
        await f.Login(second, "customer"); Guid otherCourt;
        await using (var db = f.Context())
        {
            var court = new Court { VenueId = f.VenueId, Name = "Second court", Status = "ACTIVE", CreatedAt = f.Clock.GetUtcNow() };
            db.Courts.Add(court); otherCourt = court.Id;
            for (var day = 0; day < 7; day++)
            {
                db.CourtOperatingHours.Add(new CourtOperatingHour { CourtId = court.Id, DayOfWeek = day, OpensAt = new(17, 0), ClosesAt = new(22, 0) });
                db.PricingRules.Add(new PricingRule { CourtId = court.Id, DayOfWeek = day, StartsAt = new(17, 0), EndsAt = new(22, 0), PricePerSlot = 100_000 });
            }
            await db.SaveChangesAsync();
        }
        var secondQuote = fixedSecond
            ? second.PostAsJsonAsync("/api/v1/booking-series/quote", new { courtId = otherCourt, dayOfWeek = Weekday(f), localStartTime = "18:00",
                durationMinutes = 120, startsOn = f.Date, endsOn = DateOnly.Parse(f.Date).AddMonths(1).ToString("yyyy-MM-dd") })
            : second.PostAsJsonAsync("/api/v1/availability/quote", new { courtId = otherCourt, date = f.Date, startsAt = "18:00", endsAt = "20:00" });
        var responses = await Task.WhenAll(CasualQuoteResponse(f, first), secondQuote);
        Assert.Single(responses, response => (int)response.StatusCode == 200);
        await Code(Assert.Single(responses, response => (int)response.StatusCode == 409), 409, "ACTIVE_QUOTE_EXISTS");
        await using var check = f.Context(); Assert.Single(await check.QuoteReservations.Where(x => x.ReleasedAt == null && x.ConsumedAt == null).ToArrayAsync());
        var allocations = await check.CourtAllocations.Where(x => x.Status == "RESERVED").ToArrayAsync();
        Assert.NotEmpty(allocations); Assert.Single(allocations.Select(x => x.QuoteReservationId).Distinct());
    }
}
