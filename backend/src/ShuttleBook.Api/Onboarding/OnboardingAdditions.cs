using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Onboarding;

public static partial class OnboardingEndpoints
{
    private sealed record SensitiveVenueBaseline(string Address, string Contact, string Timezone, double Latitude,
        double Longitude, string BankCode, string AccountName, string AccountNumber, Guid QrUploadId);
    private sealed record BusinessRevision(long Version, string Name, string LegalName, string Contact);
    private sealed record AdditionCourt(Guid Id, string Name, long Version,
        List<CourtOperatingHour> Hours, List<PricingRule> Prices);
    private sealed record AdditionVenue(Guid Id, string Name, string Address, string Contact, string Timezone,
        double Latitude, double Longitude, long Version, Guid? ImageUploadId, string BankCode, string AccountName,
        string AccountNumber, Guid QrUploadId, List<AdditionCourt> Courts);
    private sealed record AdditionProfile(Guid Id, string Name, string LegalName, string Contact, List<AdditionVenue> Venues);

    private static Task<bool> ValidOptionalVenueImage(ShuttleBookDbContext db, Venue venue, CancellationToken ct) =>
        venue.ImageUploadId is null ? Task.FromResult(true) : db.MediaUploads.AnyAsync(u =>
            u.Id == venue.ImageUploadId && u.VenueId == venue.Id && u.CourtId == null && u.BookingId == null &&
            u.Purpose == "VENUE_IMAGE" && u.Status == "READY", ct);

    private static SensitiveVenueBaseline Baseline(Venue v, VenuePaymentAccount p) =>
        new(v.Address, v.Contact, v.Timezone, v.Latitude, v.Longitude, p.BankCode, p.AccountName, p.AccountNumber, p.QrUploadId);
    private static Task<bool> ActiveOwner(ShuttleBookDbContext db, User user, Business business, CancellationToken ct) =>
        user.Status != UserStatus.Active || business.Status != "ACTIVE" ? Task.FromResult(false) :
        db.BusinessMemberships.AnyAsync(m => m.BusinessId == business.Id && m.UserId == user.Id && m.Role == "OWNER" && m.Status == "ACTIVE", ct);
    private static async Task<bool> ActiveSubmittedOwner(ShuttleBookDbContext db, ApprovalRequest a, Business b, CancellationToken ct)
    {
        var owner = await db.Users.SingleOrDefaultAsync(u => u.Id == a.SubmittedBy, ct);
        return owner?.AccountType == AccountType.VenueOperator && await ActiveOwner(db, owner, b, ct);
    }
    private static Task<bool> DraftVenue(ShuttleBookDbContext db, User user, Business b, Venue v, CancellationToken ct) =>
        v.Status != "DRAFT" ? Task.FromResult(false) : b.Status == "DRAFT" ? Owns(db, user.Id, b.Id, ct) : ActiveOwner(db, user, b, ct);
    private static async Task<bool> DraftCourt(ShuttleBookDbContext db, User user, Business b, Venue v, Court c, CancellationToken ct) =>
        c.Status == "INACTIVE" && (await DraftVenue(db, user, b, v, ct) || v.Status == "PUBLISHED" && await ActiveOwner(db, user, b, ct));

    private static async Task<bool> ConfigurableCourt(ShuttleBookDbContext db, User user, Business b, Venue v, Court c, CancellationToken ct) =>
        await DraftCourt(db, user, b, v, c, ct) || c.Status == "PENDING_APPROVAL" && v.Status == "PUBLISHED" &&
        await ActiveOwner(db, user, b, ct) && await db.ApprovalRequests.AnyAsync(a =>
            a.BusinessId == b.Id && a.VenueId == v.Id && a.CourtId == c.Id &&
            a.Kind == "COURT_ADDITION" && a.Status == "PENDING", ct);

