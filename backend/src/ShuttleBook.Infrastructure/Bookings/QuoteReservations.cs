using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Bookings;

public sealed class QuoteReservation
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CourtId { get; set; }
    public string Kind { get; set; } = "CASUAL";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

public static class QuoteReservations
{
    // Public reads ignore expired holds even if the Worker is temporarily stopped.
    public static IQueryable<CourtAllocation> Active(ShuttleBookDbContext db, DateTimeOffset now) =>
        db.CourtAllocations.Where(a => a.Status == "RESERVED" &&
            (a.Kind != "QUOTE_HOLD" || db.QuoteReservations.Any(r => r.Id == a.QuoteReservationId &&
                r.ReleasedAt == null && r.ConsumedAt == null && r.ExpiresAt > now)));

    // Quote/create lock parents before the court, matching published configuration updates.
    public static async Task LockCourt(ShuttleBookDbContext db, Guid courtId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT b.id FROM businesses b JOIN venues v ON v.business_id=b.id JOIN courts c ON c.venue_id=v.id WHERE c.id={courtId} FOR UPDATE OF b", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT v.id FROM venues v JOIN courts c ON c.venue_id=v.id WHERE c.id={courtId} FOR UPDATE OF v", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM courts WHERE id={courtId} FOR UPDATE", ct);
    }

    public static async Task CleanupCourt(ShuttleBookDbContext db, Guid courtId, DateTimeOffset now, CancellationToken ct)
    {
        var expired = await db.QuoteReservations.Where(r => r.CourtId == courtId && r.ConsumedAt == null &&
            r.ReleasedAt == null && r.ExpiresAt <= now).OrderBy(r => r.Id).ToListAsync(ct);
        foreach (var r in expired) await Release(db, r, now, ct);
        await db.SaveChangesAsync(ct);
    }

    // Serialize quote requests for one customer across all courts and booking types.
    // Take this before court locks so concurrent tabs cannot each observe zero holds.
    public static async Task LockCustomerQuote(ShuttleBookDbContext db, Guid customerId, CancellationToken ct)
    {
        var key = $"CUSTOMER_QUOTE:{customerId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))", ct);
    }

    public static Task<bool> HasActiveCustomerQuote(ShuttleBookDbContext db, Guid customerId, DateTimeOffset now, CancellationToken ct) =>
        db.QuoteReservations.AnyAsync(r => r.CustomerId == customerId && r.ConsumedAt == null &&
            r.ReleasedAt == null && r.ExpiresAt > now, ct);

    public static QuoteReservation Reserve(ShuttleBookDbContext db, Guid quoteId, Guid customerId, Guid courtId, string kind,
        DateTimeOffset now, DateTimeOffset deadline, IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> times)
    {
        var r = new QuoteReservation { Id = quoteId, CustomerId = customerId, CourtId = courtId, Kind = kind,
            CreatedAt = now, ExpiresAt = deadline };
        db.QuoteReservations.Add(r);
        foreach (var t in times) db.CourtAllocations.Add(new CourtAllocation { CourtId = courtId, Kind = "QUOTE_HOLD",
            QuoteReservationId = quoteId, StartsAt = t.Start, EndsAt = t.End, CreatedAt = now });
        return r;
    }

    public static async Task<List<CourtAllocation>> Allocations(ShuttleBookDbContext db, Guid reservationId, CancellationToken ct) =>
        await db.CourtAllocations.Where(a => a.QuoteReservationId == reservationId && a.Status == "RESERVED" && a.Kind == "QUOTE_HOLD")
            .OrderBy(a => a.StartsAt).ThenBy(a => a.Id).ToListAsync(ct);

    public static async Task Release(ShuttleBookDbContext db, QuoteReservation reservation, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var a in await Allocations(db, reservation.Id, ct)) { a.Status = "RELEASED"; a.ReleasedAt = now; }
        reservation.ReleasedAt = now;
    }

    public static async Task<bool> ProcessOne(ShuttleBookDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var candidates = await db.QuoteReservations.AsNoTracking().Where(r => r.ReleasedAt == null && r.ConsumedAt == null && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt).ThenBy(r => r.Id).Select(r => new { r.Id, r.CourtId }).Take(100).ToListAsync(ct);
        foreach (var candidate in candidates)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Skip locked courts instead of delaying all quote cleanup behind one busy court.
            var locked = await db.Courts.FromSqlInterpolated($"SELECT * FROM courts WHERE id={candidate.CourtId} FOR UPDATE SKIP LOCKED").AsNoTracking().SingleOrDefaultAsync(ct);
            if (locked is null) continue;
            if (!await db.QuoteReservations.AnyAsync(r => r.CourtId == candidate.CourtId && r.ReleasedAt == null && r.ConsumedAt == null && r.ExpiresAt <= now, ct)) continue;
            await CleanupCourt(db, candidate.CourtId, now, ct);
            await tx.CommitAsync(ct); return true;
        }
        return false;
    }
}
