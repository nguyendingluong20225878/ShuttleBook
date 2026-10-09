using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Bookings;

public static class ConfirmationAlerts
{
    public const int SlaMinutes = 30;

    public static async Task<bool> ProcessOne(ShuttleBookDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now.AddMinutes(-SlaMinutes);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var candidates = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE status IN ('AWAITING_OWNER_CONFIRMATION','NEEDS_REVIEW') AND EXISTS (SELECT 1 FROM payments WHERE booking_id=bookings.id AND status IN ('TRANSFER_REPORTED','NEEDS_REVIEW') AND first_reported_at <= {cutoff} AND confirmation_alerted_at IS NULL) ORDER BY id LIMIT 100").AsNoTracking().ToListAsync(ct);
        foreach (var scope in candidates)
        {
            var group = await BookingGroup.Lock(db, scope, ct, true);
            if (group is null || !group.Consistent) continue;
            var booking = group.Anchor;
            var payment = await db.BookingPayments.FromSqlInterpolated($"SELECT * FROM payments WHERE booking_id={booking.Id} FOR UPDATE").SingleAsync(ct);
            if (payment.ConfirmationAlertedAt is not null || payment.FirstReportedAt is null || payment.FirstReportedAt > cutoff ||
                !(booking.Status == "AWAITING_OWNER_CONFIRMATION" && payment.Status == "TRANSFER_REPORTED" || booking.Status == "NEEDS_REVIEW" && payment.Status == "NEEDS_REVIEW")) continue;
            payment.ConfirmationAlertedAt = now;
            db.OutboxMessages.Add(new OutboxMessage { EventType = "PAYMENT_CONFIRMATION_OVERDUE", EntityId = booking.Id,
                CreatedAt = now, NextAttemptAt = now });
            db.AuditEvents.Add(new AuditEvent { Action = "payment.confirmation_overdue", EntityType = "booking",
                EntityId = booking.Id, CorrelationId = "confirmation-alert-worker", CreatedAt = now });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        return false;
    }
}
