using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Operations;

public static partial class CourtOperationsEndpoints
{
    private sealed record SpecialPriceInput(DateOnly StartsOn, DateOnly EndsOn, string? StartsAt,
        string? EndsAt, long PricePerSlot);
    private sealed record SpecialPriceCommand(SpecialPriceInput? SpecialPrice);

    private static object? SingleSpecialPrice(List<PricingRule> rules, List<CourtOperatingHour> hours)
    {
        if (rules.Count == 0) return null;
        var first = rules[0];
        var expectedDays = hours.Where(x => DayAppears(first.StartsOn, first.EndsOn, x.DayOfWeek))
            .Select(x => x.DayOfWeek).OrderBy(x => x).ToArray();
        var actualDays = rules.Select(x => x.DayOfWeek).OrderBy(x => x).ToArray();
        if (first.Priority != 1 || !actualDays.SequenceEqual(expectedDays) ||
            rules.Any(x => x.StartsOn != first.StartsOn || x.EndsOn != first.EndsOn ||
                x.StartsAt != first.StartsAt || x.EndsAt != first.EndsAt ||
                x.PricePerSlot != first.PricePerSlot || x.Priority != 1)) return null;
        return new { first.StartsOn, first.EndsOn,
            startsAt = first.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture),
            endsAt = first.EndsAt.ToString("HH:mm", CultureInfo.InvariantCulture), first.PricePerSlot };
    }

    private static bool DayAppears(DateOnly startsOn, DateOnly endsOn, int dayOfWeek) =>
        startsOn.AddDays(((dayOfWeek - (int)startsOn.DayOfWeek) + 7) % 7) <= endsOn;

    private static async Task<IResult> SetSpecialPrice(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var (input, readError) = await Read<SpecialPriceCommand>(http, ["specialPrice"],
            null, null, null, ct);
        if (readError is not null) return readError;
        var special = input!.SpecialPrice;
        if (special is not null && (special.StartsOn == default || special.EndsOn == default ||
            special.StartsOn > special.EndsOn || special.EndsOn.DayNumber - special.StartsOn.DayNumber > 366 ||
            special.PricePerSlot <= 0 || !SlotTime(special.StartsAt, out _) ||
            !SlotTime(special.EndsAt, out _))) return Problem(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (court, _, user, scopeError) = await ScopedCourt(http, db, courtId, true, ct);
        if (scopeError is not null) return scopeError;
        if (court!.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, court.Version);
        if (versionError is not null) return versionError;
        var hours = await db.CourtOperatingHours.Where(x => x.CourtId == courtId).ToListAsync(ct);
        var replacement = new List<PricingRule>();
        if (special is not null)
        {
            var start = TimeOnly.ParseExact(special.StartsAt!, "HH:mm", CultureInfo.InvariantCulture);
            var end = TimeOnly.ParseExact(special.EndsAt!, "HH:mm", CultureInfo.InvariantCulture);
            if (start >= end) return Problem(http, 400, "VALIDATION_FAILED");
            foreach (var hour in hours.Where(x => DayAppears(special.StartsOn, special.EndsOn, x.DayOfWeek)))
            {
                if (start < hour.OpensAt || end > hour.ClosesAt)
                    return Problem(http, 400, "VALIDATION_FAILED");
                replacement.Add(new PricingRule { CourtId = courtId, DayOfWeek = hour.DayOfWeek,
                    StartsOn = special.StartsOn, EndsOn = special.EndsOn, StartsAt = start,
                    EndsAt = end, PricePerSlot = special.PricePerSlot, Priority = 1 });
            }
            if (replacement.Count == 0) return Problem(http, 400, "VALIDATION_FAILED");
        }
        db.PricingRules.RemoveRange(await db.PricingRules.Where(x => x.CourtId == courtId && x.Priority > 0).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.PricingRules.AddRange(replacement);
        court.Version++;
        Audit(db, user!.Id, "court.special_price_updated", courtId, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { court.Id, court.Version, rules = replacement.Count });
    }
}