    private static async Task<IResult> ConfirmCourt(HttpContext http, ShuttleBookDbContext db, Guid courtId, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0 || http.Request.ContentLength is > 0 ||
            await http.Request.Body.ReadAsync(new byte[1], ct) > 0) return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (user?.AccountType != AccountType.VenueOperator || user.Status != UserStatus.Active)
            return Problem(http, 403, "FORBIDDEN");
        var observed = await db.Courts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courtId, ct);
        if (observed is null) return Problem(http, 404, "NOT_FOUND");
        var parent = await db.Venues.AsNoTracking().SingleAsync(v => v.Id == observed.VenueId, ct);
        if (!await Owns(db, user.Id, parent.BusinessId, ct)) return Problem(http, 404, "NOT_FOUND");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id={parent.BusinessId} FOR UPDATE").SingleAsync(ct);
        if (!await ActiveOwner(db, user, business, ct)) return Problem(http, 409, "STATE_CONFLICT");
        var venue = await db.Venues.FromSqlInterpolated($"SELECT * FROM venues WHERE id={parent.Id} FOR UPDATE").SingleAsync(ct);
        var court = await db.Courts.FromSqlInterpolated($"SELECT * FROM courts WHERE id={courtId} FOR UPDATE").SingleAsync(ct);
        if (venue.Status != "PUBLISHED" || court.VenueId != venue.Id || court.Status is not ("INACTIVE" or "PENDING_APPROVAL"))
            return Problem(http, 409, "STATE_CONFLICT");
        var legacy = await db.ApprovalRequests.FromSqlInterpolated($"SELECT * FROM approval_requests WHERE business_id={business.Id} AND venue_id={venue.Id} AND court_id={courtId} AND kind='COURT_ADDITION' AND status='PENDING' FOR UPDATE").ToListAsync(ct);
        if (court.Status == "PENDING_APPROVAL" && legacy.Count == 0) return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, court.Version);
        if (versionError is not null) return versionError;
        var hours = await db.CourtOperatingHours.Where(h => h.CourtId == courtId).ToListAsync(ct);
        var prices = await db.PricingRules.Where(p => p.CourtId == courtId).ToListAsync(ct);
        var payment = await db.VenuePaymentAccounts.SingleOrDefaultAsync(p => p.VenueId == venue.Id, ct);
        if (!ValidStoredSchedule(hours, prices) || payment is null ||
            !await db.MediaUploads.AnyAsync(u => u.Id == payment.QrUploadId && u.VenueId == venue.Id &&
                u.CourtId == null && u.BookingId == null && u.Purpose == "QR" && u.Status == "READY", ct))
            return Problem(http, 409, "INCOMPLETE_PROFILE");
        court.Status = "ACTIVE"; court.Version++;
        foreach (var request in legacy)
        {
            request.Status = "SUPERSEDED";
            request.ReviewedAt = DateTimeOffset.UtcNow;
            request.Reason = "Owner confirmed court under the updated publication policy.";
            Audit(db, user.Id, "approval.court_superseded", "approval_request", request.Id, http);
        }
        Audit(db, user.Id, "court.confirmed", "court", court.Id, http);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        http.Response.Headers.ETag = $"\"{court.Version}\"";
        return Ok(http, new { court.Id, court.Status, court.Version });
    }

    private static async Task<IResult> SubmitVenueAddition(HttpContext http, ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var user = await UserAsync(http, db, ct);
        if (user?.AccountType != AccountType.VenueOperator || user.Status != UserStatus.Active) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null || !await Owns(db, user.Id, venue.BusinessId, ct)) return Problem(http, 404, "NOT_FOUND");
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id={venue.BusinessId} FOR UPDATE").SingleAsync(ct);
        await db.Entry(venue).ReloadAsync(ct);
        if (!await ActiveOwner(db, user, business, ct) || venue.Status != "DRAFT" ||
            await db.ApprovalRequests.AnyAsync(a => a.BusinessId == business.Id && a.Status == "PENDING", ct))
            return Problem(http, 409, "STATE_CONFLICT");
        var courts = await db.Courts.Where(c => c.VenueId == venueId).ToListAsync(ct);
        if (courts.Any(c => c.Status != "INACTIVE")) return Problem(http, 409, "STATE_CONFLICT");
        var profile = await AdditionData(db, business, venue, courts, ct);
        if (profile is null) return Problem(http, 409, "INCOMPLETE_PROFILE");
        venue.Status = "PENDING_APPROVAL";
        foreach (var court in courts) court.Status = "PENDING_APPROVAL";
        var request = new ApprovalRequest { BusinessId = business.Id, VenueId = venueId,
            Kind = "VENUE_ADDITION", SubmittedBy = user.Id,
            Snapshot = JsonSerializer.Serialize(profile, Json), SubmittedAt = DateTimeOffset.UtcNow };
        db.ApprovalRequests.Add(request);
        Outbox(db, "APPROVAL_SUBMITTED", null, request.Id, null);
        Audit(db, user.Id, "inventory.addition_submitted", "venue", venueId, http);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(http, new { request.Id, request.Kind, request.Status, venueId });
    }
    private static async Task<AdditionProfile?> AdditionData(ShuttleBookDbContext db, Business business, Venue venue, List<Court> courts, CancellationToken ct)
    {
        if (courts.Count == 0 || !TextOk(venue.Contact, 3, 160) || !await ValidOptionalVenueImage(db, venue, ct)) return null;
        var payment = await db.VenuePaymentAccounts.SingleOrDefaultAsync(p => p.VenueId == venue.Id, ct);
        if (payment is null || !await db.MediaUploads.AnyAsync(u => u.Id == payment.QrUploadId && u.VenueId == venue.Id && u.Purpose == "QR" && u.Status == "READY", ct)) return null;
        var snapshots = new List<AdditionCourt>();
        foreach (var court in courts.OrderBy(c => c.Id))
        {
            var hours = await db.CourtOperatingHours.Where(h => h.CourtId == court.Id).OrderBy(h => h.DayOfWeek).ToListAsync(ct);
            var prices = await db.PricingRules.Where(p => p.CourtId == court.Id).OrderBy(p => p.DayOfWeek).ThenBy(p => p.StartsAt).ToListAsync(ct);
            if (!ValidStoredSchedule(hours, prices)) return null;
            snapshots.Add(new(court.Id, court.Name, court.Version, hours, prices));
        }
        return new(business.Id, business.Name, business.LegalName, business.Contact,
            [new(venue.Id, venue.Name, venue.Address, venue.Contact, venue.Timezone, venue.Latitude, venue.Longitude, venue.Version,
                venue.ImageUploadId, payment.BankCode, payment.AccountName, payment.AccountNumber, payment.QrUploadId, snapshots)]);
    }
    private static async Task<IResult> SubmitBusinessRevision(HttpContext http, ShuttleBookDbContext db, Guid businessId, CancellationToken ct)
    {
        var (input, error) = await Read<BusinessInput>(http, ["name", "legalName", "contact"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Name, 2, 160) || !TextOk(input.LegalName, 2, 200) || !TextOk(input.Contact, 3, 160)) return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id={businessId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (business is null || !await Owns(db, user!.Id, businessId, ct)) return Problem(http, 404, "NOT_FOUND");
        if (!await ActiveOwner(db, user!, business, ct) || await db.ApprovalRequests.AnyAsync(a => a.BusinessId == businessId && a.Status == "PENDING", ct)) return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, business.Version);
        if (versionError is not null) return versionError;
        var approval = new ApprovalRequest { BusinessId = businessId, Kind = "BUSINESS_REVISION", SubmittedBy = user!.Id,
            Snapshot = JsonSerializer.Serialize(new BusinessRevision(business.Version, input.Name!.Trim(), input.LegalName!.Trim(), input.Contact!.Trim()), Json), SubmittedAt = DateTimeOffset.UtcNow };
        db.ApprovalRequests.Add(approval); Outbox(db, "APPROVAL_SUBMITTED", null, approval.Id, null);
        Audit(db, user.Id, "business.revision_submitted", "business", businessId, http);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(http, new { approval.Id, approval.Kind, approval.Status });
    }
    private static async Task<(IResult? Error, bool Done)> DecideExtension(HttpContext http, ShuttleBookDbContext db,
        ApprovalRequest approval, User admin, string? reason, CancellationToken ct)
    {
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id={approval.BusinessId} FOR UPDATE").SingleAsync(ct);
        if (business.Status != "ACTIVE" || reason is null && !await ActiveSubmittedOwner(db, approval, business, ct)) return (Problem(http, 409, "STATE_CONFLICT"), false);
        if (approval.Kind == "BUSINESS_REVISION")
        {
            var snapshot = JsonSerializer.Deserialize<BusinessRevision>(approval.Snapshot, Json);
            if (snapshot is null || reason is null && snapshot.Version != business.Version) return (Problem(http, 409, "VERSION_CONFLICT"), false);
            if (reason is null)
            {
                business.Name = snapshot.Name; business.LegalName = snapshot.LegalName; business.Contact = snapshot.Contact;
                business.Version++; business.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            // Stored approvals may contain the removed court image field. Ignore it when comparing approval inventory.
            var submitted = JsonSerializer.Deserialize<AdditionProfile>(approval.Snapshot,
                new JsonSerializerOptions(Json) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip });
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == approval.VenueId && v.BusinessId == business.Id, ct);
            if (submitted is null || venue is null || submitted.Venues.Count != 1 || submitted.Venues[0].Id != venue.Id ||
                venue.Status != "PENDING_APPROVAL") return (Problem(http, 409, "STATE_CONFLICT"), false);
            var ids = submitted.Venues[0].Courts.Select(c => c.Id).ToArray();
            if (ids.Length == 0) return (Problem(http, 409, "STATE_CONFLICT"), false);
            var courts = await db.Courts.Where(c => c.VenueId == venue.Id && ids.Contains(c.Id)).ToListAsync(ct);
            if (courts.Count != ids.Length || courts.Any(c => c.Status != "PENDING_APPROVAL")) return (Problem(http, 409, "STATE_CONFLICT"), false);
            if (reason is null)
            {
                var actual = await AdditionData(db, business, venue, courts, ct);
                if (actual is null) return (Problem(http, 409, "INCOMPLETE_PROFILE"), false);
                var equal = JsonSerializer.Serialize(actual.Venues, Json) == JsonSerializer.Serialize(submitted.Venues, Json);
                if (!equal) return (Problem(http, 409, "VERSION_CONFLICT"), false);
            }
            venue.Status = reason is null ? "PUBLISHED" : "DRAFT"; venue.Version++;
            foreach (var court in courts) { court.Status = reason is null ? "ACTIVE" : "INACTIVE"; court.Version++; }
        }
        approval.Status = reason is null ? "APPROVED" : "CHANGES_REQUESTED"; approval.Reason = reason;
        approval.ReviewedBy = admin.Id; approval.ReviewedAt = DateTimeOffset.UtcNow;
        Outbox(db, reason is null ? "APPROVAL_APPROVED" : "APPROVAL_CHANGES_REQUESTED", approval.SubmittedBy, approval.Id, reason);
        Audit(db, admin.Id, reason is null ? "approval.extension_approved" : "approval.extension_changes_requested", "approval_request", approval.Id, http);
        return (null, true);
    }
}
