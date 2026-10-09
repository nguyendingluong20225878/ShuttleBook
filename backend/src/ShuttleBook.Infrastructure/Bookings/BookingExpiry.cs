using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Bookings;

public static class BookingExpiry
{
    public static async Task<bool> ProcessOne(ShuttleBookDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Candidate is read without locking an occurrence: the aggregate lock must come first.
        var candidates = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE status='AWAITING_TRANSFER' AND payment_deadline <= {now} AND EXISTS (SELECT 1 FROM payments WHERE booking_id=bookings.id AND status='AWAITING_TRANSFER') ORDER BY payment_deadline,id LIMIT 100").AsNoTracking().ToListAsync(ct);
        foreach (var scope in candidates)
        {
            var group = await BookingGroup.Lock(db, scope, ct, true);
            if (group is null) continue;
            var booking = group.Anchor;
            var payment = await db.BookingPayments.FromSqlInterpolated($"SELECT * FROM payments WHERE booking_id={booking.Id} FOR UPDATE").SingleAsync(ct);
            if (!group.Consistent || booking.Status != "AWAITING_TRANSFER" || booking.PaymentDeadline > now || payment.Status != "AWAITING_TRANSFER") continue;
            group.SetStatus("EXPIRED", now); payment.Status = "EXPIRED";
            await group.Release(db, now, ct);
            db.AuditEvents.Add(new AuditEvent { Action = "booking.expired", EntityType = "booking", EntityId = booking.Id,
                CorrelationId = "expiry-worker", CreatedAt = now });
            db.OutboxMessages.Add(new OutboxMessage { EventType = "BOOKING_EXPIRED", TargetUserId = booking.CustomerId,
                EntityId = booking.Id, CreatedAt = now, NextAttemptAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        return false;
    }
}
