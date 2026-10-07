using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed class PaymentConfirmationTests
{
    [Fact]
    public async Task Scope_headers_and_payload_validation_do_not_mutate_booking()
    {
        await using var f = await Fixture.Create();
        using var customer = await f.Client("customer");
        var created = await f.Book(customer);
        var id = created.GetProperty("bookingId").GetGuid();
        var paymentId = created.GetProperty("payment").GetProperty("paymentId").GetGuid();
        using var guest = f.Factory.CreateClient();
        await Code(await Send(guest, ReportRoute(id), "guest", 1, Evidence()), 401, "UNAUTHORIZED");
        using var other = await f.Client("other");
        await Code(await Send(other, ReportRoute(id), "other", 1, Evidence()), 404, "NOT_FOUND");
        await Code(await other.GetAsync($"/api/v1/payments/{paymentId}"), 404, "NOT_FOUND");
        using var ownerB = await f.Client("owner-b");
        await Code(await ownerB.GetAsync($"/api/v1/operator/bookings/{id}"), 404, "NOT_FOUND");
        await Code(await ownerB.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings"), 404, "NOT_FOUND");
        await Code(await ownerB.GetAsync($"/api/v1/payments/{paymentId}"), 404, "NOT_FOUND");
        await Code(await Send(ownerB, ConfirmRoute(id), "wrong-owner", 1, Confirmation(created)), 404, "NOT_FOUND");
        using var owner = await f.Client("owner");
        await Code(await Send(owner, ReportRoute(id), "owner-report", 1, Evidence()), 403, "FORBIDDEN");
        await Code(await Send(customer, ConfirmRoute(id), "customer-confirm", 1, Confirmation(created)), 403, "FORBIDDEN");
        using var admin = await f.Client("admin");
        await Code(await Send(admin, ConfirmRoute(id), "admin-confirm", 1, Confirmation(created)), 403, "FORBIDDEN");
        await Code(await Send(customer, ReportRoute(id), "missing-version", null, Evidence()), 428, "PRECONDITION_REQUIRED");
        await Code(await Send(customer, ReportRoute(id), "stale-version", 2, Evidence()), 412, "PRECONDITION_FAILED");
        await Code(await Send(customer, ReportRoute(id), "unknown-field", 1, new { bankReference = "DEMO", objectKey = "client-chosen" }), 400, "UNSUPPORTED_FIELD");
        await Code(await Send(customer, ReportRoute(id), "long-note", 1, Evidence(note: new string('x', 1001))), 400, "VALIDATION_FAILED");
        using (var duplicate = new HttpRequestMessage(HttpMethod.Post, ReportRoute(id)) { Content = new StringContent("{\"bankReference\":\"A\",\"bankReference\":\"B\"}", System.Text.Encoding.UTF8, "application/json") })
        {
            duplicate.Headers.Add("Idempotency-Key", "duplicate-json"); duplicate.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
            await Code(await customer.SendAsync(duplicate), 400, "VALIDATION_FAILED");
        }
        await using var db = f.Context();
        Assert.Equal("AWAITING_TRANSFER", (await db.Bookings.SingleAsync()).Status);
        Assert.Equal("AWAITING_TRANSFER", (await db.BookingPayments.SingleAsync()).Status);
        Assert.Equal(1, (await db.Bookings.SingleAsync()).Version);
        Assert.Equal(0, await Count(db, "payment_evidence"));
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Concurrent_report_replay_is_one_transition_and_reused_key_cannot_change_intent()
    {
        await using var f = await Fixture.Create();
        using var customer = await f.Client("customer");
        using var secondHost = new ApiFactory(f.Connection, f.Clock);
        using var secondClient = secondHost.CreateClient();
        await f.Login(secondClient, "customer");
        var created = await f.Book(customer);
        var id = created.GetProperty("bookingId").GetGuid();
        var requests = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            Send(i % 2 == 0 ? customer : secondClient, ReportRoute(id), "same-report", 1, Evidence())));
        foreach (var response in requests)
        {
            var data = await Data(response);
            Assert.Equal(id, data.GetProperty("bookingId").GetGuid());
            Assert.Equal("AWAITING_OWNER_CONFIRMATION", data.GetProperty("status").GetString());
            Assert.Equal(2, data.GetProperty("version").GetInt64());
        }
        await Code(await Send(customer, ReportRoute(id), "same-report", 1, Evidence("OTHER-REFERENCE")), 409, "IDEMPOTENCY_KEY_REUSED");
        await Code(await Send(customer, ReportRoute(id), "new-report", 2, Evidence()), 409, "STATE_CONFLICT");
        var differentBooking = await f.Book(customer, "20:00", "21:00", "different-booking");
        await Code(await Send(customer, ReportRoute(differentBooking.GetProperty("bookingId").GetGuid()), "same-report", 1, Evidence()), 409, "IDEMPOTENCY_KEY_REUSED");
        await using var db = f.Context();
        Assert.Equal(1, await Count(db, "payment_evidence"));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_TRANSFER_REPORTED"));
        Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync(x => x.Id == db.Bookings.Where(b => b.Id == id).Select(b => b.AllocationId).First())).Status);
    }

    [Fact]
    public async Task Exact_deadline_and_expiry_only_release_bookings_without_transfer_report()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var before = await f.Book(customer, "18:00", "19:00", "before");
        var exact = await f.Book(customer, "19:00", "20:00", "exact");
        var after = await f.Book(customer, "20:00", "21:00", "after");
        var deadline = before.GetProperty("paymentDeadline").GetDateTimeOffset();
        f.Clock.Set(deadline.AddTicks(-1));
        await Data(await Send(customer, ReportRoute(before.GetProperty("bookingId").GetGuid()), "report-before", 1, Evidence()));
        f.Clock.Set(deadline);
        await Code(await Send(customer, ReportRoute(exact.GetProperty("bookingId").GetGuid()), "report-exact", 1, Evidence()), 409, "PAYMENT_DEADLINE_EXPIRED");
        f.Clock.Set(deadline.AddTicks(1));
        await Code(await Send(customer, ReportRoute(after.GetProperty("bookingId").GetGuid()), "report-after", 1, Evidence()), 409, "PAYMENT_DEADLINE_EXPIRED");
        var processed = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => {
            await using var worker = f.Context(); return await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None);
        }));
        Assert.Equal(2, processed.Count(x => x));
        await using var db = f.Context();
        var rows = await db.Bookings.OrderBy(x => x.LocalStart).ToArrayAsync();
        Assert.Equal(new[] { "AWAITING_OWNER_CONFIRMATION", "EXPIRED", "EXPIRED" }, rows.Select(x => x.Status));
        Assert.Equal(1, await Count(db, "payment_evidence"));
        Assert.Equal(1, await db.CourtAllocations.CountAsync(x => x.Status == "RESERVED"));
    }

    [Fact]
    public async Task Report_checks_deadline_after_waiting_for_booking_lock_and_cannot_revive_expired_row()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        var deadline = created.GetProperty("paymentDeadline").GetDateTimeOffset();
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM bookings WHERE id={id} FOR UPDATE");
        var waitingReport = Send(customer, ReportRoute(id), "wait-report", 1, Evidence());
        await WaitForBookingLock(f);
        Assert.False(waitingReport.IsCompleted);
        f.Clock.Set(deadline);
        await tx.CommitAsync();
        await Code(await waitingReport, 409, "PAYMENT_DEADLINE_EXPIRED");
        await using (var worker = f.Context()) Assert.True(await BookingExpiry.ProcessOne(worker, deadline, CancellationToken.None));
        await Code(await Send(customer, ReportRoute(id), "expired-report", 2, Evidence()), 409, "STATE_CONFLICT");
        await using var db = f.Context();
        Assert.Equal("EXPIRED", (await db.Bookings.SingleAsync()).Status);
        Assert.Equal("RELEASED", (await db.CourtAllocations.SingleAsync()).Status);
        Assert.Equal(0, await Count(db, "payment_evidence"));
    }

    [Fact]
    public async Task F05_upgrade_preserves_booking_QR_snapshot_and_original_create_idempotency()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        string oldHash;
        await using (var db = f.Context())
        {
            oldHash = (await db.BookingIdempotency.SingleAsync()).RequestHash;
            // This test-owned DB has only pre-report F05 data. Downgrade strips
            // F06's nullable columns, then upgrade exercises real legacy rows.
            await db.GetService<IMigrator>().MigrateAsync("20261006181227_F05CasualBooking");
            var connection = (NpgsqlConnection)db.Database.GetDbConnection(); await connection.OpenAsync();
            await using var legacy = new NpgsqlCommand("SELECT customer_id FROM idempotency_records", connection);
            Assert.Equal(f.CustomerId, (Guid)(await legacy.ExecuteScalarAsync())!);
            await connection.CloseAsync();
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        }
        f.Clock.Set(f.Clock.GetUtcNow().AddMinutes(3));
        var replay = await Data(await f.ReplayCreate(customer), 201);
        Assert.Equal(id, replay.GetProperty("bookingId").GetGuid());
        Assert.Equal(created.GetProperty("amount").GetInt64(), replay.GetProperty("amount").GetInt64());
        using (var qr = await customer.GetAsync($"/api/v1/bookings/{id}/qr"))
        { Assert.Equal(HttpStatusCode.OK, qr.StatusCode); Assert.Equal(f.Bytes, await qr.Content.ReadAsByteArrayAsync()); }
        await using var upgraded = f.Context();
        Assert.Equal(1, await upgraded.Bookings.CountAsync());
        Assert.Equal(f.CustomerId, (await upgraded.BookingIdempotency.SingleAsync()).ActorUserId);
        Assert.Equal(oldHash, (await upgraded.BookingIdempotency.SingleAsync()).RequestHash);
        Assert.Equal("QR", (await upgraded.MediaUploads.SingleAsync()).Purpose);
        Assert.Null((await upgraded.MediaUploads.SingleAsync()).BookingId);
    }

    [Fact]
    public async Task Proof_upload_is_private_READY_and_scoped_to_customer_booking_and_evidence()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        using var other = await f.Client("other"); using var owner = await f.Client("owner"); using var ownerB = await f.Client("owner-b");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        var second = await f.Book(customer, "19:00", "20:00", "second"); var secondId = second.GetProperty("bookingId").GetGuid();
        var otherBooking = await f.Book(other, "20:00", "21:00", "other"); var otherId = otherBooking.GetProperty("bookingId").GetGuid();
        var pending = await f.PresignProof(customer, id);
        var pendingId = pending.GetProperty("id").GetGuid();
        await Code(await Send(customer, ReportRoute(id), "pending-proof", 1, Evidence(proof: pendingId)), 409, "UPLOAD_NOT_READY");
        await Code(await Send(customer, ReportRoute(id), "qr-is-not-proof", 1, Evidence(proof: f.QrId)), 404, "NOT_FOUND");
        var otherProof = await f.UploadProof(other, otherId);
        await Code(await Send(customer, ReportRoute(id), "foreign-proof", 1, Evidence(proof: otherProof)), 404, "NOT_FOUND");
        var secondProof = await f.UploadProof(customer, secondId);
        await Code(await Send(customer, ReportRoute(id), "another-booking-proof", 1, Evidence(proof: secondProof)), 404, "NOT_FOUND");
        var proof = await f.UploadProof(customer, id);
        await Code(await owner.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        var reported = await Data(await Send(customer, ReportRoute(id), "valid-proof", 1, Evidence(proof: proof)));
        Assert.Single(reported.GetProperty("evidence").EnumerateArray());
        Assert.DoesNotContain("objectKey", reported.GetRawText());
        using (var image = await owner.GetAsync($"/api/v1/uploads/{proof}/view"))
        {
            Assert.Equal(HttpStatusCode.OK, image.StatusCode); Assert.Equal(f.Bytes, await image.Content.ReadAsByteArrayAsync());
            Assert.True(image.Headers.CacheControl?.NoStore);
        }
        await Code(await other.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        await Code(await ownerB.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        using var guest = f.Factory.CreateClient();
        await Code(await guest.GetAsync($"/api/v1/uploads/{proof}/view"), 401, "UNAUTHORIZED");
        await Code(await other.PostAsync($"/api/v1/uploads/{proof}/complete", null), 404, "NOT_FOUND");
        await Code(await customer.PostAsJsonAsync("/api/v1/uploads/presign", new { venueId = f.VenueId, purpose = "QR", contentType = "image/png", sizeBytes = f.Bytes.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(f.Bytes)) }), 403, "FORBIDDEN");
        await using var db = f.Context();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE media_uploads SET booking_id={otherId} WHERE id={proof}"));
        Assert.Contains(ex.SqlState, new[] { "23503", "23514" }); await tx.RollbackAsync();
    }

    [Fact]
    public async Task Existing_booking_reads_reauthorize_owner_membership_business_and_user_from_database()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        await Data(await owner.GetAsync($"/api/v1/operator/bookings/{id}"));
        await using (var db = f.Context())
        {
            (await db.Venues.SingleAsync()).Status = "SUSPENDED";
            (await db.Courts.SingleAsync()).Status = "INACTIVE";
            await db.SaveChangesAsync();
        }
        // A stopped catalog must not prevent reconciling an already held order.
        await Data(await owner.GetAsync($"/api/v1/operator/bookings/{id}"));
        await using (var db = f.Context()) { (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "REVOKED"; await db.SaveChangesAsync(); }
        await Code(await owner.GetAsync($"/api/v1/operator/bookings/{id}"), 404, "NOT_FOUND");
        await Code(await Send(owner, ConfirmRoute(id), "revoked", 1, Confirmation(created)), 404, "NOT_FOUND");
        await using (var db = f.Context())
        {
            (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "ACTIVE";
            (await db.Users.SingleAsync(x => x.Id == f.OwnerId)).Status = UserStatus.Suspended; await db.SaveChangesAsync();
        }
        await Code(await owner.GetAsync($"/api/v1/operator/bookings/{id}"), 401, "UNAUTHORIZED");
        await using (var db = f.Context())
        {
            (await db.Users.SingleAsync(x => x.Id == f.OwnerId)).Status = UserStatus.Active;
            (await db.Businesses.SingleAsync(x => x.Id == f.BusinessId)).Status = "SUSPENDED"; await db.SaveChangesAsync();
        }
        await Code(await owner.GetAsync($"/api/v1/operator/bookings/{id}"), 404, "NOT_FOUND");
        await using var check = f.Context();
        Assert.Equal("AWAITING_TRANSFER", (await check.Bookings.SingleAsync()).Status);
        Assert.Equal("RESERVED", (await check.CourtAllocations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Operator_listing_paginates_filters_and_returns_aggregate_counts_without_private_evidence()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var first = await f.Book(customer); var second = await f.Book(customer, "19:00", "20:00", "second");
        await Data(await Send(customer, ReportRoute(first.GetProperty("bookingId").GetGuid()), "report-first", 1, Evidence()));
        var page = await Data(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?limit=1"));
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(1, page.GetProperty("counts").GetProperty("awaitingOwnerConfirmation").GetInt32());
        Assert.Equal(0, page.GetProperty("counts").GetProperty("needsReview").GetInt32());
        Assert.DoesNotContain("DEMO-REFERENCE", page.GetRawText()); Assert.DoesNotContain("1234567890", page.GetRawText()); Assert.DoesNotContain("objectKey", page.GetRawText());
        var cursor = page.GetProperty("nextCursor").GetString(); Assert.NotNull(cursor);
        var next = await Data(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?limit=1&before={cursor}"));
        Assert.Single(next.GetProperty("items").EnumerateArray());
        Assert.NotEqual(page.GetProperty("items")[0].GetProperty("bookingId").GetGuid(), next.GetProperty("items")[0].GetProperty("bookingId").GetGuid());
        var filtered = await Data(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?status=AWAITING_TRANSFER&dateFrom={f.Date}&dateTo={f.Date}"));
        Assert.Equal(second.GetProperty("bookingId").GetGuid(), Assert.Single(filtered.GetProperty("items").EnumerateArray()).GetProperty("bookingId").GetGuid());
        Assert.Equal(1, filtered.GetProperty("counts").GetProperty("awaitingOwnerConfirmation").GetInt32());
        await Code(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?limit=0"), 400, "VALIDATION_FAILED");
        await Code(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?status=PAID"), 400, "VALIDATION_FAILED");
        await Code(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings?before=bad"), 400, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Proof_presign_content_verification_and_database_attachment_constraints_use_real_storage()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var other = await f.Client("other"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        var paymentId = created.GetProperty("payment").GetProperty("paymentId").GetGuid();
        var otherBooking = await f.Book(other, "19:00", "20:00", "other"); var otherId = otherBooking.GetProperty("bookingId").GetGuid();
        var route = $"/api/v1/bookings/{id}/proof-uploads/presign";
        using var guest = f.Factory.CreateClient();
        var input = new { contentType = "image/png", sizeBytes = f.Bytes.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(f.Bytes)) };
        await Code(await guest.PostAsJsonAsync(route, input), 401, "UNAUTHORIZED");
        await Code(await other.PostAsJsonAsync(route, input), 404, "NOT_FOUND");
        await Code(await owner.PostAsJsonAsync(route, input), 403, "FORBIDDEN");
        await Code(await customer.PostAsJsonAsync(route, new { contentType = "image/svg+xml", sizeBytes = f.Bytes.Length, sha256Base64 = input.sha256Base64 }), 400, "VALIDATION_FAILED");
        await Code(await customer.PostAsJsonAsync(route, new { contentType = "image/png", sizeBytes = 5242881, sha256Base64 = input.sha256Base64 }), 400, "VALIDATION_FAILED");
        await Code(await customer.PostAsJsonAsync(route, new { contentType = "image/png", sizeBytes = f.Bytes.Length, sha256Base64 = "invalid" }), 400, "VALIDATION_FAILED");
        await Code(await customer.PostAsJsonAsync(route, new { contentType = "image/png", sizeBytes = f.Bytes.Length, sha256Base64 = input.sha256Base64, objectKey = "client-chosen" }), 400, "UNSUPPORTED_FIELD");
        var pending = await f.PresignProof(customer, id); var pendingId = pending.GetProperty("id").GetGuid();
        using (var invalidContent = new ByteArrayContent(new byte[f.Bytes.Length]))
        {
            invalidContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            await Code(await customer.PutAsync(pending.GetProperty("uploadUrl").GetString(), invalidContent), 400, "VALIDATION_FAILED");
        }
        await Code(await customer.PostAsync($"/api/v1/uploads/{pendingId}/complete", null), 409, "UPLOAD_NOT_FOUND");
        var foreignProof = await f.UploadProof(other, otherId);
        var proof = await f.UploadProof(customer, id);
        using (var view = await customer.GetAsync($"/api/v1/uploads/{proof}/view"))
        { Assert.Equal(HttpStatusCode.OK, view.StatusCode); Assert.Equal(f.Bytes, await view.Content.ReadAsByteArrayAsync()); }
        await Code(await owner.GetAsync($"/api/v1/uploads/{proof}/view"), 404, "NOT_FOUND");
        await using var db = f.Context();
        async Task Attachment(Guid upload, string expected)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO payment_evidence (id,payment_id,booking_id,customer_id,venue_id,proof_upload_id,kind,bank_reference,note,reported_at) VALUES ({Guid.CreateVersion7()},{paymentId},{id},{f.CustomerId},{f.VenueId},{upload},'INITIAL','DB-DEMO',NULL,{f.Clock.GetUtcNow()})"));
            Assert.Equal(expected, ex.SqlState); await tx.RollbackAsync();
        }
        await Attachment(pendingId, "23514"); await Attachment(f.QrId, "23514"); await Attachment(foreignProof, "23503");
        Assert.Equal(0, await Count(db, "payment_evidence"));
        Assert.Equal("PENDING", (await db.MediaUploads.SingleAsync(x => x.Id == pendingId)).Status);
    }

    [Fact]
    public async Task Paid_metadata_is_database_required_and_confirmed_allocation_remains_protected_after_play_time()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        var starts = created.GetProperty("startsAt").GetDateTimeOffset(); var ends = created.GetProperty("endsAt").GetDateTimeOffset();
        await using var db = f.Context();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var missing = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payments SET status='PAID',confirmed_by={f.OwnerId},confirmed_at={f.Clock.GetUtcNow()},confirmed_amount=NULL WHERE booking_id={id}"));
            Assert.Equal("23514", missing.SqlState); await tx.RollbackAsync();
        }
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var mismatch = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE payments SET status='PAID',confirmed_by={f.OwnerId},confirmed_at={f.Clock.GetUtcNow()},confirmed_amount=expected_amount+1 WHERE booking_id={id}"));
            Assert.Equal("23514", mismatch.SqlState); await tx.RollbackAsync();
        }
        var booking = await db.Bookings.SingleAsync(); var payment = await db.BookingPayments.SingleAsync();
        booking.Status = "CONFIRMED"; booking.Version++;
        payment.Status = "PAID"; payment.ConfirmedBy = f.OwnerId; payment.ConfirmedAt = f.Clock.GetUtcNow(); payment.ConfirmedAmount = payment.ExpectedAmount;
        await db.SaveChangesAsync();
        await using (var worker = f.Context()) Assert.False(await BookingExpiry.ProcessOne(worker, ends.AddDays(1), CancellationToken.None));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var overlap = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO court_allocations (id,court_id,kind,status,starts_at,ends_at,created_at) VALUES ({Guid.CreateVersion7()},{f.CourtId},'MAINTENANCE','RESERVED',{starts.AddMinutes(30)},{ends.AddMinutes(30)},{f.Clock.GetUtcNow()})"));
            Assert.Equal("23P01", overlap.SqlState); await tx.RollbackAsync();
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO court_allocations (id,court_id,kind,status,starts_at,ends_at,created_at) VALUES ({Guid.CreateVersion7()},{f.CourtId},'BOOKING','RESERVED',{ends},{ends.AddMinutes(30)},{f.Clock.GetUtcNow()})");
        Assert.Equal("CONFIRMED", (await db.Bookings.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync(x => x.Id == booking.AllocationId)).Status);
    }

    [Fact]
    public async Task Outbox_commit_failure_rolls_back_notifications_and_multiple_dispatchers_deduplicate_scoped_owner_event()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid(); var now = f.Clock.GetUtcNow();
        await using (var first = f.Context()) Assert.True(await OutboxDispatch.ProcessOne(first, now, CancellationToken.None));
        Guid eventId;
        await using (var db = f.Context())
        {
            var message = new OutboxMessage { EventType = "PAYMENT_TRANSFER_REPORTED", EntityId = id, CreatedAt = now, NextAttemptAt = now };
            eventId = message.Id; db.OutboxMessages.Add(message); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION f06_test_commit_fail() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.event_type='PAYMENT_TRANSFER_REPORTED' THEN RAISE EXCEPTION 'Test transaction commit failure'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER f06_test_commit_failure AFTER UPDATE ON outbox_messages
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION f06_test_commit_fail();
                """);
        }
        await using (var crashing = f.Context())
        {
            var failure = await Assert.ThrowsAsync<OutboxDispatchException>(() => OutboxDispatch.ProcessOne(crashing, now, CancellationToken.None));
            Assert.Equal(eventId, failure.MessageId);
        }
        await using (var check = f.Context())
        {
            Assert.Empty(await check.Notifications.Where(x => x.OutboxMessageId == eventId).ToArrayAsync());
            Assert.Null((await check.OutboxMessages.SingleAsync(x => x.Id == eventId)).ProcessedAt);
            await check.Database.ExecuteSqlRawAsync("DROP TRIGGER f06_test_commit_failure ON outbox_messages; DROP FUNCTION f06_test_commit_fail();");
        }
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
            await using var dispatcher = f.Context(); return await OutboxDispatch.ProcessOne(dispatcher, now, CancellationToken.None);
        }));
        Assert.Equal(1, outcomes.Count(x => x));
        await using (var db = f.Context())
        {
            var notice = Assert.Single(await db.Notifications.Where(x => x.OutboxMessageId == eventId).ToArrayAsync());
            Assert.Equal(f.OwnerId, notice.UserId); Assert.Contains(created.GetProperty("bookingNo").GetString()!, notice.Body);
            Assert.DoesNotContain("1234567890", notice.Body);
            var message = await db.OutboxMessages.SingleAsync(x => x.Id == eventId); message.ProcessedAt = null;
            await db.SaveChangesAsync();
        }
        await using (var restart = f.Context()) Assert.True(await OutboxDispatch.ProcessOne(restart, now, CancellationToken.None));
        await using (var check = f.Context()) Assert.Single(await check.Notifications.Where(x => x.OutboxMessageId == eventId).ToArrayAsync());
        using var owner = await f.Client("owner"); using var ownerB = await f.Client("owner-b");
        var ownerNotices = await Data(await owner.GetAsync("/api/v1/me/notifications"));
        var linked = Assert.Single(ownerNotices.EnumerateArray());
        Assert.Equal(id, linked.GetProperty("bookingId").GetGuid()); Assert.Equal("OPERATOR_BOOKING", linked.GetProperty("action").GetString());
        Assert.Empty((await Data(await ownerB.GetAsync("/api/v1/me/notifications"))).EnumerateArray());
        await using (var db = f.Context()) { (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "REVOKED"; await db.SaveChangesAsync(); }
        Assert.Empty((await Data(await owner.GetAsync("/api/v1/me/notifications"))).EnumerateArray());
    }

    [Fact]
    public async Task Unknown_or_no_recipient_outbox_is_retained_with_backoff_alert_and_retry_recovery()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid(); var now = f.Clock.GetUtcNow();
        await using (var createdDispatcher = f.Context()) Assert.True(await OutboxDispatch.ProcessOne(createdDispatcher, now, CancellationToken.None));
        Guid unknownId;
        await using (var db = f.Context())
        {
            var message = new OutboxMessage { EventType = "UNKNOWN_TEST_EVENT", TargetUserId = f.OwnerId, EntityId = id, CreatedAt = now, NextAttemptAt = now };
            unknownId = message.Id; db.OutboxMessages.Add(message); await db.SaveChangesAsync();
        }
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            await using (var dispatch = f.Context())
            { var failed = await Assert.ThrowsAsync<OutboxDispatchException>(() => OutboxDispatch.ProcessOne(dispatch, now, CancellationToken.None)); Assert.Equal(unknownId, failed.MessageId); }
            await using var record = f.Context();
            Assert.Equal(attempt, await OutboxDispatch.RecordFailure(record, unknownId, now, CancellationToken.None, "InvalidOperationException"));
            var message = await record.OutboxMessages.AsNoTracking().SingleAsync(x => x.Id == unknownId);
            Assert.Null(message.ProcessedAt); Assert.Equal(attempt, message.Attempts); Assert.Equal("InvalidOperationException", message.LastFailureType);
            Assert.Equal(now.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(10, attempt)))), message.NextAttemptAt);
            if (attempt == 8) Assert.NotNull(message.AlertedAt);
            now = message.NextAttemptAt;
        }
        // Repair only this disposable message to exercise retry recovery with
        // the same outbox identity, then prove a revoked owner gets no fallback.
        await using (var db = f.Context())
        {
            var message = await db.OutboxMessages.SingleAsync(x => x.Id == unknownId); message.EventType = "PAYMENT_TRANSFER_REPORTED"; message.TargetUserId = null;
            (await db.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "REVOKED"; await db.SaveChangesAsync();
        }
        await using (var none = f.Context()) await Assert.ThrowsAsync<OutboxDispatchException>(() => OutboxDispatch.ProcessOne(none, now, CancellationToken.None));
        await using (var check = f.Context())
        {
            Assert.Null((await check.OutboxMessages.SingleAsync(x => x.Id == unknownId)).ProcessedAt);
            Assert.Empty(await check.Notifications.Where(x => x.OutboxMessageId == unknownId).ToArrayAsync());
            (await check.BusinessMemberships.SingleAsync(x => x.UserId == f.OwnerId)).Status = "ACTIVE"; await check.SaveChangesAsync();
        }
        await using (var recovery = f.Context()) Assert.True(await OutboxDispatch.ProcessOne(recovery, now, CancellationToken.None));
        await using var final = f.Context();
        Assert.Equal(f.OwnerId, Assert.Single(await final.Notifications.Where(x => x.OutboxMessageId == unknownId).ToArrayAsync()).UserId);
        Assert.NotNull((await final.OutboxMessages.SingleAsync(x => x.Id == unknownId)).ProcessedAt);
        Assert.Null((await final.OutboxMessages.SingleAsync(x => x.Id == unknownId)).LastFailureType);
    }

    [Fact]
    public async Task Eight_parallel_proof_completion_requests_write_one_READY_audit_without_exposing_object_key()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer");
        var booking = await f.Book(customer); var id = booking.GetProperty("bookingId").GetGuid();
        var upload = await f.PresignProof(customer, id); var uploadId = upload.GetProperty("id").GetGuid();
        Assert.DoesNotContain("objectKey", upload.GetRawText());
        using (var content = new ByteArrayContent(f.Bytes))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var put = await customer.PutAsync(upload.GetProperty("uploadUrl").GetString(), content);
            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        }
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => customer.PostAsync($"/api/v1/uploads/{uploadId}/complete", null)));
        foreach (var response in responses)
        {
            var ready = await Data(response);
            Assert.Equal(uploadId, ready.GetProperty("id").GetGuid()); Assert.Equal("READY", ready.GetProperty("status").GetString());
        }
        await using var db = f.Context();
        Assert.Equal("READY", (await db.MediaUploads.SingleAsync(x => x.Id == uploadId)).Status);
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.EntityId == uploadId && x.Action == "media.upload_ready"));
        Assert.Equal(0, await Count(db, "payment_evidence"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Optional_reference_report_screenshot_supplement_confirmation_and_normalized_replay(bool withScreenshot)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid(); var amount = created.GetProperty("amount").GetInt64();
        Guid? proof = withScreenshot ? await f.UploadProof(customer, id) : null;
        object reportBody = proof.HasValue ? new { proofUploadId = proof.Value } : new { };
        var firstTime = f.Clock.GetUtcNow();
        var reported = await Data(await Send(customer, ReportRoute(id), "no-reference-report", 1, reportBody));
        Assert.Equal("AWAITING_OWNER_CONFIRMATION", reported.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, reported.GetProperty("evidence")[0].GetProperty("bankReference").ValueKind);
        Assert.Equal(withScreenshot, reported.GetProperty("evidence")[0].GetProperty("proofUrl").ValueKind == JsonValueKind.String);
        var replay = await Data(await Send(customer, ReportRoute(id), "no-reference-report", 1,
            new { bankReference = " ", proofUploadId = proof, note = (string?)null }));
        Assert.Equal(2, replay.GetProperty("version").GetInt64());
        await Code(await Send(customer, ReportRoute(id), "no-reference-report", 1,
            new { proofUploadId = proof, note = "Changed intent" }), 409, "IDEMPOTENCY_KEY_REUSED");
        await Code(await Send(owner, ConfirmRoute(id), "no-reference-mismatch", 2,
            new { confirmedAmount = amount + 1 }), 409, "PAYMENT_AMOUNT_MISMATCH");
        await Data(await Send(owner, RejectRoute(id), "request-screenshot", 2, Decision("NEEDS_REVIEW", "Bổ sung ảnh chụp chuyển khoản")));
        f.Clock.Set(created.GetProperty("paymentDeadline").GetDateTimeOffset().AddMinutes(1));
        await using (var worker = f.Context()) Assert.False(await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None));
        var supplemented = await Data(await Send(customer, ReportRoute(id), "no-reference-supplement", 3, new { note = "Đã bổ sung" }));
        Assert.Equal(firstTime, supplemented.GetProperty("payment").GetProperty("firstReportedAt").GetDateTimeOffset());
        Assert.Equal(2, supplemented.GetProperty("evidence").GetArrayLength());
        Assert.All(supplemented.GetProperty("evidence").EnumerateArray(), item => Assert.Equal(JsonValueKind.Null, item.GetProperty("bankReference").ValueKind));
        var confirmed = await Data(await Send(owner, ConfirmRoute(id), "no-reference-confirm", 4, new { confirmedAmount = amount }));
        Assert.Equal("CONFIRMED", confirmed.GetProperty("status").GetString());
        Assert.Equal("PAID", confirmed.GetProperty("payment").GetProperty("status").GetString());
        Assert.Equal(5, confirmed.GetProperty("version").GetInt64());
        Assert.Equal(JsonValueKind.Null, confirmed.GetProperty("decisions")[1].GetProperty("bankReference").ValueKind);
        await Data(await Send(owner, ConfirmRoute(id), "no-reference-confirm", 4,
            new { confirmedAmount = amount, bankReference = (string?)null, note = " " }));
        await using var db = f.Context();
        Assert.Equal(2, await db.PaymentEvidence.CountAsync()); Assert.Equal(2, await db.PaymentDecisions.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_CONFIRMED"));
        Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync()).Status);
        Assert.All(await db.PaymentEvidence.ToArrayAsync(), item => Assert.Null(item.BankReference));
    }

    [Fact]
    public async Task Optional_reference_migration_preserves_legacy_history_snapshots_and_idempotency()
    {
        await using var f = await Fixture.Create();
        await using (var old = f.Context())
            await old.GetService<IMigrator>().MigrateAsync("20261006223312_F06ExactPaymentAmount");
        using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        var reportBody = Evidence("LEGACY-BANK-REFERENCE"); var confirmation = Confirmation(created);
        await Data(await Send(customer, ReportRoute(id), "legacy-report", 1, reportBody));
        await Data(await Send(owner, ConfirmRoute(id), "legacy-confirm", 2, confirmation));
        string recipientSnapshot; string reportHash;
        await using (var before = f.Context())
        {
            recipientSnapshot = (await before.BookingPayments.SingleAsync()).RecipientSnapshot;
            reportHash = (await before.BookingIdempotency.SingleAsync(x => x.Key == "legacy-report")).RequestHash;
            await before.Database.MigrateAsync(); await before.Database.MigrateAsync();
        }
        var replay = await Data(await Send(customer, ReportRoute(id), "legacy-report", 1, reportBody));
        Assert.Equal("CONFIRMED", replay.GetProperty("status").GetString());
        Assert.Equal("LEGACY-BANK-REFERENCE", replay.GetProperty("evidence")[0].GetProperty("bankReference").GetString());
        await Data(await Send(owner, ConfirmRoute(id), "legacy-confirm", 2, confirmation));
        var next = await f.Book(customer, "19:00", "20:00", "new-optional-reference"); var nextId = next.GetProperty("bookingId").GetGuid();
        await Data(await Send(customer, ReportRoute(nextId), "new-report", 1, new { }));
        await Data(await Send(owner, ConfirmRoute(nextId), "new-confirm", 2, new { confirmedAmount = next.GetProperty("amount").GetInt64() }));
        await using var after = f.Context();
        Assert.Equal(recipientSnapshot, (await after.BookingPayments.SingleAsync(x => x.BookingId == id)).RecipientSnapshot);
        Assert.Equal(reportHash, (await after.BookingIdempotency.SingleAsync(x => x.Key == "legacy-report")).RequestHash);
        Assert.Equal("LEGACY-BANK-REFERENCE", (await after.PaymentEvidence.SingleAsync(x => x.BookingId == id)).BankReference);
        Assert.Null((await after.PaymentEvidence.SingleAsync(x => x.BookingId == nextId)).BankReference);
        Assert.Equal(2, await after.PaymentDecisions.CountAsync());
        await using var tx = await after.Database.BeginTransactionAsync();
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => after.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE payment_decisions SET confirmed_amount=NULL WHERE booking_id={nextId} AND resolution='CONFIRMED'"));
        Assert.Equal("23514", invalid.SqlState); await tx.RollbackAsync();
    }

    [Fact]
    public async Task Report_and_exact_amount_confirmation_validate_inputs_preserve_snapshots_and_replay_without_mutating()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid(); var amount = created.GetProperty("amount").GetInt64();
        await Code(await Send(customer, ReportRoute(id), "invalid-reference-type", 1, new { bankReference = 42 }), 400, "VALIDATION_FAILED");
        await Code(await Send(customer, ReportRoute(id), "long-reference", 1, Evidence(new string('x', 101))), 400, "VALIDATION_FAILED");
        await Code(await Send(owner, ConfirmRoute(id), "before-report", 1, Confirmation(created)), 409, "STATE_CONFLICT");
        var reported = await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence("  FT-DEMO-001  ", note: null)));
        Assert.Equal("FT-DEMO-001", reported.GetProperty("evidence")[0].GetProperty("bankReference").GetString());
        Assert.Equal(JsonValueKind.Null, reported.GetProperty("evidence")[0].GetProperty("proofUrl").ValueKind);
        Assert.Equal("TRANSFER_REPORTED", reported.GetProperty("payment").GetProperty("status").GetString());
        await Code(await Send(owner, ConfirmRoute(id), "short", 2, new { confirmedAmount = amount - 1, bankReference = "OWNER-REF" }), 409, "PAYMENT_AMOUNT_MISMATCH");
        await Code(await Send(owner, ConfirmRoute(id), "extra", 2, new { confirmedAmount = amount + 1, bankReference = "OWNER-REF" }), 409, "PAYMENT_AMOUNT_MISMATCH");
        await Code(await Send(owner, ConfirmRoute(id), "negative", 2, new { confirmedAmount = -1, bankReference = "OWNER-REF" }), 400, "VALIDATION_FAILED");
        await Code(await Send(owner, ConfirmRoute(id), "overflow", 2, new { confirmedAmount = long.MaxValue, bankReference = "OWNER-REF" }), 400, "VALIDATION_FAILED");
        await Code(await Send(owner, ConfirmRoute(id), "fractional-vnd", 2, new { confirmedAmount = 0.5m, bankReference = "OWNER-REF" }), 400, "VALIDATION_FAILED");
        await Code(await Send(owner, ConfirmRoute(id), "long-owner-reference", 2, new { confirmedAmount = amount, bankReference = new string('x', 101) }), 400, "VALIDATION_FAILED");
        await Code(await Send(owner, RejectRoute(id), "blank-reason", 2, Decision("NEEDS_REVIEW", " ")), 400, "VALIDATION_FAILED");
        await Code(await Send(owner, RejectRoute(id), "unknown-resolution", 2, Decision("CANCELLED", "Invalid resolution")), 400, "VALIDATION_FAILED");
        await using (var changed = f.Context())
        {
            (await changed.VenuePaymentAccounts.SingleAsync()).AccountName = "NEW RECIPIENT";
            foreach (var rule in await changed.PricingRules.ToArrayAsync()) rule.PricePerSlot = 200000;
            (await changed.Courts.SingleAsync()).HoldMinutes = 25; await changed.SaveChangesAsync();
        }
        var confirmed = await Data(await Send(owner, ConfirmRoute(id), "confirm", 2, Confirmation(created)));
        Assert.Equal("CONFIRMED", confirmed.GetProperty("status").GetString());
        Assert.Equal("PAID", confirmed.GetProperty("payment").GetProperty("status").GetString());
        Assert.Equal(3, confirmed.GetProperty("version").GetInt64());
        Assert.Equal(amount, confirmed.GetProperty("payment").GetProperty("confirmedAmount").GetInt64());
        Assert.Equal(f.OwnerId, confirmed.GetProperty("payment").GetProperty("confirmedBy").GetGuid());
        Assert.Equal(created.GetProperty("paymentDeadline").GetDateTimeOffset(), confirmed.GetProperty("paymentDeadline").GetDateTimeOffset());
        Assert.Equal("TEST CLUB", confirmed.GetProperty("payment").GetProperty("accountName").GetString());
        Assert.Equal(created.GetProperty("slots").GetRawText(), confirmed.GetProperty("slots").GetRawText());
        Assert.Equal("CONFIRMED", (await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence("  FT-DEMO-001  ", note: null)))).GetProperty("status").GetString());
        Assert.Equal(3, (await Data(await Send(owner, ConfirmRoute(id), "confirm", 2, Confirmation(created)))).GetProperty("version").GetInt64());
        await Code(await Send(owner, ConfirmRoute(id), "confirm", 2, new { confirmedAmount = amount, bankReference = "CHANGED" }), 409, "IDEMPOTENCY_KEY_REUSED");
        await Code(await Send(owner, ConfirmRoute(id), "confirm-new-key", 3, Confirmation(created)), 409, "STATE_CONFLICT");
        await DrainOutbox(f);
        await using var db = f.Context();
        Assert.Equal(1, await Count(db, "payment_evidence")); Assert.Equal(1, await Count(db, "payment_decisions"));
        Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync()).Status);
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_CONFIRMED"));
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.UserId == f.CustomerId));
        Assert.Equal(1, await db.Notifications.CountAsync(x => x.UserId == f.OwnerId));
    }

    [Fact]
    public async Task Review_supplement_after_original_deadline_and_replay_keep_history_and_first_report_clock()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid(); var firstTime = f.Clock.GetUtcNow();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        var reviewed = await Data(await Send(owner, RejectRoute(id), "review", 2, Decision("NEEDS_REVIEW", "Cần bổ sung mã đối chiếu")));
        Assert.Equal("NEEDS_REVIEW", reviewed.GetProperty("status").GetString());
        f.Clock.Set(created.GetProperty("paymentDeadline").GetDateTimeOffset().AddHours(1));
        await using (var worker = f.Context()) Assert.False(await BookingExpiry.ProcessOne(worker, f.Clock.GetUtcNow(), CancellationToken.None));
        var supplemented = await Data(await Send(customer, ReportRoute(id), "supplement", 3, Evidence("FT-SUPPLEMENT", note: "Đã đối chiếu theo yêu cầu")));
        Assert.Equal("AWAITING_OWNER_CONFIRMATION", supplemented.GetProperty("status").GetString());
        Assert.Equal(4, supplemented.GetProperty("version").GetInt64());
        Assert.Equal(new[] { "INITIAL", "SUPPLEMENT" }, supplemented.GetProperty("evidence").EnumerateArray().Select(x => x.GetProperty("kind").GetString()));
        Assert.Equal(firstTime, supplemented.GetProperty("payment").GetProperty("firstReportedAt").GetDateTimeOffset());
        Assert.Equal(f.Clock.GetUtcNow(), supplemented.GetProperty("payment").GetProperty("lastReportedAt").GetDateTimeOffset());
        Assert.Equal(4, (await Data(await Send(owner, RejectRoute(id), "review", 2, Decision("NEEDS_REVIEW", "Cần bổ sung mã đối chiếu")))).GetProperty("version").GetInt64());
        await Data(await Send(owner, RejectRoute(id), "review-again", 4, Decision("NEEDS_REVIEW", "Chủ sân đang đối chiếu")));
        var confirmed = await Data(await Send(owner, ConfirmRoute(id), "confirmed-from-review", 5, Confirmation(created)));
        Assert.Equal("CONFIRMED", confirmed.GetProperty("status").GetString()); Assert.Equal(6, confirmed.GetProperty("version").GetInt64());
        Assert.Equal("CONFIRMED", (await Data(await Send(customer, ReportRoute(id), "supplement", 3, Evidence("FT-SUPPLEMENT", note: "Đã đối chiếu theo yêu cầu")))).GetProperty("status").GetString());
        await using var check = f.Context();
        Assert.Equal(2, await Count(check, "payment_evidence")); Assert.Equal(3, await Count(check, "payment_decisions"));
        Assert.Equal("RESERVED", (await check.CourtAllocations.SingleAsync()).Status);
        Assert.False(await BookingExpiry.ProcessOne(check, f.Clock.GetUtcNow(), CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Final_rejection_from_pending_or_review_releases_slot_atomically_and_allows_rebooking(bool fromReview)
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        var version = 2L;
        if (fromReview) { await Data(await Send(owner, RejectRoute(id), "review", version, Decision("NEEDS_REVIEW", "Đối chiếu giao dịch"))); version++; }
        var rejected = await Data(await Send(owner, RejectRoute(id), "reject", version, Decision("FINAL_REJECTION", "Không tìm thấy giao dịch")));
        Assert.Equal("PAYMENT_REJECTED", rejected.GetProperty("status").GetString()); Assert.Equal("REJECTED", rejected.GetProperty("payment").GetProperty("status").GetString());
        var terminalVersion = rejected.GetProperty("version").GetInt64();
        await Code(await Send(owner, ConfirmRoute(id), "late-confirm", terminalVersion, Confirmation(created)), 409, "STATE_CONFLICT");
        await Code(await Send(customer, ReportRoute(id), "late-report", terminalVersion, Evidence()), 409, "STATE_CONFLICT");
        Assert.Equal(terminalVersion, (await Data(await Send(owner, RejectRoute(id), "reject", version, Decision("FINAL_REJECTION", "Không tìm thấy giao dịch")))).GetProperty("version").GetInt64());
        var newBooking = await f.Book(customer, key: "rebook");
        Assert.NotEqual(id, newBooking.GetProperty("bookingId").GetGuid());
        await DrainOutbox(f);
        await using var db = f.Context();
        var allocationId = (await db.Bookings.SingleAsync(x => x.Id == id)).AllocationId;
        Assert.Equal("RELEASED", (await db.CourtAllocations.SingleAsync(x => x.Id == allocationId)).Status);
        Assert.NotNull((await db.CourtAllocations.SingleAsync(x => x.Id == allocationId)).ReleasedAt);
        Assert.Equal(1, await db.CourtAllocations.CountAsync(x => x.Status == "RESERVED"));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_REJECTED"));
    }

    [Fact]
    public async Task Thirty_minute_SLA_includes_review_deduplicates_owner_Admin_and_does_not_reset_on_supplement_or_release()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        using var admin = await f.Client("admin");
        var a = await f.Book(customer); var aId = a.GetProperty("bookingId").GetGuid();
        var b = await f.Book(customer, "19:00", "20:00", "second"); var bId = b.GetProperty("bookingId").GetGuid();
        var terminal = await f.Book(customer, "20:00", "21:00", "terminal"); var terminalId = terminal.GetProperty("bookingId").GetGuid();
        var firstTime = f.Clock.GetUtcNow();
        await Data(await Send(customer, ReportRoute(aId), "report-a", 1, Evidence("REF-A")));
        await Data(await Send(customer, ReportRoute(bId), "report-b", 1, Evidence("REF-B")));
        await Data(await Send(customer, ReportRoute(terminalId), "report-terminal", 1, Evidence("REF-C")));
        await Data(await Send(owner, RejectRoute(aId), "review-a", 2, Decision("NEEDS_REVIEW", "Bổ sung đối chiếu")));
        await Data(await Send(owner, ConfirmRoute(terminalId), "confirm-terminal", 2, Confirmation(terminal)));
        await using (var early = f.Context()) Assert.False(await ConfirmationAlerts.ProcessOne(early, firstTime.AddMinutes(30).AddTicks(-1), CancellationToken.None));
        var scans = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ => {
            await using var db = f.Context(); return await ConfirmationAlerts.ProcessOne(db, firstTime.AddMinutes(30), CancellationToken.None);
        }));
        Assert.Equal(2, scans.Count(x => x));
        // Model a real Admin request within the 30-minute inactivity window.
        // A new token issued at +31 minutes would be future-dated relative to
        // JWT's system clock, while this deterministic payment clock is advanced.
        f.Clock.Set(firstTime.AddMinutes(20));
        await Data(await admin.GetAsync("/api/v1/admin-auth/me"));
        f.Clock.Set(firstTime.AddMinutes(31));
        var supplement = await Data(await Send(customer, ReportRoute(aId), "supplement-a", 3, Evidence("REF-A-SUPPLEMENT")));
        Assert.Equal(firstTime.AddMinutes(30), supplement.GetProperty("confirmationDueAt").GetDateTimeOffset());
        Assert.True(supplement.GetProperty("isOverdue").GetBoolean());
        await using (var later = f.Context())
        {
            Assert.False(await ConfirmationAlerts.ProcessOne(later, firstTime.AddHours(2), CancellationToken.None));
            Assert.False(await BookingExpiry.ProcessOne(later, firstTime.AddHours(2), CancellationToken.None));
        }
        await DrainOutbox(f);
        await using (var db = f.Context())
        {
            Assert.Equal(2, await db.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_CONFIRMATION_OVERDUE"));
            var overdueNotices = await (from n in db.Notifications join o in db.OutboxMessages on n.OutboxMessageId equals o.Id where o.EventType == "PAYMENT_CONFIRMATION_OVERDUE" select n).ToArrayAsync();
            Assert.Equal(4, overdueNotices.Length); Assert.Equal(2, overdueNotices.Count(x => x.UserId == f.OwnerId));
            var adminId = (await db.Users.SingleAsync(x => x.AccountType == AccountType.Admin)).Id;
            Assert.Equal(2, overdueNotices.Count(x => x.UserId == adminId));
            Assert.DoesNotContain(overdueNotices, x => x.UserId == f.CustomerId || x.UserId == f.OwnerBId);
            Assert.Null((await db.BookingPayments.SingleAsync(x => x.BookingId == terminalId)).ConfirmationAlertedAt);
            Assert.Equal(4, (await db.Bookings.SingleAsync(x => x.Id == aId)).Version);
            Assert.Equal("RESERVED", (await db.CourtAllocations.FirstAsync()).Status);
        }
        var adminNotices = await Data(await admin.GetAsync("/api/v1/me/notifications"));
        Assert.All(adminNotices.EnumerateArray(), x => Assert.Equal("ADMIN_PAYMENT_ALERT", x.GetProperty("action").GetString()));
        await Code(await admin.GetAsync($"/api/v1/payments/{a.GetProperty("payment").GetProperty("paymentId").GetGuid()}"), 403, "FORBIDDEN");
    }

    [Fact]
    public async Task Confirm_and_final_reject_wait_for_the_same_booking_lock_and_only_one_terminal_transition_wins()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        using var secondHost = new ApiFactory(f.Connection, f.Clock); using var secondOwner = secondHost.CreateClient(); await f.Login(secondOwner, "owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM bookings WHERE id={id} FOR UPDATE");
        var confirm = Send(owner, ConfirmRoute(id), "confirm-race", 2, Confirmation(created));
        var reject = Send(secondOwner, RejectRoute(id), "reject-race", 2, Decision("FINAL_REJECTION", "Không tìm thấy giao dịch"));
        await WaitForBookingLock(f, 2); await tx.CommitAsync();
        var responses = await Task.WhenAll(confirm, reject); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.OK) await Data(response);
            else await Code(response, 412, "PRECONDITION_FAILED");
        }
        await using var db = f.Context(); var booking = await db.Bookings.SingleAsync(); var payment = await db.BookingPayments.SingleAsync(); var allocation = await db.CourtAllocations.SingleAsync();
        Assert.Equal(3, booking.Version); Assert.Equal(1, await Count(db, "payment_decisions"));
        if (booking.Status == "CONFIRMED") { Assert.Equal("PAID", payment.Status); Assert.Equal("RESERVED", allocation.Status); }
        else { Assert.Equal("PAYMENT_REJECTED", booking.Status); Assert.Equal("REJECTED", payment.Status); Assert.Equal("RELEASED", allocation.Status); }
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_CONFIRMED" || x.EventType == "PAYMENT_REJECTED"));
    }

    [Fact]
    public async Task Customer_supplement_and_owner_confirm_serialize_without_losing_committed_evidence()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        await Data(await Send(owner, RejectRoute(id), "review", 2, Decision("NEEDS_REVIEW", "Bổ sung chứng từ")));
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM bookings WHERE id={id} FOR UPDATE");
        var supplement = Send(customer, ReportRoute(id), "supplement-race", 3, Evidence("SUPPLEMENT-RACE"));
        var confirm = Send(owner, ConfirmRoute(id), "confirm-race", 3, Confirmation(created));
        await WaitForBookingLock(f, 2); await tx.CommitAsync();
        var responses = await Task.WhenAll(supplement, confirm); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        foreach (var response in responses) { if (response.StatusCode == HttpStatusCode.OK) await Data(response); else await Code(response, 412, "PRECONDITION_FAILED"); }
        await using var db = f.Context(); var booking = await db.Bookings.SingleAsync();
        Assert.Equal(4, booking.Version); Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync()).Status);
        if (booking.Status == "CONFIRMED") { Assert.Equal(1, await Count(db, "payment_evidence")); Assert.Equal("PAID", (await db.BookingPayments.SingleAsync()).Status); }
        else { Assert.Equal("AWAITING_OWNER_CONFIRMATION", booking.Status); Assert.Equal(2, await Count(db, "payment_evidence")); Assert.Equal("TRANSFER_REPORTED", (await db.BookingPayments.SingleAsync()).Status); }
    }

    [Fact]
    public async Task Waiting_commands_reauthorize_revoked_owner_and_suspended_customer_after_booking_lock()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        await using (var holder = f.Context())
        {
            await using var tx = await holder.Database.BeginTransactionAsync();
            await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM bookings WHERE id={id} FOR UPDATE");
            var waiting = Send(owner, ConfirmRoute(id), "revoked-while-waiting", 2, Confirmation(created));
            await WaitForBookingLock(f);
            await holder.Database.ExecuteSqlInterpolatedAsync($"UPDATE business_memberships SET status='REVOKED' WHERE user_id={f.OwnerId} AND business_id={f.BusinessId}");
            await tx.CommitAsync(); await Code(await waiting, 404, "NOT_FOUND");
        }
        await using (var reset = f.Context())
        {
            Assert.Equal(2, (await reset.Bookings.SingleAsync()).Version); Assert.Equal(0, await Count(reset, "payment_decisions"));
            await reset.Database.ExecuteSqlInterpolatedAsync($"UPDATE business_memberships SET status='ACTIVE' WHERE user_id={f.OwnerId} AND business_id={f.BusinessId}");
        }
        await Data(await Send(owner, RejectRoute(id), "review", 2, Decision("NEEDS_REVIEW", "Bổ sung đối chiếu")));
        await using (var holder = f.Context())
        {
            await using var tx = await holder.Database.BeginTransactionAsync();
            await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM bookings WHERE id={id} FOR UPDATE");
            var waiting = Send(customer, ReportRoute(id), "suspended-while-waiting", 3, Evidence("SUPPLEMENT"));
            await WaitForBookingLock(f);
            var user = await holder.Users.SingleAsync(x => x.Id == f.CustomerId); user.Status = UserStatus.Suspended;
            await holder.SaveChangesAsync(); await tx.CommitAsync(); await Code(await waiting, 401, "UNAUTHORIZED");
        }
        await using var db = f.Context();
        Assert.Equal("NEEDS_REVIEW", (await db.Bookings.SingleAsync()).Status); Assert.Equal(3, (await db.Bookings.SingleAsync()).Version);
        Assert.Equal(1, await Count(db, "payment_evidence")); Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Owner_confirmation_holds_membership_until_commit_and_membership_revoke_waits()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        var paymentId = created.GetProperty("payment").GetProperty("paymentId").GetGuid();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM payments WHERE id={paymentId} FOR UPDATE");
        var confirmation = Send(owner, ConfirmRoute(id), "confirm-before-revoke", 2, Confirmation(created));
        await WaitForLock(f, "payments", "FOR UPDATE");
        await using var revoker = f.Context();
        var revoke = revoker.Database.ExecuteSqlInterpolatedAsync($"UPDATE business_memberships SET status='REVOKED' WHERE user_id={f.OwnerId} AND business_id={f.BusinessId}");
        await WaitForLock(f, "business_memberships", "UPDATE"); Assert.False(revoke.IsCompleted);
        await tx.CommitAsync();
        Assert.Equal("CONFIRMED", (await Data(await confirmation)).GetProperty("status").GetString());
        Assert.Equal(1, await revoke);
        await Code(await owner.GetAsync($"/api/v1/operator/bookings/{id}"), 404, "NOT_FOUND");
        await using var db = f.Context(); Assert.Equal("PAID", (await db.BookingPayments.SingleAsync()).Status);
        Assert.Equal("RESERVED", (await db.CourtAllocations.SingleAsync()).Status);
    }

    [Fact]
    public async Task SLA_scan_skips_locked_confirmation_and_does_not_alert_after_terminal_commit()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid(); var reportedAt = f.Clock.GetUtcNow();
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        f.Clock.Set(reportedAt.AddMinutes(31));
        await using var holder = f.Context(); await using var tx = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM bookings WHERE id={id} FOR UPDATE");
        var confirmation = Send(owner, ConfirmRoute(id), "confirm", 2, Confirmation(created));
        await WaitForBookingLock(f);
        await using (var lockedScan = f.Context()) Assert.False(await ConfirmationAlerts.ProcessOne(lockedScan, f.Clock.GetUtcNow(), CancellationToken.None));
        await tx.CommitAsync(); await Data(await confirmation);
        await using var scan = f.Context();
        Assert.False(await ConfirmationAlerts.ProcessOne(scan, f.Clock.GetUtcNow(), CancellationToken.None));
        Assert.Equal(0, await scan.OutboxMessages.CountAsync(x => x.EventType == "PAYMENT_CONFIRMATION_OVERDUE"));
        Assert.Null((await scan.BookingPayments.SingleAsync()).ConfirmationAlertedAt);
    }

    [Fact]
    public async Task Eighteen_digit_VND_confirmation_returns_exact_strings_and_persists_integer_amount_without_rounding()
    {
        await using var f = await Fixture.Create(); using var customer = await f.Client("customer"); using var owner = await f.Client("owner");
        const long perSlot = 400_000_000_000_000_001; const long total = perSlot * 2;
        await using (var prices = f.Context()) { foreach (var rule in await prices.PricingRules.ToArrayAsync()) rule.PricePerSlot = perSlot; await prices.SaveChangesAsync(); }
        var created = await f.Book(customer); var id = created.GetProperty("bookingId").GetGuid();
        Assert.Equal(total, created.GetProperty("amount").GetInt64());
        Assert.Equal("800000000000000002", created.GetProperty("amountExact").GetString());
        Assert.Equal("800000000000000002", created.GetProperty("payment").GetProperty("expectedAmountExact").GetString());
        await Data(await Send(customer, ReportRoute(id), "report", 1, Evidence()));
        var confirmed = await Data(await Send(owner, ConfirmRoute(id), "confirm", 2, Confirmation(created)));
        Assert.Equal("800000000000000002", confirmed.GetProperty("payment").GetProperty("confirmedAmountExact").GetString());
        Assert.Equal("800000000000000002", confirmed.GetProperty("decisions")[0].GetProperty("confirmedAmountExact").GetString());
        var listed = await Data(await owner.GetAsync($"/api/v1/operator/venues/{f.VenueId}/bookings"));
        Assert.Equal("800000000000000002", listed.GetProperty("items")[0].GetProperty("amountExact").GetString());
        await using var db = f.Context(); Assert.Equal(total, (await db.BookingPayments.SingleAsync()).ConfirmedAmount);
    }

    private static object Decision(string resolution, string reason, string reasonCode = "EVIDENCE_REQUIRED") => new { resolution, reasonCode, reason };
    private static async Task DrainOutbox(Fixture f)
    {
        for (var i = 0; i < 100; i++) { await using var db = f.Context(); if (!await OutboxDispatch.ProcessOne(db, f.Clock.GetUtcNow(), CancellationToken.None)) return; }
        Assert.Fail("Outbox did not drain within the test safety bound.");
    }

    private static async Task WaitForBookingLock(Fixture f, int expectedWaiters = 1)
        => await WaitForLock(f, "bookings", "FOR UPDATE", expectedWaiters);

    private static async Task WaitForLock(Fixture f, string resource, string operation, int expectedWaiters = 1)
    {
        await using var inspector = new NpgsqlConnection(f.Connection); await inspector.OpenAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND wait_event_type='Lock' AND query ILIKE @resource AND query ILIKE @operation", inspector);
            command.Parameters.AddWithValue("resource", "%" + resource + "%"); command.Parameters.AddWithValue("operation", "%" + operation + "%");
            if ((long)(await command.ExecuteScalarAsync())! >= expectedWaiters) return;
            await Task.Delay(20);
        }
        Assert.Fail($"Expected {expectedWaiters} request(s) waiting for {resource} {operation}; the PostgreSQL lock barrier was not reached.");
    }

    private static string ReportRoute(Guid id) => $"/api/v1/bookings/{id}/transfer-evidence";
    private static string ConfirmRoute(Guid id) => $"/api/v1/operator/bookings/{id}/confirm-payment";
    private static string RejectRoute(Guid id) => $"/api/v1/operator/bookings/{id}/reject-payment";
    private static object Evidence(string reference = "DEMO-REFERENCE", Guid? proof = null, string? note = "Test only") =>
        new { bankReference = reference, proofUploadId = proof, note };
    private static object Confirmation(JsonElement created) => new { confirmedAmount = created.GetProperty("amount").GetInt64(), bankReference = "DEMO-OWNER-REFERENCE", note = "Reconciled test transaction" };
    private static Task<HttpResponseMessage> Send(HttpClient client, string route, string key, long? version, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return client.SendAsync(request);
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response, int status = 200)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            return json.RootElement.GetProperty("data").Clone();
        }
    }
    private static async Task Code(HttpResponseMessage response, int status, string code)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        }
    }
    private static async Task<long> Count(ShuttleBookDbContext db, string table)
    {
        // Only fixed test-owned table names are accepted; never interpolate user input.
        Assert.Contains(table, new[] { "payment_evidence", "payment_decisions" });
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        return (long)(await new NpgsqlCommand($"SELECT count(*) FROM {table}", connection).ExecuteScalarAsync())!;
    }

    private sealed class ManualClock : TimeProvider
    {
        // PostgreSQL stores microseconds. A whole-second starting clock avoids
        // response-vs-persisted sub-microsecond differences at the deadline.
        private long utcTicks = DateTimeOffset.UtcNow.UtcTicks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);
        public void Set(DateTimeOffset now) => Interlocked.Exchange(ref utcTicks, now.UtcTicks);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public string Connection = "";
        public string Name = "";
        private NpgsqlConnection? control;
        private readonly List<string> mediaPaths = [];
        public ApiFactory Factory = null!;
        public ManualClock Clock = new();
        public Guid VenueId, CourtId, CustomerId, OtherCustomerId, OwnerId, OwnerBId, BusinessId, QrId;
        public string Date = "";
        private object? lastCreateBody;
        private string lastCreateKey = "";
        public readonly byte[] Bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
        public ShuttleBookDbContext Context() => new(new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(Connection).Options);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var settings = LocalPostgresTestServer.Require(Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING")!);
            settings.Timeout = 15; settings.CommandTimeout = 30;
            f.Name = $"shuttlebook_f06_test_{Guid.NewGuid():N}";
            Assert.Matches("^shuttlebook_f06_test_[0-9a-f]{32}$", f.Name);
            f.control = new(settings.ConnectionString); await f.control.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(f.Name)}", f.control).ExecuteNonQueryAsync();
            settings.Database = f.Name; f.Connection = settings.ConnectionString;
            try
            {
                await using (var db = f.Context())
                {
                    await db.Database.MigrateAsync();
                    var now = f.Clock.GetUtcNow(); var hasher = new PasswordHasher<User>();
                    foreach (var name in new[] { "customer", "other", "owner", "owner-b", "admin" })
                    {
                        var user = new User { Email = $"f06-{name}@example.test", NormalizedEmail = $"f06-{name}@example.test",
                            AccountType = name == "admin" ? AccountType.Admin : name.StartsWith("owner", StringComparison.Ordinal) ? AccountType.VenueOperator : AccountType.Customer,
                            Status = UserStatus.Active, CreatedAt = now, UpdatedAt = now, EmailVerifiedAt = now };
                        user.PasswordHash = hasher.HashPassword(user, "Test-password-2026!"); db.Users.Add(user);
                        switch (name) { case "customer": f.CustomerId = user.Id; break; case "other": f.OtherCustomerId = user.Id; break; case "owner": f.OwnerId = user.Id; break; case "owner-b": f.OwnerBId = user.Id; break; }
                    }
                    var business = new Business { Name = "F06 Club", LegalName = "Test", Contact = "Private", Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
                    var otherBusiness = new Business { Name = "F06 Other Club", LegalName = "Test", Contact = "Private", Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
                    db.Businesses.AddRange(business, otherBusiness); f.BusinessId = business.Id;
                    db.BusinessMemberships.AddRange(new BusinessMembership { BusinessId = business.Id, UserId = f.OwnerId, Role = "OWNER", Status = "ACTIVE", CreatedAt = now },
                        new BusinessMembership { BusinessId = otherBusiness.Id, UserId = f.OwnerBId, Role = "OWNER", Status = "ACTIVE", CreatedAt = now });
                    var venue = new Venue { BusinessId = business.Id, Name = "F06 Venue", Address = "Hà Nội", Contact = "Venue test", Latitude = 21, Longitude = 105,
                        Timezone = "Asia/Ho_Chi_Minh", Status = "PUBLISHED", CreatedAt = now, UpdatedAt = now };
                    db.Venues.Add(venue); f.VenueId = venue.Id;
                    var court = new Court { VenueId = venue.Id, Name = "F06 Court", Status = "ACTIVE", CreatedAt = now };
                    db.Courts.Add(court); f.CourtId = court.Id;
                    for (var day = 0; day < 7; day++)
                    {
                        db.CourtOperatingHours.Add(new CourtOperatingHour { CourtId = court.Id, DayOfWeek = day, OpensAt = new(17, 0), ClosesAt = new(22, 0) });
                        db.PricingRules.Add(new PricingRule { CourtId = court.Id, DayOfWeek = day, StartsAt = new(17, 0), EndsAt = new(22, 0), PricePerSlot = 100000 });
                    }
                    f.Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone)).Date).AddDays(7).ToString("yyyy-MM-dd");
                    var qr = new MediaUpload { OwnerUserId = f.OwnerId, VenueId = venue.Id, Purpose = "QR", Status = "READY", ObjectKey = $"f06/{Guid.NewGuid():N}",
                        ContentType = "image/png", SizeBytes = f.Bytes.Length, Sha256Base64 = Convert.ToBase64String(SHA256.HashData(f.Bytes)), CreatedAt = now };
                    db.MediaUploads.Add(qr); f.QrId = qr.Id;
                    db.VenuePaymentAccounts.Add(new VenuePaymentAccount { VenueId = venue.Id, BankCode = "TEST", AccountName = "TEST CLUB", AccountNumber = "1234567890", QrUploadId = qr.Id });
                    await db.SaveChangesAsync(); await db.Database.MigrateAsync();
                }
                f.Factory = new(f.Connection, f.Clock);
                using var client = f.Factory.CreateClient();
                var root = f.Factory.Services.GetRequiredService<IWebHostEnvironment>();
                var path = Path.Combine(root.ContentRootPath, ".media-local", f.QrId.ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllBytesAsync(path, f.Bytes); f.mediaPaths.Add(path);
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async Task<HttpClient> Client(string name) { var client = Factory.CreateClient(); await Login(client, name); return client; }
        public async Task Login(HttpClient client, string name)
        {
            // F01 isolates Admin authentication from the public Customer/operator login.
            var route = name == "admin" ? "/api/v1/admin-auth/login" : "/api/v1/auth/login";
            if (name == "admin") client.DefaultRequestHeaders.Add("Origin", "http://localhost:5175");
            var result = await Data(await client.PostAsJsonAsync(route, new { contactType = "email", contact = $"f06-{name}@example.test", password = "Test-password-2026!" }));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.GetProperty("accessToken").GetString());
        }
        public async Task<JsonElement> Book(HttpClient client, string start = "18:00", string end = "19:00", string key = "booking")
        {
            var q = await Data(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId = CourtId, date = Date, startsAt = start, endsAt = end }));
            lastCreateBody = new { courtId = CourtId, quoteId = q.GetProperty("quoteId").GetGuid(), startsAt = q.GetProperty("startsAt").GetDateTimeOffset(), endsAt = q.GetProperty("endsAt").GetDateTimeOffset() };
            lastCreateKey = key;
            return await Data(await ReplayCreate(client), 201);
        }
        public Task<HttpResponseMessage> ReplayCreate(HttpClient client) => Send(client, "/api/v1/bookings", lastCreateKey, null, lastCreateBody!);
        public async Task<JsonElement> PresignProof(HttpClient client, Guid bookingId)
        {
            var upload = await Data(await client.PostAsJsonAsync($"/api/v1/bookings/{bookingId}/proof-uploads/presign", new {
                contentType = "image/png", sizeBytes = Bytes.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(Bytes)) }));
            var id = upload.GetProperty("id").GetGuid();
            mediaPaths.Add(Path.Combine(Factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, ".media-local", id.ToString("N")));
            return upload;
        }
        public async Task<Guid> UploadProof(HttpClient client, Guid bookingId)
        {
            var upload = await PresignProof(client, bookingId); var id = upload.GetProperty("id").GetGuid();
            using var content = new ByteArrayContent(Bytes); content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var put = await client.PutAsync(upload.GetProperty("uploadUrl").GetString(), content); Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
            await Data(await client.PostAsync($"/api/v1/uploads/{id}/complete", null));
            await Data(await client.PostAsync($"/api/v1/uploads/{id}/complete", null));
            return id;
        }
        public async ValueTask DisposeAsync()
        {
            Factory?.Dispose(); foreach (var path in mediaPaths) if (File.Exists(path)) File.Delete(path);
            if (control is null) return;
            var settings = LocalPostgresTestServer.Require(control.ConnectionString);
            Assert.Matches("^shuttlebook_f06_test_[0-9a-f]{32}$", Name);
            Assert.Equal(Name, new NpgsqlConnectionStringBuilder(Connection).Database);
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(Name)} WITH (FORCE)", control).ExecuteNonQueryAsync();
            await control.DisposeAsync();
        }
    }
    private sealed class ApiFactory(string connection, ManualClock clock) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:ShuttleBook"] = connection, ["Identity:JwtSigningKey"] = "f06-test-signing-key-more-than-32-bytes",
                ["Identity:OtpPepper"] = "f06-test-pepper-more-than-32-bytes", ["Media:Mode"] = "Local",
                ["AdminSession:AllowedOrigin"] = "http://localhost:5175" }));
            builder.ConfigureLogging(l => l.ClearProviders()); return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); });
        }
    }
}
