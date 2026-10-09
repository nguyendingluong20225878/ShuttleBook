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

    // Replace a customer's prior intent atomically without extending its active deadline.
    // Call inside the court transaction; any failed quote rolls this replacement back.
    public static async Task<DateTimeOffset?> ReplaceOwn(ShuttleBookDbContext db, Guid customerId, Guid courtId, DateTimeOffset now, CancellationToken ct)
    {
        var old = await db.QuoteReservations.Where(r => r.CustomerId == customerId && r.CourtId == courtId &&
            r.ConsumedAt == null && r.ReleasedAt == null).OrderBy(r => r.Id).ToListAsync(ct);
        DateTimeOffset? deadline = null;
        foreach (var r in old)
        {
            if (r.ExpiresAt > now && (deadline is null || r.ExpiresAt < deadline)) deadline = r.ExpiresAt;
            await Release(db, r, now, ct);
        }
        await db.SaveChangesAsync(ct);
        return deadline;
    }

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
