using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Api.Onboarding;

public static partial class OnboardingEndpoints
{
    private sealed record VenueContactInput(string? Contact);

    private static async Task<IResult> UpdateVenueContact(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<VenueContactInput>(http, ["contact"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Contact, 3, 160)) return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var venue = await db.Venues.SingleOrDefaultAsync(x => x.Id == venueId, ct);
        if (venue is null || !await Owns(db, user!.Id, venue.BusinessId, ct)) return Problem(http, 404, "NOT_FOUND");
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {venue.BusinessId} FOR UPDATE")
            .SingleAsync(ct);
        await db.Entry(venue).ReloadAsync(ct);
        if (venue.Status != "PUBLISHED" || !await ActiveOwner(db, user, business, ct))
            return Problem(http, 409, "STATE_CONFLICT");
        if (await db.ApprovalRequests.AnyAsync(x => x.VenueId == venueId && x.Status == "PENDING", ct))
            return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, venue.Version);
        if (versionError is not null) return versionError;
        venue.Contact = input.Contact!.Trim(); venue.Version++; venue.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, user.Id, "venue.contact_updated", "venue", venue.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { venue.Id, venue.Contact, venue.Version });
    }
}
