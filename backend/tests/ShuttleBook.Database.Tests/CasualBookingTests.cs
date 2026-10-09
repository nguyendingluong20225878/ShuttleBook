using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed class CasualBookingTests
{
    [Theory]
    [InlineData(60, 120)]
    [InlineData(90, 90)]
    public async Task Casual_half_hour_extensions_after_minimum_quote_create_replay_and_preserve_policy(int blockMinutes, int minimumMinutes)
    {
        await using var f = await Fixture.Create();
        using var client = f.Factory.CreateClient();
        await f.Login(client, "customer");
        await using (var db = f.Context())
        {
            var court = await db.Courts.SingleAsync();
            court.BookingBlockMinutes = blockMinutes;
            court.MinimumBookingMinutes = minimumMinutes;
            await db.SaveChangesAsync();
        }

        // Keep the configured minimum, but allow each extra half hour once it
        // is reached: 4/5/6/7 slots for min120, and 3/4/5/6 for min90.
        var firstDate = DateOnly.Parse(f.Date).AddDays(1);
        var minimumSlots = minimumMinutes / 30;
        var start = new TimeOnly(17, 0);
        async Task<HttpResponseMessage> Quote(DateOnly date, string from, string to) =>
            await client.PostAsJsonAsync("/api/v1/availability/quote", new {
                courtId = f.CourtId, date = date.ToString("yyyy-MM-dd"), startsAt = from, endsAt = to
            });

        await Code(await Quote(firstDate, "17:00", start.AddMinutes(minimumMinutes - 30).ToString("HH:mm")), 400, "VALIDATION_FAILED");
        await Code(await Quote(firstDate, "17:15", "20:00"), 400, "VALIDATION_FAILED");
        await Code(await Quote(firstDate, "17:00", "20:15"), 400, "VALIDATION_FAILED");
        await Code(await Quote(firstDate, "20:00", "22:30"), 409, "PRICE_UNAVAILABLE");

        for (var extra = 0; extra < 4; extra++)
        {
            var count = minimumSlots + extra;
            var date = firstDate.AddDays(extra);
            var end = start.AddMinutes(count * 30);
            var quote = await Data(await Quote(date, "17:00", end.ToString("HH:mm")));
            var expectedAmount = count * 100000L;
            Assert.Equal(count, quote.GetProperty("slots").GetArrayLength());
            Assert.Equal(expectedAmount, quote.GetProperty("amount").GetInt64());
            Assert.Equal(blockMinutes, quote.GetProperty("bookingBlockMinutes").GetInt32());
            Assert.Equal(minimumMinutes, quote.GetProperty("minimumBookingMinutes").GetInt32());
            var slots = quote.GetProperty("slots").EnumerateArray().ToArray();
            for (var index = 0; index < count; index++)
            {
                Assert.Equal(start.AddMinutes(index * 30).ToString("HH:mm"), slots[index].GetProperty("startsAt").GetString());
                Assert.Equal(start.AddMinutes((index + 1) * 30).ToString("HH:mm"), slots[index].GetProperty("endsAt").GetString());
                Assert.Equal(100000L, slots[index].GetProperty("pricePerSlot").GetInt64());
            }

            var key = $"half-hour-extension-{blockMinutes}-{count}";
            var created = await Data(await f.CreateBooking(client, quote, key), 201);
            var replay = await Data(await f.CreateBooking(client, quote, key), 201);
            var id = created.GetProperty("bookingId").GetGuid();
            Assert.Equal(id, replay.GetProperty("bookingId").GetGuid());
            Assert.Equal(expectedAmount, created.GetProperty("amount").GetInt64());
            Assert.Equal(expectedAmount, replay.GetProperty("amount").GetInt64());
            Assert.Equal("AWAITING_TRANSFER", created.GetProperty("status").GetString());
            Assert.Equal(end.ToString("HH:mm"), created.GetProperty("localEnd").GetString());
            await using (var db = f.Context())
            {
                var booking = await db.Bookings.SingleAsync(x => x.Id == id);
                Assert.Equal(blockMinutes, booking.BookingBlockMinutes);
                Assert.Equal(minimumMinutes, booking.MinimumBookingMinutes);
                Assert.Equal(count, JsonDocument.Parse(booking.Slots).RootElement.GetArrayLength());
                Assert.Equal(expectedAmount, (await db.BookingPayments.SingleAsync(x => x.BookingId == id)).ExpectedAmount);
                var allocation = await db.CourtAllocations.SingleAsync(x => x.Id == booking.AllocationId);
                Assert.Equal("RESERVED", allocation.Status);
                Assert.Equal(count * 30, (allocation.EndsAt - allocation.StartsAt).TotalMinutes);
            }
            await Code(await Quote(date, "17:00", end.ToString("HH:mm")), 409, "SLOT_UNAVAILABLE");
        }

        // Block remains part of the quote fingerprint even though duration
        // divisibility no longer limits casual booking.
        var beforePolicyChange = await Data(await Quote(firstDate.AddDays(5), "17:00", start.AddMinutes(minimumMinutes + 30).ToString("HH:mm")));
        await using (var db = f.Context())
        {
            var court = await db.Courts.SingleAsync();
            court.BookingBlockMinutes = 30;
            await db.SaveChangesAsync();
        }
        await Code(await f.CreateBooking(client, beforePolicyChange, "block-fingerprint"), 409, "QUOTE_CHANGED");
        await using (var db = f.Context())
        {
            Assert.Equal(4, await db.Bookings.CountAsync());
            Assert.Equal(4, await db.BookingPayments.CountAsync());
            Assert.Equal(4, await db.CourtAllocations.CountAsync(x => x.Kind == "BOOKING"));
            var held = Assert.Single(await db.CourtAllocations.Where(x => x.Kind == "QUOTE_HOLD" && x.Status == "RESERVED").ToArrayAsync());
            Assert.Equal(beforePolicyChange.GetProperty("quoteId").GetGuid(), held.QuoteReservationId);
            Assert.Equal(4, await db.BookingIdempotency.CountAsync());
            Assert.Equal(4, await db.OutboxMessages.CountAsync(x => x.EventType == "BOOKING_CREATED"));
        }
    }

    [Fact]
    public async Task Quotes_validation_policy_snapshot_auth_QR_and_idempotent_replay_use_real_database()
    {
        await using var f = await Fixture.Create();
        using var client = f.Factory.CreateClient();
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId=f.CourtId, date=f.Date, startsAt="18:00", endsAt="20:00" }), 401, "UNAUTHORIZED");
        await f.Login(client, "customer");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId=f.CourtId, date=f.Date, startsAt="16:00", endsAt="17:00" }), 409, "PRICE_UNAVAILABLE");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId=f.CourtId, date=f.Date, startsAt="18:10", endsAt="19:00" }), 400, "VALIDATION_FAILED");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId=f.CourtId, date=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd"), startsAt="18:00", endsAt="19:00" }), 400, "VALIDATION_FAILED");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId=f.CourtId, date=f.Date, startsAt="18:00", endsAt="19:00", amount=1 }), 400, "UNSUPPORTED_FIELD");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId=f.CourtId, date=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(70).ToString("yyyy-MM-dd"), startsAt="18:00", endsAt="19:00" }), 400, "VALIDATION_FAILED");
        var q = await f.Quote(client, "18:00", "20:00");
        Assert.Equal(600000, q.GetProperty("amount").GetInt64());
        Assert.Equal(4, q.GetProperty("slots").GetArrayLength());
        Assert.Equal(11, q.GetProperty("startsAt").GetDateTimeOffset().Hour);
        Assert.DoesNotContain("accountNumber", q.GetRawText());
        await using (var db = f.Context()) Assert.Equal("QUOTE_HOLD", Assert.Single(await db.CourtAllocations.ToListAsync()).Kind);
        using (var guest = f.Factory.CreateClient()) await Code(await f.CreateBooking(guest, q, "guest"), 401, "UNAUTHORIZED");
        var created = await Data(await f.CreateBooking(client, q, "first"), 201);
        var id = created.GetProperty("bookingId").GetGuid();
        Assert.Equal("AWAITING_TRANSFER", created.GetProperty("status").GetString());
        Assert.DoesNotContain("objectKey", created.GetRawText());
        Assert.Equal("******7890", created.GetProperty("payment").GetProperty("maskedAccountNumber").GetString());
        var replay = await Data(await f.CreateBooking(client, q, "first"), 201);
        Assert.Equal(id, replay.GetProperty("bookingId").GetGuid());
        await Code(await f.CreateBooking(client, q, "first", Guid.NewGuid()), 409, "IDEMPOTENCY_KEY_REUSED");
        using (var qr = await client.GetAsync($"/api/v1/bookings/{id}/qr"))
        { Assert.Equal(HttpStatusCode.OK, qr.StatusCode); Assert.Equal(f.Bytes, await qr.Content.ReadAsByteArrayAsync()); }
        var list = await Data(await client.GetAsync("/api/v1/me/bookings"));
        Assert.Single(list.GetProperty("items").EnumerateArray()); Assert.DoesNotContain("account", list.GetRawText());
        await f.Login(client, "other");
        await Code(await client.GetAsync($"/api/v1/bookings/{id}"), 404, "NOT_FOUND");
        await Code(await client.GetAsync($"/api/v1/bookings/{id}/qr"), 404, "NOT_FOUND");
        await f.Login(client, "operator"); await Code(await client.GetAsync("/api/v1/me/bookings"), 403, "FORBIDDEN");
        await f.Login(client, "customer");
        await using (var db = f.Context())
        {
            var account = await db.VenuePaymentAccounts.SingleAsync(); account.AccountName="NEW OWNER";
            var newQr = new MediaUpload { OwnerUserId=f.OwnerId, VenueId=f.VenueId, Purpose="QR", Status="READY",
                ObjectKey="new-qr", ContentType="image/png", SizeBytes=1, Sha256Base64=Convert.ToBase64String(SHA256.HashData([1])), CreatedAt=DateTimeOffset.UtcNow };
            db.MediaUploads.Add(newQr); account.QrUploadId=newQr.Id;
            (await db.PricingRules.Where(x=>x.Priority==0).FirstAsync()).PricePerSlot=300000;
            await db.SaveChangesAsync();
        }
        var detail = await Data(await client.GetAsync($"/api/v1/bookings/{id}"));
        Assert.Equal(600000, detail.GetProperty("amount").GetInt64());
        Assert.Equal("TEST CLUB", detail.GetProperty("payment").GetProperty("accountName").GetString());
        using (var qr = await client.GetAsync($"/api/v1/bookings/{id}/qr")) Assert.Equal(f.Bytes, await qr.Content.ReadAsByteArrayAsync());
        await using (var db = f.Context())
        { Assert.Equal(1, await db.Bookings.CountAsync()); Assert.Equal(1, await db.BookingPayments.CountAsync()); Assert.Equal(1, await db.BookingIdempotency.CountAsync()); Assert.Equal(1, await db.OutboxMessages.CountAsync()); }
        await using (var db = f.Context())
        { var user=await db.Users.SingleAsync(x=>x.Id==f.CustomerId); user.Status=UserStatus.Suspended; await db.SaveChangesAsync(); }
        await Code(await client.GetAsync($"/api/v1/bookings/{id}"), 401, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Twenty_overlapping_quote_requests_across_two_hosts_and_same_key_create_protect_one_aggregate()
    {
        await using var f = await Fixture.Create(); using var a=f.Factory.CreateClient(); using var hostB=new ApiFactory(f.Connection); using var b=hostB.CreateClient();
        await f.Login(a,"customer"); await f.Login(b,"other");
        var replies = await Task.WhenAll(Enumerable.Range(0,20).Select(i => (i%2==0?a:b).PostAsJsonAsync("/api/v1/availability/quote", new {courtId=f.CourtId,date=f.Date,startsAt="18:00",endsAt="20:00"})));
        var successful = new List<JsonElement>();
        for (var i=0;i<replies.Length;i++) {
            if (replies[i].StatusCode==HttpStatusCode.OK) successful.Add(await Data(replies[i]));
            else {
                using (replies[i]) {
                    Assert.Equal(HttpStatusCode.Conflict, replies[i].StatusCode);
                    using var error = JsonDocument.Parse(await replies[i].Content.ReadAsStringAsync());
                    Assert.Contains(error.RootElement.GetProperty("code").GetString(), new[] { "SLOT_UNAVAILABLE", "ACTIVE_QUOTE_EXISTS" });
                }
            }
        }
        Assert.NotEmpty(successful); Assert.True(successful.Count<20);
        Guid ownerId; Guid currentQuote;
        await using(var db=f.Context()) {
            var hold=Assert.Single(await db.QuoteReservations.Where(x=>x.ReleasedAt==null && x.ConsumedAt==null).ToArrayAsync()); ownerId=hold.CustomerId;currentQuote=hold.Id;
            Assert.Single(await db.CourtAllocations.Where(x=>x.Status=="RESERVED").ToArrayAsync());Assert.Empty(await db.Bookings.ToArrayAsync());
        }
        var active=successful.Single(q=>q.GetProperty("quoteId").GetGuid()==currentQuote);var winner=ownerId==f.CustomerId?a:b;
        var create = await Task.WhenAll(Enumerable.Range(0,8).Select(_=>f.CreateBooking(winner,active,"same-key")));
        var ids=new List<Guid>();foreach(var response in create)ids.Add((await Data(response,201)).GetProperty("bookingId").GetGuid());Assert.Single(ids.Distinct());
        var adjacent=await f.Quote(winner,"20:00","21:00");await Data(await f.CreateBooking(winner,adjacent,"adjacent"),201);
        await using var check=f.Context(); Assert.Equal(2,await check.Bookings.CountAsync());Assert.Equal(2,await check.BookingPayments.CountAsync());Assert.Equal(2,await check.CourtAllocations.CountAsync(x=>x.Status=="RESERVED"));
    }

    [Fact]
    public async Task Expired_changed_quotes_maintenance_half_open_policy_QR_and_missing_price_are_checked()
    {
        await using var f=await Fixture.Create(); using var client=f.Factory.CreateClient(); await f.Login(client,"customer");
        async Task ReleaseCurrentHold()
        {
            await using var db = f.Context();
            var active = await db.QuoteReservations.Where(x => x.CustomerId == f.CustomerId && x.ReleasedAt == null && x.ConsumedAt == null).ToArrayAsync();
            foreach (var hold in active) await QuoteReservations.Release(db, hold, DateTimeOffset.UtcNow, CancellationToken.None);
            await db.SaveChangesAsync();
        }
        var q=await f.Quote(client,"18:00","19:00");
        await using(var db=f.Context()) { var quote=await db.BookingQuotes.SingleAsync(); quote.CreatedAt=DateTimeOffset.UtcNow.AddMinutes(-3); quote.ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1); var hold=await db.QuoteReservations.SingleAsync();hold.CreatedAt=quote.CreatedAt;hold.ExpiresAt=quote.ExpiresAt; await db.SaveChangesAsync(); }
        await Code(await f.CreateBooking(client,q,"expired"),409,"QUOTE_EXPIRED");
        q=await f.Quote(client,"18:00","19:00");
        await using(var db=f.Context()) { (await db.PricingRules.SingleAsync(x=>x.Priority==1)).PricePerSlot=210000; await db.SaveChangesAsync(); }
        await Code(await f.CreateBooking(client,q,"price-changed"),409,"QUOTE_CHANGED");
        await ReleaseCurrentHold();
        q=await f.Quote(client,"18:00","19:00");
        await using(var db=f.Context()) { var court=await db.Courts.SingleAsync(); court.HoldMinutes=25; await db.SaveChangesAsync(); }
        await Code(await f.CreateBooking(client,q,"changed"),409,"QUOTE_CHANGED");
        await ReleaseCurrentHold();
        await using(var db=f.Context()) { var court=await db.Courts.SingleAsync(); court.BookingBlockMinutes=60; court.MinimumBookingMinutes=60; await db.SaveChangesAsync(); }
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=f.Date,startsAt="18:00",endsAt="18:30"}),400,"VALIDATION_FAILED");
        await f.Quote(client,"18:00","19:00");
        await using(var db=f.Context()) { var court=await db.Courts.SingleAsync(); court.BookingBlockMinutes=90; court.MinimumBookingMinutes=90; await db.SaveChangesAsync(); }
        await ReleaseCurrentHold();
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=f.Date,startsAt="18:00",endsAt="19:00"}),400,"VALIDATION_FAILED");
        q=await f.Quote(client,"18:00","19:30");
        await using(var db=f.Context()) { var court=await db.Courts.SingleAsync(); court.BookingBlockMinutes=30; court.MinimumBookingMinutes=30;
            var hold=await db.QuoteReservations.SingleAsync(x=>x.Id==q.GetProperty("quoteId").GetGuid());hold.ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-1);hold.CreatedAt=hold.ExpiresAt.AddMinutes(-2);
            var oldQuote=await db.BookingQuotes.SingleAsync(x=>x.Id==hold.Id);oldQuote.ExpiresAt=hold.ExpiresAt;oldQuote.CreatedAt=hold.CreatedAt;await db.SaveChangesAsync();await QuoteReservations.CleanupCourt(db,f.CourtId,DateTimeOffset.UtcNow,CancellationToken.None);
            db.CourtAllocations.Add(new CourtAllocation { CourtId=f.CourtId, Kind="MAINTENANCE", StartsAt=q.GetProperty("startsAt").GetDateTimeOffset(), EndsAt=q.GetProperty("startsAt").GetDateTimeOffset().AddMinutes(30),CreatedAt=DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        await Code(await f.CreateBooking(client,q,"maintenance-old-quote"),409,"QUOTE_EXPIRED");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=f.Date,startsAt="18:00",endsAt="19:30"}),409,"SLOT_UNAVAILABLE");
        var free=await f.Quote(client,"18:30","19:00"); Assert.Equal(210000,free.GetProperty("amount").GetInt64());
        await Data(await f.CreateBooking(client,free,"free-after-maintenance"),201);
        Guid otherCourt;
        await using(var db=f.Context()) {
            var court=new Court {VenueId=f.VenueId,Name="Independent court",Status="ACTIVE",CreatedAt=DateTimeOffset.UtcNow};db.Courts.Add(court);otherCourt=court.Id;
            var day=(int)DateOnly.Parse(f.Date).DayOfWeek;
            db.CourtOperatingHours.Add(new CourtOperatingHour {CourtId=court.Id,DayOfWeek=day,OpensAt=new(17,0),ClosesAt=new(22,0)});
            db.PricingRules.Add(new PricingRule {CourtId=court.Id,DayOfWeek=day,StartsAt=new(17,0),EndsAt=new(22,0),PricePerSlot=100000});await db.SaveChangesAsync();
        }
        var otherQuote=await Data(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=otherCourt,date=f.Date,startsAt="18:00",endsAt="19:00"}));
        var independent=await Data(await f.CreateBooking(client,otherQuote,"independent-court"),201);
        await using(var db=f.Context()) {
            await using var tx=await db.Database.BeginTransactionAsync();var id=independent.GetProperty("bookingId").GetGuid();
            var ex=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bookings SET court_id={f.CourtId} WHERE id={id}"));Assert.Equal("23503",ex.SqlState);await tx.RollbackAsync();
        }
        long originalPrice;
        var quoteDay=(int)DateOnly.Parse(f.Date).DayOfWeek;
        await using(var db=f.Context()) { var rule=await db.PricingRules.SingleAsync(x=>x.CourtId==f.CourtId && x.DayOfWeek==quoteDay && x.Priority==0);originalPrice=rule.PricePerSlot;rule.PricePerSlot=long.MaxValue;await db.SaveChangesAsync(); }
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=f.Date,startsAt="20:00",endsAt="20:30"}),409,"PRICE_UNAVAILABLE");
        await using(var db=f.Context()) { (await db.PricingRules.SingleAsync(x=>x.CourtId==f.CourtId && x.DayOfWeek==quoteDay && x.Priority==0)).PricePerSlot=originalPrice;await db.SaveChangesAsync(); }
        await using(var db=f.Context()) { var qr=await db.MediaUploads.SingleAsync(); qr.Status="PENDING"; await db.SaveChangesAsync(); }
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=f.Date,startsAt="20:00",endsAt="21:00"}),409,"PAYMENT_SETUP_UNAVAILABLE");
        await using(var db=f.Context()) { var qr=await db.MediaUploads.SingleAsync(); qr.Status="READY"; db.PricingRules.RemoveRange(db.PricingRules); await db.SaveChangesAsync(); }
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=f.Date,startsAt="20:00",endsAt="21:00"}),409,"PRICE_UNAVAILABLE");
    }

    [Fact]
    public async Task Expiry_multiple_workers_preserves_reported_booking_and_upgrade_constraints()
    {
        await using var f=await Fixture.Create(); using var client=f.Factory.CreateClient(); await f.Login(client,"customer");
        var q=await f.Quote(client,"18:00","19:00"); var created=await Data(await f.CreateBooking(client,q,"expiry"),201); var id=created.GetProperty("bookingId").GetGuid();
        var later=DateTimeOffset.UtcNow.AddMinutes(25);
        var processed=await Task.WhenAll(Enumerable.Range(0,4).Select(async _=> { await using var db=f.Context(); return await BookingExpiry.ProcessOne(db,later,CancellationToken.None); }));
        Assert.Equal(1,processed.Count(x=>x));
        var detail=await Data(await client.GetAsync($"/api/v1/bookings/{id}")); Assert.Equal("EXPIRED",detail.GetProperty("status").GetString());
        var replay=await Data(await f.CreateBooking(client,q,"expiry"),201); Assert.Equal(id,replay.GetProperty("bookingId").GetGuid());
        var again=await f.Quote(client,"18:00","19:00"); var reported=await Data(await f.CreateBooking(client,again,"reported"),201); var reportId=reported.GetProperty("bookingId").GetGuid();
        await using(var db=f.Context())
        { await using var tx=await db.Database.BeginTransactionAsync(); var row=await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id={reportId} FOR UPDATE").SingleAsync(); row.Status="AWAITING_OWNER_CONFIRMATION";
            (await db.BookingPayments.SingleAsync(x=>x.BookingId==reportId)).Status="TRANSFER_REPORTED"; await db.SaveChangesAsync();
            await using var worker=f.Context(); Assert.False(await BookingExpiry.ProcessOne(worker,later,CancellationToken.None)); await tx.CommitAsync(); }
        await using(var db=f.Context()) { Assert.False(await BookingExpiry.ProcessOne(db,later,CancellationToken.None));
            Assert.Equal("RESERVED",(await db.CourtAllocations.SingleAsync(x=>x.Id==db.Bookings.Where(b=>b.Id==reportId).Select(b=>b.AllocationId).First())).Status);
            Assert.Equal(1,await db.OutboxMessages.CountAsync(x=>x.EventType=="BOOKING_EXPIRED")); await db.Database.MigrateAsync(); }
        await using(var db=f.Context())
        { await using var tx=await db.Database.BeginTransactionAsync(); var ex=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bookings SET ends_at=starts_at+interval '45 minutes' WHERE id={id}")); Assert.Equal("23514",ex.SqlState); await tx.RollbackAsync(); }
        await using(var db=f.Context())
        { await using var tx=await db.Database.BeginTransactionAsync(); var wrongVenue=Guid.NewGuid();var ex=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bookings SET venue_id={wrongVenue} WHERE id={id}")); Assert.Equal("23503",ex.SqlState); await tx.RollbackAsync(); }
        await using(var db=f.Context()) {
            (await db.Bookings.SingleAsync(x=>x.Id==reportId)).Status="CONFIRMED";
            var paid = await db.BookingPayments.SingleAsync(x=>x.BookingId==reportId);
            paid.Status="PAID"; paid.ConfirmedAmount=paid.ExpectedAmount; paid.ConfirmedBy=f.OwnerId; paid.ConfirmedAt=DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            Assert.False(await BookingExpiry.ProcessOne(db,later,CancellationToken.None));
            Assert.Equal("RESERVED",(await db.CourtAllocations.SingleAsync(x=>x.Id==db.Bookings.Where(b=>b.Id==reportId).Select(b=>b.AllocationId).First())).Status);
        }
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response,int status=200)
    { using(response) { var text=await response.Content.ReadAsStringAsync(); Assert.True((int)response.StatusCode==status,$"Expected {status}, got {(int)response.StatusCode}: {text}"); return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone(); } }
    private static async Task Code(HttpResponseMessage response,int status,string code)
    { using(response) { Assert.Equal(status,(int)response.StatusCode); Assert.Equal(code,JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString()); } }

    [Fact]
    public async Task Local_grid_supports_quarter_hour_timezone_and_rejects_DST_gaps_and_ambiguity()
    {
        await using var f=await Fixture.Create(); using var host=new ApiFactory(f.Connection,365);using var client=host.CreateClient(); await f.Login(client,"customer");
        await using(var db=f.Context()) { (await db.Venues.SingleAsync()).Timezone="Pacific/Chatham";await db.SaveChangesAsync(); }
        var q=await f.Quote(client,"18:00","19:00");Assert.Equal(15,q.GetProperty("startsAt").GetDateTimeOffset().Minute);
        await Data(await f.CreateBooking(client,q,"quarter-offset"),201);
        await using(var db=f.Context()) {
            (await db.Venues.SingleAsync()).Timezone="America/New_York";
            foreach(var hour in await db.CourtOperatingHours.ToListAsync()){hour.OpensAt=new(1,0);hour.ClosesAt=new(4,0);}
            db.PricingRules.RemoveRange(db.PricingRules);await db.SaveChangesAsync();
            for(var day=0;day<7;day++)db.PricingRules.Add(new PricingRule {CourtId=f.CourtId,DayOfWeek=day,StartsAt=new(1,0),EndsAt=new(4,0),PricePerSlot=100000});await db.SaveChangesAsync();
        }
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly Sunday(int year,int month,int nth){var first=new DateOnly(year,month,1);return first.AddDays((7-(int)first.DayOfWeek)%7+7*(nth-1));}
        var spring=Sunday(today.Year,3,2);if(spring<=today)spring=Sunday(today.Year+1,3,2);
        var fall=Sunday(today.Year,11,1);if(fall<=today)fall=Sunday(today.Year+1,11,1);
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=spring.ToString("yyyy-MM-dd"),startsAt="02:00",endsAt="03:00"}),409,"SCHEDULE_UNAVAILABLE");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=f.CourtId,date=fall.ToString("yyyy-MM-dd"),startsAt="01:00",endsAt="02:00"}),409,"SCHEDULE_UNAVAILABLE");
        await Code(await client.PostAsJsonAsync("/api/v1/availability/quote?extra=1",new {courtId=f.CourtId,date=f.Date,startsAt="01:00",endsAt="02:00"}),400,"VALIDATION_FAILED");
        // Changing the venue timezone does not move the earlier Chatham allocation.
        // Use New York 03:00–04:00 to test publication scope independently of that booking.
        var unpublishedQuote = await f.Quote(client,"03:00","04:00");
        await using(var db=f.Context()) { (await db.Venues.SingleAsync()).Status="SUSPENDED";await db.SaveChangesAsync(); }
        await Code(await f.CreateBooking(client,unpublishedQuote,"unpublished"),404,"NOT_FOUND");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public string Connection=""; public string Name=""; public NpgsqlConnection Control=null!; public ApiFactory Factory=null!;
        public Guid VenueId,CourtId,CustomerId,OwnerId,QrId; public string Date=""; public byte[] Bytes=[137,80,78,71,13,10,26,10]; private string path="";
        public ShuttleBookDbContext Context()=>new(new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(Connection).Options);
        public static async Task<Fixture> Create()
        {
            var f=new Fixture(); var settings=LocalPostgresTestServer.Require(Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING")!); settings.Timeout=15;settings.CommandTimeout=30;
            f.Name=$"shuttlebook_f05_test_{Guid.NewGuid():N}"; f.Control=new(settings.ConnectionString);await f.Control.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(f.Name)}",f.Control).ExecuteNonQueryAsync();settings.Database=f.Name;f.Connection=settings.ConnectionString;
            try {
            await using(var db=f.Context())
            {
                // Seed with the current EF model only after the current schema
                // exists. Legacy F05 upgrade rows are covered independently by
                // PaymentConfirmationTests.
                await db.Database.MigrateAsync(); var now=DateTimeOffset.UtcNow;var hasher=new PasswordHasher<User>();
                foreach(var name in new[]{"customer","other","operator"}) { var user=new User {Email=$"f05-{name}@example.test",NormalizedEmail=$"f05-{name}@example.test",AccountType=name=="operator"?AccountType.VenueOperator:AccountType.Customer,Status=UserStatus.Active,CreatedAt=now,UpdatedAt=now};user.PasswordHash=hasher.HashPassword(user,"Test-password-2026!");db.Users.Add(user);if(name=="customer")f.CustomerId=user.Id;if(name=="operator")f.OwnerId=user.Id; }
                var business=new Business {Name="F05 Club",LegalName="Test",Contact="Private",Status="ACTIVE",CreatedAt=now,UpdatedAt=now};db.Businesses.Add(business);
                var venue=new Venue {BusinessId=business.Id,Name="F05 Venue",Address="Hà Nội",Contact="Venue contact",Latitude=21,Longitude=105,Timezone="Asia/Ho_Chi_Minh",Status="PUBLISHED",CreatedAt=now,UpdatedAt=now};db.Venues.Add(venue);f.VenueId=venue.Id;
                var court=new Court {VenueId=venue.Id,Name="F05 Court",Status="ACTIVE",CreatedAt=now};db.Courts.Add(court);f.CourtId=court.Id;
                for(var day=0;day<7;day++) {db.CourtOperatingHours.Add(new CourtOperatingHour {CourtId=court.Id,DayOfWeek=day,OpensAt=new(17,0),ClosesAt=new(22,0)});db.PricingRules.Add(new PricingRule {CourtId=court.Id,DayOfWeek=day,StartsAt=new(17,0),EndsAt=new(22,0),PricePerSlot=100000});}
                var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone)).Date).AddDays(7);f.Date=date.ToString("yyyy-MM-dd");
                db.PricingRules.Add(new PricingRule {CourtId=court.Id,DayOfWeek=(int)date.DayOfWeek,StartsOn=date,EndsOn=date,StartsAt=new(18,0),EndsAt=new(19,0),PricePerSlot=200000,Priority=1});
                var qr=new MediaUpload {OwnerUserId=f.OwnerId,VenueId=venue.Id,Purpose="QR",Status="READY",ObjectKey=$"f05/{Guid.NewGuid():N}",ContentType="image/png",SizeBytes=f.Bytes.Length,Sha256Base64=Convert.ToBase64String(SHA256.HashData(f.Bytes)),CreatedAt=now};db.MediaUploads.Add(qr);f.QrId=qr.Id;
                db.VenuePaymentAccounts.Add(new VenuePaymentAccount {VenueId=venue.Id,BankCode="TEST",AccountName="TEST CLUB",AccountNumber="1234567890",QrUploadId=qr.Id});await db.SaveChangesAsync();
                await db.Database.MigrateAsync();Assert.Equal("F05 Court",(await db.Courts.SingleAsync()).Name);await db.Database.MigrateAsync();
            }
            f.Factory=new(f.Connection);using var client=f.Factory.CreateClient();var root=f.Factory.Services.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) as Microsoft.AspNetCore.Hosting.IWebHostEnvironment;
            f.path=Path.Combine(root!.ContentRootPath,".media-local",f.QrId.ToString("N"));Directory.CreateDirectory(Path.GetDirectoryName(f.path)!);await File.WriteAllBytesAsync(f.path,f.Bytes);return f;
            } catch {await f.DisposeAsync();throw;}
        }
        public async Task Login(HttpClient client,string name)
        { var result=await Data(await client.PostAsJsonAsync("/api/v1/auth/login",new {contactType="email",contact=$"f05-{name}@example.test",password="Test-password-2026!"}));client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",result.GetProperty("accessToken").GetString()); }
        public Task<JsonElement> Quote(HttpClient client,string start,string end)=>QuoteCore(client,start,end);
        private async Task<JsonElement> QuoteCore(HttpClient client,string start,string end)=>await Data(await client.PostAsJsonAsync("/api/v1/availability/quote",new {courtId=CourtId,date=Date,startsAt=start,endsAt=end}));
        public Task<HttpResponseMessage> CreateBooking(HttpClient client,JsonElement q,string key,Guid? overrideCourt=null)
        { var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/bookings") {Content=JsonContent.Create(new {courtId=overrideCourt??q.GetProperty("courtId").GetGuid(),quoteId=q.GetProperty("quoteId").GetGuid(),startsAt=q.GetProperty("startsAt").GetDateTimeOffset(),endsAt=q.GetProperty("endsAt").GetDateTimeOffset()})};request.Headers.Add("Idempotency-Key",key);return client.SendAsync(request); }
        public async ValueTask DisposeAsync() {Factory?.Dispose();if(File.Exists(path))File.Delete(path);NpgsqlConnection.ClearAllPools();if(Control is not null){await new NpgsqlCommand($"DROP DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(Name)} WITH (FORCE)",Control).ExecuteNonQueryAsync();await Control.DisposeAsync();}}
    }
    private sealed class ApiFactory(string connection,int horizon=60):WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder) {builder.ConfigureHostConfiguration(c=>c.AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:ShuttleBook"]=connection,["Identity:JwtSigningKey"]="f05-test-signing-key-more-than-32-bytes",["Identity:OtpPepper"]="f05-test-pepper-more-than-32-bytes",["Media:Mode"]="Local",["Booking:MaxAdvanceDays"]=horizon.ToString() }));builder.ConfigureLogging(l=>l.ClearProviders());return base.CreateHost(builder);}
        protected override void ConfigureWebHost(IWebHostBuilder builder)=>builder.UseEnvironment("Testing");
    }
}
