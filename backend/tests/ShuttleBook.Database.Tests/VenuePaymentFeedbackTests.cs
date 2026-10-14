using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    [Fact]
    public async Task Venue_payment_revision_conflicts_on_interleaved_metadata_change_then_retries_with_canonical_metadata()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, venue, _) = await ActiveInventory(context);
            var barrier = new PaymentRevisionInterleaver();
            await using var baseFactory = new OnboardingApiFactory(connection);
            await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddDbContext<ShuttleBookDbContext>(options => options.AddInterceptors(barrier))));
            using var owner = factory.CreateClient();
            var login = await Data(await owner.PostAsJsonAsync("/api/v1/auth/login", new { contactType = "email", contact = "addition-owner@example.test", password = Password }));
            owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
            var qr = await Upload(owner, venue, "QR");
            var path = $"{Root}/venues/{venue}/payment-account/revisions";
            object proposal = new { accountName = "NEW RECIPIENT", accountNumber = "9999999999", qrUploadId = qr };
            barrier.Arm();
            var pending = owner.PostAsJsonAsync(path, proposal);
            try
            {
                await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
                await using var change = context(); await using var tx = await change.Database.BeginTransactionAsync();
                var canonical = (await change.Venues.FindAsync(venue))!;
                await change.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM businesses WHERE id = {canonical.BusinessId} FOR UPDATE");
                canonical.Address = "Canonical concurrent address"; canonical.Version++;
                await change.SaveChangesAsync(); await tx.CommitAsync();
            }
            finally { barrier.Resume.TrySetResult(); }
            await Problem(await pending, HttpStatusCode.Conflict, "STATE_CONFLICT");
            await using (var check = context()) Assert.Empty(await check.ApprovalRequests.ToListAsync());
            var approval = (await Data(await owner.PostAsJsonAsync(path, proposal))).GetProperty("id").GetGuid();
            await using var final = context();
            using var snapshot = JsonDocument.Parse((await final.ApprovalRequests.FindAsync(approval))!.Snapshot);
            Assert.Equal("Canonical concurrent address", snapshot.RootElement.GetProperty("address").GetString());
            Assert.Equal(2, snapshot.RootElement.GetProperty("version").GetInt64());
        });
    }

    private sealed class PaymentRevisionInterleaver : DbCommandInterceptor
    {
        private int armed;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Volatile.Write(ref armed, 1);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM businesses", StringComparison.Ordinal) &&
                command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) && Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                Reached.TrySetResult(); await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task Venue_payment_without_bank_code_keeps_owner_upload_and_account_validation()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (business, existingVenue, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var owner = await Login(anon, factory, "addition-owner@example.test");
            using var other = await Login(anon, factory, "addition-other@example.test");
            using var customer = await Login(anon, factory, "addition-customer@example.test");
            var venue = (await Data(await owner.PostAsJsonAsync($"{Root}/businesses/{business}/venues", VenueBody()), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var qr = await Upload(owner, venue, "QR");
            var image = await Upload(owner, venue, "VENUE_IMAGE");
            var otherQr = await Upload(owner, existingVenue, "QR");
            string path = $"{Root}/venues/{venue}/payment-account";
            object Valid() => new { accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = qr };
            await Problem(await other.PutAsJsonAsync(path, Valid()), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await customer.PutAsJsonAsync(path, Valid()), HttpStatusCode.Forbidden, "FORBIDDEN");
            foreach (var body in new[] {
                new { accountName = "X", accountNumber = "1234567890", qrUploadId = qr },
                new { accountName = "TEST OWNER", accountNumber = "12345", qrUploadId = qr },
                new { accountName = "TEST OWNER", accountNumber = "123abc7890", qrUploadId = qr },
                new { accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = Guid.Empty },
                new { accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = image },
                new { accountName = "TEST OWNER", accountNumber = "1234567890", qrUploadId = otherQr }
            }) await Problem(await owner.PutAsJsonAsync(path, body), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            var saved = await Data(await owner.PutAsJsonAsync(path, Valid()));
            Assert.False(saved.TryGetProperty("bankCode", out _));
            var detail = await Data(await owner.GetAsync($"{Root}/businesses/{business}"));
            var payment = detail.GetProperty("venues").EnumerateArray().Single(v => v.GetProperty("id").GetGuid() == venue).GetProperty("paymentAccount");
            Assert.False(payment.TryGetProperty("bankCode", out _));
            Assert.Equal(qr, payment.GetProperty("qrUploadId").GetGuid());
            await using var check = context();
            var stored = await check.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venue);
            Assert.Equal("", stored.BankCode); Assert.Equal("1234567890", stored.AccountNumber);
        });
    }

    [Fact]
    public async Task Venue_payment_is_shared_by_courts_revision_requires_admin_and_preserves_booking_snapshots()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (business, venue, firstCourt) = await ActiveInventory(context);
            Guid secondCourt, secondVenue, isolatedCourt, isolatedQr;
            await using (var db = context())
            {
                var sibling = new Court { VenueId = venue, Name = "2", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
                var isolated = new Venue { BusinessId = business, Name = "Independent Venue", Address = "Independent address", Contact = "Test contact", Status = "PUBLISHED", Latitude = 10.8, Longitude = 106.7, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                var isolatedSlot = new Court { VenueId = isolated.Id, Name = "1", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
                secondCourt = sibling.Id; secondVenue = isolated.Id; isolatedCourt = isolatedSlot.Id;
                db.Courts.AddRange(sibling, isolatedSlot); db.Venues.Add(isolated); await db.SaveChangesAsync();
                var oldQr = await db.MediaUploads.SingleAsync(u => u.VenueId == venue && u.Purpose == "QR");
                var owner = await db.Users.SingleAsync(u => u.Email == "addition-owner@example.test");
                var independentQr = new MediaUpload { OwnerUserId = owner.Id, VenueId = isolated.Id, Purpose = "QR", ObjectKey = "independent-qr", Status = "READY", ContentType = oldQr.ContentType, Sha256Base64 = oldQr.Sha256Base64, SizeBytes = oldQr.SizeBytes, CreatedAt = DateTimeOffset.UtcNow };
                isolatedQr = independentQr.Id; db.MediaUploads.Add(independentQr); await db.SaveChangesAsync();
                db.VenuePaymentAccounts.Add(new() { VenueId = isolated.Id, AccountName = "INDEPENDENT OWNER", AccountNumber = "8888888888", QrUploadId = independentQr.Id });
                foreach (var id in new[] { firstCourt, secondCourt, isolatedCourt })
                {
                    db.CourtOperatingHours.Add(new() { CourtId = id, DayOfWeek = 1, OpensAt = new(8, 0), ClosesAt = new(10, 0) });
                    db.PricingRules.Add(new() { CourtId = id, DayOfWeek = 1, StartsAt = new(8, 0), EndsAt = new(10, 0), PricePerSlot = 100000 });
                }
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anon = factory.CreateClient();
            using var ownerClient = await Login(anon, factory, "addition-owner@example.test");
            using var other = await Login(anon, factory, "addition-other@example.test");
            using var admin = await Login(anon, factory, "addition-admin@example.test", true);
            using var customer = await Login(anon, factory, "addition-customer@example.test");
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").Date).AddDays(1);
            while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
            async Task<JsonElement> Book(Guid court, DateOnly day)
            {
                var quote = await Data(await customer.PostAsJsonAsync("/api/v1/availability/quote", new { courtId = court, date = day.ToString("yyyy-MM-dd"), startsAt = "08:00", endsAt = "10:00" }));
                using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings") { Content = JsonContent.Create(new { courtId = court, quoteId = quote.GetProperty("quoteId").GetGuid(), startsAt = quote.GetProperty("startsAt").GetDateTimeOffset(), endsAt = quote.GetProperty("endsAt").GetDateTimeOffset() }) };
                create.Headers.Add("Idempotency-Key", "payment-feedback-" + Guid.NewGuid().ToString("N"));
                var booking = await Data(await customer.SendAsync(create), HttpStatusCode.Created);
                Assert.False(booking.GetProperty("payment").TryGetProperty("bankCode", out _));
                return booking;
            }
            var oldBooking = await Book(firstCourt, date);
            var nextQr = await Upload(ownerClient, venue, "QR");
            object Revision(Guid qr) => new { accountName = "NEW RECIPIENT", accountNumber = "9999999999", qrUploadId = qr };
            string revisionPath = $"{Root}/venues/{venue}/payment-account/revisions";
            await Problem(await other.PostAsJsonAsync(revisionPath, Revision(nextQr)), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await ownerClient.PostAsJsonAsync(revisionPath, Revision(isolatedQr)), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await ownerClient.PostAsJsonAsync(revisionPath, new { address = "Old cached address", accountName = "NEW RECIPIENT", accountNumber = "9999999999", qrUploadId = nextQr }), HttpStatusCode.BadRequest, "UNSUPPORTED_FIELD");
            // The browser may still have old metadata. Only the current locked venue may enter this QR-only proposal.
            await using (var changed = context()) { (await changed.Venues.FindAsync(venue))!.Address = "Canonical updated address"; await changed.SaveChangesAsync(); }
            var approval = (await Data(await ownerClient.PostAsJsonAsync(revisionPath, Revision(nextQr)))).GetProperty("id").GetGuid();
            var pendingBooking = await Book(secondCourt, date);
            var isolatedBooking = await Book(isolatedCourt, date);
            string oldSnapshot, pendingSnapshot;
            await using (var db = context())
            {
                var current = await db.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venue);
                Assert.NotEqual(nextQr, current.QrUploadId); Assert.Equal("TEST OWNER", current.AccountName);
                var oldPayment = await db.BookingPayments.SingleAsync(p => p.BookingId == oldBooking.GetProperty("bookingId").GetGuid());
                var pendingPayment = await db.BookingPayments.SingleAsync(p => p.BookingId == pendingBooking.GetProperty("bookingId").GetGuid());
                oldSnapshot = oldPayment.RecipientSnapshot; pendingSnapshot = pendingPayment.RecipientSnapshot;
                Assert.Equal(current.QrUploadId, oldPayment.QrUploadId); Assert.Equal(oldPayment.QrUploadId, pendingPayment.QrUploadId);
                Assert.Equal(isolatedQr, (await db.BookingPayments.SingleAsync(p => p.BookingId == isolatedBooking.GetProperty("bookingId").GetGuid())).QrUploadId);
                Assert.Equal(2, await db.VenuePaymentAccounts.CountAsync());
            }
            var adminDetail = await Data(await admin.GetAsync($"/api/v1/admin/approval-requests/{approval}"));
            Assert.DoesNotContain("bankCode", adminDetail.GetProperty("snapshot").GetString());
            using (var proposed = JsonDocument.Parse(adminDetail.GetProperty("snapshot").GetString()!))
                Assert.Equal("Canonical updated address", proposed.RootElement.GetProperty("address").GetString());
            Assert.False(adminDetail.GetProperty("current").TryGetProperty("bankCode", out _));
            await Data(await admin.PostAsync($"/api/v1/admin/approval-requests/{approval}/approve", null));
            var newBooking = await Book(secondCourt, date.AddDays(7));
            Assert.Equal("NEW RECIPIENT", newBooking.GetProperty("payment").GetProperty("accountName").GetString());
            var oldDetail = await Data(await customer.GetAsync($"/api/v1/bookings/{oldBooking.GetProperty("bookingId").GetGuid()}"));
            Assert.Equal("TEST OWNER", oldDetail.GetProperty("payment").GetProperty("accountName").GetString());
            Assert.False(oldDetail.GetProperty("payment").TryGetProperty("bankCode", out _));
            await using var final = context();
            Assert.Equal(nextQr, (await final.BookingPayments.SingleAsync(p => p.BookingId == newBooking.GetProperty("bookingId").GetGuid())).QrUploadId);
            Assert.Equal(oldSnapshot, (await final.BookingPayments.SingleAsync(p => p.BookingId == oldBooking.GetProperty("bookingId").GetGuid())).RecipientSnapshot);
            Assert.Equal(pendingSnapshot, (await final.BookingPayments.SingleAsync(p => p.BookingId == pendingBooking.GetProperty("bookingId").GetGuid())).RecipientSnapshot);
            Assert.Equal(isolatedQr, (await final.VenuePaymentAccounts.SingleAsync(p => p.VenueId == secondVenue)).QrUploadId);
            Assert.Equal("Canonical updated address", (await final.Venues.FindAsync(venue))!.Address);
        });
    }
}
