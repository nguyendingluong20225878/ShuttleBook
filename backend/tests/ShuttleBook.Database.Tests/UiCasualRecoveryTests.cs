using Microsoft.EntityFrameworkCore;

namespace ShuttleBook.Database.Tests;

public sealed partial class FixedSeriesTests
{
    [Fact]
    public async Task UI_casual_lost_response_replay_after_quote_TTL_returns_original_booking_and_allocation()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var quote = await CasualQuote(f, customer);
        // The backend commits, but the UI may never receive this response body.
        using (var ignored = await CasualCreate(customer, quote, "ui-casual-lost-response"))
            Assert.Equal(System.Net.HttpStatusCode.Created, ignored.StatusCode);
        Guid bookingId, allocationId;
        await using (var db = f.Context())
        {
            var booking = Assert.Single(await db.Bookings.ToArrayAsync()); bookingId = booking.Id; allocationId = booking.AllocationId;
        }
        f.Clock.Set(quote.GetProperty("expiresAt").GetDateTimeOffset().AddSeconds(1));
        var replay = await Data(await CasualCreate(customer, quote, "ui-casual-lost-response"), 201);
        Assert.Equal(bookingId, Id(replay)); Assert.Equal("AWAITING_TRANSFER", replay.GetProperty("status").GetString());
        await using var check = f.Context(); Assert.Single(await check.Bookings.ToArrayAsync()); Assert.Single(await check.BookingPayments.ToArrayAsync());
        Assert.Single(await check.BookingIdempotency.ToArrayAsync());
        var allocation = Assert.Single(await check.CourtAllocations.ToArrayAsync()); Assert.Equal(allocationId, allocation.Id);
        Assert.Equal("BOOKING", allocation.Kind); Assert.Equal("RESERVED", allocation.Status);
        Assert.NotNull((await check.QuoteReservations.SingleAsync()).ConsumedAt);
    }
}
