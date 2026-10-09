using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Bookings;

/// <summary>One payment and one concurrency boundary for a casual booking or a complete fixed series.</summary>
public sealed record BookingGroup(Booking Anchor, IReadOnlyList<Booking> Members, BookingSeries? Series)
{
    public static async Task<Guid?> AnchorId(ShuttleBookDbContext db, Guid bookingId, CancellationToken ct) =>
        await (from b in db.Bookings.AsNoTracking() join p in db.BookingPayments.AsNoTracking()
               on b.PaymentScopeId equals p.PaymentScopeId where b.Id == bookingId select (Guid?)p.BookingId).SingleOrDefaultAsync(ct);

    public static async Task<BookingGroup?> Lock(ShuttleBookDbContext db, Booking scope, CancellationToken ct, bool skipLocked = false)
    {
        BookingSeries? series = null;
        if (scope.SeriesId is Guid seriesId)
        {
            series = skipLocked
                ? await db.BookingSeries.FromSqlInterpolated($"SELECT * FROM booking_series WHERE id={seriesId} FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct)
                : await db.BookingSeries.FromSqlInterpolated($"SELECT * FROM booking_series WHERE id={seriesId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (series is null) return null;
        }
        var members = skipLocked
            ? await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE payment_scope_id={scope.PaymentScopeId} ORDER BY id FOR UPDATE SKIP LOCKED").ToListAsync(ct)
            : await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE payment_scope_id={scope.PaymentScopeId} ORDER BY id FOR UPDATE").ToListAsync(ct);
        if (members.Count == 0 || series is not null && members.Count != series.OccurrenceCount) return null;
        var anchorId = await db.BookingPayments.AsNoTracking().Where(p => p.PaymentScopeId == scope.PaymentScopeId)
            .Select(p => p.BookingId).SingleAsync(ct);
        var anchor = members.Single(b => b.Id == anchorId);
        return new(anchor, members, series);
    }

    public bool Consistent => Members.All(b => b.Status == Anchor.Status && b.Version == Anchor.Version && b.PaymentDeadline == Anchor.PaymentDeadline);

    public void SetStatus(string status, DateTimeOffset? expiredAt = null)
    {
        var version = Anchor.Version + 1;
        foreach (var b in Members) { b.Status = status; b.Version = version; if (expiredAt is not null) b.ExpiredAt = expiredAt; }
    }

    public async Task Release(ShuttleBookDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var ids = Members.Select(b => b.AllocationId).ToArray();
        var allocations = await db.CourtAllocations.FromSqlInterpolated(
            $"SELECT * FROM court_allocations WHERE id=ANY({ids}) ORDER BY id FOR UPDATE").ToListAsync(ct);
        if (allocations.Count != Members.Count) throw new InvalidOperationException("Incomplete booking allocation group.");
        foreach (var allocation in allocations) { allocation.Status = "RELEASED"; allocation.ReleasedAt = now; }
    }
}
