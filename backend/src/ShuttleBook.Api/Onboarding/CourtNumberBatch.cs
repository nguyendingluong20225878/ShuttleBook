using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Onboarding;

public static partial class OnboardingEndpoints
{
    private sealed record CourtNumberBatchInput(List<CourtNumberChange>? Courts);
    private sealed record CourtNumberChange(Guid Id, long Version, int Number);

    private static async Task<IResult> UpdateCourtNumbers(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<CourtNumberBatchInput>(http, ["courts"], ct);
        if (error is not null) return error;
        var changes = input?.Courts;
        if (changes is not { Count: > 0 and <= 200 } || changes.Any(x => x.Id == Guid.Empty || x.Version < 1 || x.Number < 1) ||
            changes.Select(x => x.Id).Distinct().Count() != changes.Count ||
            changes.Select(x => x.Number).Distinct().Count() != changes.Count)
            return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var venue = await db.Venues.SingleOrDefaultAsync(x => x.Id == venueId, ct);
        if (venue is null || !await Owns(db, user!.Id, venue.BusinessId, ct)) return Problem(http, 404, "NOT_FOUND");
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {venue.BusinessId} FOR UPDATE")
            .SingleAsync(ct);
        await db.Entry(venue).ReloadAsync(ct);
        var courts = await db.Courts.Where(x => x.VenueId == venueId).OrderBy(x => x.Id).ToListAsync(ct);
        if (courts.Count != changes.Count || courts.Select(x => x.Id).Except(changes.Select(x => x.Id)).Any())
            return Problem(http, 409, "STATE_CONFLICT");
        foreach (var court in courts)
        {
            if (!await DraftCourt(db, user, business, venue, court, ct) &&
                !(court.Status == "ACTIVE" && venue.Status == "PUBLISHED" && await ActiveOwner(db, user, business, ct)))
                return Problem(http, 409, "STATE_CONFLICT");
            if (court.Version != changes.Single(x => x.Id == court.Id).Version)
                return Problem(http, 412, "PRECONDITION_FAILED");
        }
        var changed = courts.Where(court => court.Name != CourtNumbers.Name(changes.Single(x => x.Id == court.Id).Number)).ToList();
        // The existing unique (venue_id, name) index is immediate. Temporary names permit swaps
        // inside the same transaction; no intermediate state becomes visible to other requests.
        foreach (var court in changed) court.Name = $"__renaming_{court.Id:N}";
        if (changed.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var court in courts)
        {
            var name = CourtNumbers.Name(changes.Single(x => x.Id == court.Id).Number);
            if (court.Name == name) continue;
            court.Name = name;
            court.Version++;
            Audit(db, user.Id, "court.name_updated", "court", court.Id, http);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { courts = CourtNumbers.Order(courts).Select(x => new { x.Id, x.Name, x.Version }) });
    }
}
