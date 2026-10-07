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
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE status='AWAITING_TRANSFER' AND payment_deadline <= {now} AND EXISTS (SELECT 1 FROM payments WHERE booking_id=bookings.id AND status='AWAITING_TRANSFER') ORDER BY payment_deadline,id LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (booking is null) return false;
        var payment = await db.BookingPayments.SingleAsync(x => x.BookingId == booking.Id, ct);
        if (payment.Status != "AWAITING_TRANSFER") return false;
        var allocation = await db.CourtAllocations.SingleAsync(x => x.Id == booking.AllocationId, ct);
        booking.Status = "EXPIRED"; booking.ExpiredAt = now; booking.Version++;
        payment.Status = "EXPIRED"; allocation.Status = "RELEASED"; allocation.ReleasedAt = now;
        db.AuditEvents.Add(new AuditEvent { Action = "booking.expired", EntityType = "booking", EntityId = booking.Id,
            CorrelationId = "expiry-worker", CreatedAt = now });
        db.OutboxMessages.Add(new OutboxMessage { EventType = "BOOKING_EXPIRED", TargetUserId = booking.CustomerId,
            EntityId = booking.Id, CreatedAt = now, NextAttemptAt = now });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
}
