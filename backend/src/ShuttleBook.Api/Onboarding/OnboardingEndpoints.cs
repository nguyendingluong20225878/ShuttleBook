using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Onboarding;

public static class OnboardingEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    private sealed record BusinessInput(string? Name, string? LegalName, string? Contact);
    private sealed record VenueInput(string? Name, string? Address, string? Contact, string? Timezone,
        double Latitude, double Longitude);
    private sealed record CourtInput(string? Name);
    private sealed record HourInput(int DayOfWeek, string? OpensAt, string? ClosesAt);
    private sealed record PriceInput(int DayOfWeek, string? StartsAt, string? EndsAt, long PricePerSlot);
    private sealed record ScheduleInput(List<HourInput>? Hours, List<PriceInput>? Prices);
    private sealed record PaymentInput(string? BankCode, string? AccountName, string? AccountNumber, Guid QrUploadId);
    private sealed record ImageInput(Guid UploadId);
    private sealed record ReasonInput(string? Reason);
    private sealed record RevisionInput(string? Address, string? Contact, string? Timezone, double Latitude, double Longitude,
        string? BankCode, string? AccountName, string? AccountNumber, Guid QrUploadId);
    private sealed record RevisionSnapshot(long Version, string Address, string Contact, string Timezone, double Latitude,
        double Longitude, string BankCode, string AccountName, string AccountNumber, Guid QrUploadId);

    public static void MapOnboardingEndpoints(this WebApplication app)
    {
        var partner = app.MapGroup("/api/v1/partner-onboarding").RequireAuthorization();
        partner.MapPost("/businesses", CreateBusiness);
        partner.MapGet("/businesses", ListBusinesses);
        partner.MapGet("/businesses/{businessId:guid}", GetBusiness);
        partner.MapPut("/businesses/{businessId:guid}", UpdateBusiness);
        partner.MapPost("/businesses/{businessId:guid}/venues", CreateVenue);
        partner.MapPut("/venues/{venueId:guid}", UpdateVenue);
        partner.MapPut("/venues/{venueId:guid}/image", SetImage);
        partner.MapPost("/venues/{venueId:guid}/courts", CreateCourt);
        partner.MapPut("/courts/{courtId:guid}", UpdateCourt);
        partner.MapPut("/courts/{courtId:guid}/schedule", SetSchedule);
        partner.MapPut("/venues/{venueId:guid}/payment-account", SetPaymentAccount);
        partner.MapPost("/businesses/{businessId:guid}/submit", Submit);
        partner.MapPost("/venues/{venueId:guid}/revisions", SubmitRevision);

        var admin = app.MapGroup("/api/v1/admin/approval-requests").RequireAuthorization();
        admin.MapGet("/", ListApprovals);
        admin.MapGet("/{requestId:guid}", GetApproval);
        admin.MapPost("/{requestId:guid}/approve", Approve);
        admin.MapPost("/{requestId:guid}/request-changes", RequestChanges);
    }

    private static async Task<IResult> CreateBusiness(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        var (input, error) = await Read<BusinessInput>(http, ["name", "legalName", "contact"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Name, 2, 160) || !TextOk(input.LegalName, 2, 200) || !TextOk(input.Contact, 3, 160))
            return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (user is null || user.AccountType != AccountType.VenueOperator || user.Status != UserStatus.PendingOnboarding ||
            user.EmailVerifiedAt is null && user.PhoneVerifiedAt is null) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {user.Id} FOR UPDATE").SingleAsync(ct);
        if (user.Status != UserStatus.PendingOnboarding || await db.BusinessMemberships.AnyAsync(m =>
                m.UserId == user.Id && m.Role == "OWNER" && m.Status == "PENDING", ct))
            return Problem(http, 409, "STATE_CONFLICT");
        var now = DateTimeOffset.UtcNow;
        var business = new Business { Name = input.Name!.Trim(), LegalName = input.LegalName!.Trim(),
            Contact = input.Contact!.Trim(), CreatedAt = now, UpdatedAt = now };
        db.Businesses.Add(business);
        db.BusinessMemberships.Add(new BusinessMembership { BusinessId = business.Id, UserId = user.Id, CreatedAt = now });
        Audit(db, user.Id, "business.created", "business", business.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Json(Envelope(new { business.Id, business.Name, business.LegalName, business.Contact,
            business.Status, business.Version }, http), statusCode: 201);
    }

    private static async Task<IResult> ListBusinesses(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        var ids = db.BusinessMemberships.Where(m => m.UserId == user!.Id && m.Role == "OWNER" &&
            (m.Status == "PENDING" || m.Status == "ACTIVE")).Select(m => m.BusinessId);
        var businesses = await db.Businesses.Where(b => ids.Contains(b.Id)).OrderByDescending(b => b.CreatedAt)
            .Select(b => new { b.Id, b.Name, b.LegalName, b.Status, b.Version }).ToListAsync(ct);
        return Ok(http, businesses);
    }

    private static async Task<IResult> GetBusiness(HttpContext http, ShuttleBookDbContext db, Guid businessId, CancellationToken ct)
    {
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        var business = await OwnedBusiness(db, user!.Id, businessId, ct);
        if (business is null) return Problem(http, 404, "NOT_FOUND");
        var venues = await db.Venues.Where(v => v.BusinessId == businessId).OrderBy(v => v.CreatedAt).ToListAsync(ct);
        var venueIds = venues.Select(v => v.Id).ToArray();
        var courts = await db.Courts.Where(c => venueIds.Contains(c.VenueId)).OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var courtIds = courts.Select(c => c.Id).ToArray();
        var hours = await db.CourtOperatingHours.Where(h => courtIds.Contains(h.CourtId)).ToListAsync(ct);
        var prices = await db.PricingRules.Where(p => courtIds.Contains(p.CourtId)).ToListAsync(ct);
        var accounts = await db.VenuePaymentAccounts.Where(p => venueIds.Contains(p.VenueId)).ToListAsync(ct);
        var lastApproval = await db.ApprovalRequests.Where(a => a.BusinessId == businessId)
            .OrderByDescending(a => a.SubmittedAt).Select(a => new { a.Id, a.Status, a.Reason, a.SubmittedAt, a.ReviewedAt })
            .FirstOrDefaultAsync(ct);
        return Ok(http, new { business.Id, business.Name, business.LegalName, business.Contact, business.Status,
            business.Version, approval = lastApproval,
            venues = venues.Select(v => new { v.Id, v.Name, v.Address, v.Contact, v.Timezone, v.Latitude, v.Longitude, v.Status,
                v.ImageUploadId, v.Version, paymentAccount = accounts.Where(p => p.VenueId == v.Id)
                    .Select(p => new { p.BankCode, p.AccountName, maskedAccountNumber = Mask(p.AccountNumber), p.QrUploadId }).FirstOrDefault(),
                courts = courts.Where(c => c.VenueId == v.Id).Select(c => new { c.Id, c.Name, c.Status,
                    hours = hours.Where(h => h.CourtId == c.Id).Select(h => new { h.DayOfWeek, h.OpensAt, h.ClosesAt }),
                    prices = prices.Where(p => p.CourtId == c.Id).Select(p => new { p.DayOfWeek, p.StartsAt, p.EndsAt, p.PricePerSlot }) }) }) });
    }

    private static async Task<IResult> UpdateBusiness(HttpContext http, ShuttleBookDbContext db, Guid businessId, CancellationToken ct)
    {
        var (input, error) = await Read<BusinessInput>(http, ["name", "legalName", "contact"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Name, 2, 160) || !TextOk(input.LegalName, 2, 200) || !TextOk(input.Contact, 3, 160))
            return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (business is null || !await Owns(db, user!.Id, businessId, ct)) return Problem(http, 404, "NOT_FOUND");
        if (business.Status != "DRAFT") return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, business.Version);
        if (versionError is not null) return versionError;
        business.Name = input.Name!.Trim(); business.LegalName = input.LegalName!.Trim(); business.Contact = input.Contact!.Trim();
        business.Version++; business.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(db, user.Id, "business.updated", "business", business.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { business.Id, business.Status, business.Version });
    }

    private static async Task<IResult> CreateVenue(HttpContext http, ShuttleBookDbContext db, Guid businessId, CancellationToken ct)
    {
        var (input, error) = await Read<VenueInput>(http, ["name", "address", "contact", "timezone", "latitude", "longitude"], ct);
        if (error is not null) return error;
        if (!ValidVenue(input!)) return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (business is null || !await Owns(db, user!.Id, businessId, ct)) return Problem(http, 404, "NOT_FOUND");
        if (business.Status != "DRAFT") return Problem(http, 409, "STATE_CONFLICT");
        var now = DateTimeOffset.UtcNow;
        var venue = new Venue { BusinessId = businessId, Name = input!.Name!.Trim(), Address = input.Address!.Trim(),
            Contact = input.Contact!.Trim(),
            Timezone = input.Timezone!.Trim(), Latitude = input.Latitude, Longitude = input.Longitude,
            CreatedAt = now, UpdatedAt = now };
        db.Venues.Add(venue);
        Audit(db, user.Id, "venue.created", "venue", venue.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Json(Envelope(new { venue.Id, venue.BusinessId, venue.Status }, http), statusCode: 201);
    }

    private static async Task<IResult> UpdateVenue(HttpContext http, ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<VenueInput>(http, ["name", "address", "contact", "timezone", "latitude", "longitude"], ct);
        if (error is not null) return error;
        if (!ValidVenue(input!)) return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null || !await Owns(db, user!.Id, venue.BusinessId, ct)) return Problem(http, 404, "NOT_FOUND");
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {venue.BusinessId} FOR UPDATE")
            .SingleAsync(ct);
        await db.Entry(venue).ReloadAsync(ct);
        if (business.Status != "DRAFT" || venue.Status != "DRAFT") return Problem(http, 409, "STATE_CONFLICT");
        var versionError = CheckVersion(http, venue.Version);
        if (versionError is not null) return versionError;
        venue!.Name = input!.Name!.Trim(); venue.Address = input.Address!.Trim();
        venue.Contact = input.Contact!.Trim(); venue.Timezone = input.Timezone!.Trim();
        venue.Latitude = input.Latitude; venue.Longitude = input.Longitude; venue.Version++; venue.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { venue.Id, venue.Status, venue.Version });
    }

    private static async Task<IResult> CreateCourt(HttpContext http, ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<CourtInput>(http, ["name"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Name, 1, 100)) return Problem(http, 400, "VALIDATION_FAILED");
        var (_, user, failure) = await EditableVenue(http, db, venueId, ct);
        if (failure is not null) return failure;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockDraftBusiness(db, venueId, ct)) return Problem(http, 409, "STATE_CONFLICT");
        if (await db.Courts.AnyAsync(c => c.VenueId == venueId && c.Name == input.Name!.Trim(), ct))
            return Problem(http, 409, "STATE_CONFLICT");
        var court = new Court { VenueId = venueId, Name = input.Name!.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        db.Courts.Add(court);
        Audit(db, user!.Id, "court.created", "court", court.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Json(Envelope(new { court.Id, court.VenueId, court.Name, court.Status }, http), statusCode: 201);
    }

    private static async Task<IResult> UpdateCourt(HttpContext http, ShuttleBookDbContext db, Guid courtId, CancellationToken ct)
    {
        var (input, error) = await Read<CourtInput>(http, ["name"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Name, 1, 100)) return Problem(http, 400, "VALIDATION_FAILED");
        var (court, _, failure) = await EditableCourt(http, db, courtId, ct);
        if (failure is not null) return failure;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockDraftBusiness(db, court!.VenueId, ct)) return Problem(http, 409, "STATE_CONFLICT");
        court!.Name = input.Name!.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { court.Id, court.Name });
    }

    private static async Task<IResult> SetSchedule(HttpContext http, ShuttleBookDbContext db, Guid courtId, CancellationToken ct)
    {
        var (input, error) = await Read<ScheduleInput>(http, ["hours", "prices"], ct);
        if (error is not null) return error;
        if (input!.Hours is not { Count: > 0 and <= 7 } || input.Prices is not { Count: > 0 and <= 70 } ||
            input.Hours.Select(h => h.DayOfWeek).Distinct().Count() != input.Hours.Count)
            return Problem(http, 400, "VALIDATION_FAILED");
        var parsedHours = new List<(int Day, TimeOnly Start, TimeOnly End)>();
        var parsedPrices = new List<(int Day, TimeOnly Start, TimeOnly End, long Price)>();
        foreach (var hour in input.Hours)
        {
            if (hour.DayOfWeek is < 0 or > 6 || !SlotTime(hour.OpensAt, out var start) ||
                !SlotTime(hour.ClosesAt, out var end) || start >= end) return Problem(http, 400, "VALIDATION_FAILED");
            parsedHours.Add((hour.DayOfWeek, start, end));
        }
        foreach (var price in input.Prices)
        {
            if (price.DayOfWeek is < 0 or > 6 || price.PricePerSlot <= 0 ||
                !SlotTime(price.StartsAt, out var start) || !SlotTime(price.EndsAt, out var end) || start >= end)
                return Problem(http, 400, "VALIDATION_FAILED");
            parsedPrices.Add((price.DayOfWeek, start, end, price.PricePerSlot));
        }
        foreach (var hour in parsedHours)
        {
            var cursor = hour.Start;
            foreach (var price in parsedPrices.Where(p => p.Day == hour.Day).OrderBy(p => p.Start))
            {
                if (price.Start != cursor || price.End > hour.End) return Problem(http, 400, "VALIDATION_FAILED");
                cursor = price.End;
            }
            if (cursor != hour.End) return Problem(http, 400, "VALIDATION_FAILED");
        }
        if (parsedPrices.Any(p => !parsedHours.Any(h => h.Day == p.Day))) return Problem(http, 400, "VALIDATION_FAILED");
        var (court, user, failure) = await EditableCourt(http, db, courtId, ct);
        if (failure is not null) return failure;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var venue = await db.Venues.SingleAsync(v => v.Id == court!.VenueId, ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {venue.BusinessId} FOR UPDATE")
            .SingleAsync(ct);
        if (business.Status != "DRAFT") return Problem(http, 409, "STATE_CONFLICT");
        db.CourtOperatingHours.RemoveRange(db.CourtOperatingHours.Where(h => h.CourtId == courtId));
        db.PricingRules.RemoveRange(db.PricingRules.Where(p => p.CourtId == courtId));
        await db.SaveChangesAsync(ct);
        db.CourtOperatingHours.AddRange(parsedHours.Select(h => new CourtOperatingHour
            { CourtId = courtId, DayOfWeek = h.Day, OpensAt = h.Start, ClosesAt = h.End }));
        db.PricingRules.AddRange(parsedPrices.Select(p => new PricingRule
            { CourtId = courtId, DayOfWeek = p.Day, StartsAt = p.Start, EndsAt = p.End, PricePerSlot = p.Price }));
        Audit(db, user!.Id, "court.schedule_updated", "court", court!.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { court.Id, hours = parsedHours.Count, prices = parsedPrices.Count });
    }

    private static async Task<IResult> SetPaymentAccount(HttpContext http, ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<PaymentInput>(http, ["bankCode", "accountName", "accountNumber", "qrUploadId"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.BankCode, 2, 32) || !TextOk(input.AccountName, 2, 160) ||
            !TextOk(input.AccountNumber, 6, 40) || !input.AccountNumber!.All(char.IsDigit))
            return Problem(http, 400, "VALIDATION_FAILED");
        var (_, user, failure) = await EditableVenue(http, db, venueId, ct);
        if (failure is not null) return failure;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockDraftBusiness(db, venueId, ct)) return Problem(http, 409, "STATE_CONFLICT");
        if (!await db.MediaUploads.AnyAsync(u => u.Id == input.QrUploadId && u.VenueId == venueId &&
            u.OwnerUserId == user!.Id && u.Purpose == "QR" && u.Status == "READY", ct))
            return Problem(http, 400, "VALIDATION_FAILED");
        var account = await db.VenuePaymentAccounts.SingleOrDefaultAsync(p => p.VenueId == venueId, ct);
        if (account is null) { account = new VenuePaymentAccount { VenueId = venueId }; db.VenuePaymentAccounts.Add(account); }
        account.BankCode = input.BankCode!.Trim().ToUpperInvariant();
        account.AccountName = input.AccountName!.Trim(); account.AccountNumber = input.AccountNumber!; account.QrUploadId = input.QrUploadId;
        Audit(db, user!.Id, "venue.payment_account_updated", "venue", venueId, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { venueId, account.BankCode, account.AccountName,
            maskedAccountNumber = Mask(account.AccountNumber), account.QrUploadId });
    }

    private static async Task<IResult> SetImage(HttpContext http, ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<ImageInput>(http, ["uploadId"], ct);
        if (error is not null) return error;
        var (venue, user, failure) = await EditableVenue(http, db, venueId, ct);
        if (failure is not null) return failure;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockDraftBusiness(db, venueId, ct)) return Problem(http, 409, "STATE_CONFLICT");
        if (!await db.MediaUploads.AnyAsync(u => u.Id == input!.UploadId && u.VenueId == venueId &&
            u.OwnerUserId == user!.Id && u.Purpose == "VENUE_IMAGE" && u.Status == "READY", ct))
            return Problem(http, 400, "VALIDATION_FAILED");
        venue!.ImageUploadId = input!.UploadId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { venue.Id, venue.ImageUploadId });
    }

    private static async Task<IResult> Submit(HttpContext http, ShuttleBookDbContext db, Guid businessId, CancellationToken ct)
    {
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {businessId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (business is null || !await Owns(db, user!.Id, businessId, ct)) return Problem(http, 404, "NOT_FOUND");
        if (business.Status != "DRAFT") return Problem(http, 409, "STATE_CONFLICT");
        var venues = await db.Venues.Where(v => v.BusinessId == businessId).ToListAsync(ct);
        if (venues.Count == 0 || !TextOk(business.Name, 2, 160) || !TextOk(business.LegalName, 2, 200))
            return Problem(http, 409, "INCOMPLETE_PROFILE");
        var snapshotVenues = new List<object>();
        foreach (var venue in venues)
        {
            if (venue.Status != "DRAFT" || !TextOk(venue.Contact, 3, 160) || venue.ImageUploadId is null ||
                !await db.MediaUploads.AnyAsync(u => u.Id == venue.ImageUploadId && u.VenueId == venue.Id &&
                    u.Purpose == "VENUE_IMAGE" && u.Status == "READY", ct))
                return Problem(http, 409, "INCOMPLETE_PROFILE");
            var payment = await db.VenuePaymentAccounts.SingleOrDefaultAsync(p => p.VenueId == venue.Id, ct);
            if (payment is null || !await db.MediaUploads.AnyAsync(u => u.Id == payment.QrUploadId &&
                u.VenueId == venue.Id && u.Purpose == "QR" && u.Status == "READY", ct))
                return Problem(http, 409, "INCOMPLETE_PROFILE");
            var courts = await db.Courts.Where(c => c.VenueId == venue.Id).ToListAsync(ct);
            if (courts.Count == 0) return Problem(http, 409, "INCOMPLETE_PROFILE");
            var snapshotCourts = new List<object>();
            foreach (var court in courts)
            {
                var hours = await db.CourtOperatingHours.Where(h => h.CourtId == court.Id).ToListAsync(ct);
                var prices = await db.PricingRules.Where(p => p.CourtId == court.Id).ToListAsync(ct);
                if (!ValidStoredSchedule(hours, prices)) return Problem(http, 409, "INCOMPLETE_PROFILE");
                snapshotCourts.Add(new { court.Id, court.Name, hours, prices });
            }
            snapshotVenues.Add(new { venue.Id, venue.Name, venue.Address, venue.Contact, venue.Latitude, venue.Longitude,
                venue.Timezone, venue.ImageUploadId, payment.BankCode, payment.AccountName,
                payment.AccountNumber, payment.QrUploadId, courts = snapshotCourts });
            venue.Status = "PENDING_APPROVAL";
        }
        business.Status = "PENDING_APPROVAL"; business.Version++; business.UpdatedAt = DateTimeOffset.UtcNow;
        var approval = new ApprovalRequest { BusinessId = business.Id, SubmittedBy = user.Id,
            Snapshot = JsonSerializer.Serialize(new { business.Id, business.Name, business.LegalName, business.Contact,
                venues = snapshotVenues }, Json), SubmittedAt = DateTimeOffset.UtcNow };
        db.ApprovalRequests.Add(approval);
        Outbox(db, "APPROVAL_SUBMITTED", null, approval.Id, null);
        Audit(db, user.Id, "venue.submitted", "business", business.Id, http);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(http, new { approval.Id, approval.Status, businessId });
    }

    private static async Task<IResult> SubmitRevision(HttpContext http, ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var (input, error) = await Read<RevisionInput>(http,
            ["address", "contact", "timezone", "latitude", "longitude", "bankCode", "accountName", "accountNumber", "qrUploadId"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Address, 8, 400) || !TextOk(input.Contact, 3, 160) ||
            !TextOk(input.Timezone, 3, 80) ||
            !TimeZoneInfo.TryFindSystemTimeZoneById(input.Timezone!.Trim(), out _) ||
            !double.IsFinite(input.Latitude) || !double.IsFinite(input.Longitude) ||
            input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180 ||
            !TextOk(input.BankCode, 2, 32) || !TextOk(input.AccountName, 2, 160) ||
            !TextOk(input.AccountNumber, 6, 40) || !input.AccountNumber!.All(char.IsDigit))
            return Problem(http, 400, "VALIDATION_FAILED");
        var user = await UserAsync(http, db, ct);
        if (user?.AccountType != AccountType.VenueOperator || user.Status != UserStatus.Active)
            return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var venue = await db.Venues.FromSqlInterpolated($"SELECT * FROM venues WHERE id = {venueId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (venue is null || !await db.BusinessMemberships.AnyAsync(m => m.BusinessId == venue.BusinessId &&
            m.UserId == user.Id && m.Role == "OWNER" && m.Status == "ACTIVE", ct))
            return Problem(http, 404, "NOT_FOUND");
        var business = await db.Businesses.SingleAsync(b => b.Id == venue.BusinessId, ct);
        if (venue.Status != "PUBLISHED" || business.Status != "ACTIVE") return Problem(http, 409, "STATE_CONFLICT");
        if (!await db.MediaUploads.AnyAsync(u => u.Id == input.QrUploadId && u.OwnerUserId == user.Id &&
            u.VenueId == venue.Id && u.Purpose == "QR" && u.Status == "READY", ct))
            return Problem(http, 400, "VALIDATION_FAILED");
        if (await db.ApprovalRequests.AnyAsync(a => a.BusinessId == business.Id && a.Status == "PENDING", ct))
            return Problem(http, 409, "STATE_CONFLICT");
        var snapshot = new RevisionSnapshot(venue.Version, input.Address!.Trim(), input.Contact!.Trim(),
            input.Timezone!.Trim(), input.Latitude,
            input.Longitude, input.BankCode!.Trim().ToUpperInvariant(), input.AccountName!.Trim(),
            input.AccountNumber!, input.QrUploadId);
        var approval = new ApprovalRequest { BusinessId = business.Id, VenueId = venue.Id,
            Kind = "VENUE_REVISION", SubmittedBy = user.Id, Snapshot = JsonSerializer.Serialize(snapshot, Json),
            SubmittedAt = DateTimeOffset.UtcNow };
        db.ApprovalRequests.Add(approval);
        Outbox(db, "APPROVAL_SUBMITTED", null, approval.Id, null);
        Audit(db, user.Id, "venue.revision_submitted", "venue", venue.Id, http);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(http, new { approval.Id, approval.Kind, approval.Status, venueId });
    }

    private sealed record ApprovalCursor(int Version, DateTimeOffset SubmittedAt, Guid Id, string Query, string Kind);

    private static async Task<IResult> ListApprovals(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Admin(await UserAsync(http, db, ct))) return Problem(http, 403, "FORBIDDEN");
        var query = from a in db.ApprovalRequests.AsNoTracking() where a.Status == "PENDING"
            join b in db.Businesses.AsNoTracking() on a.BusinessId equals b.Id
            select new { a.Id, a.BusinessId, businessName = b.Name, a.Kind, a.Status, a.SubmittedAt };
        // Preserve the original response for clients deployed before the paged queue.
        if (http.Request.Query.Count == 0)
            return Ok(http, await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Take(100).ToListAsync(ct));
        var parameters = http.Request.Query;
        if (parameters.Any(x => x.Value.Count != 1 || !new[] { "paged", "q", "kind", "limit", "before" }.Contains(x.Key)) ||
            !string.Equals(parameters["paged"].ToString(), "true", StringComparison.OrdinalIgnoreCase))
            return Problem(http, 400, "VALIDATION_FAILED");
        var q = parameters["q"].ToString().Trim(); var kind = parameters["kind"].ToString();
        var limit = 20;
        if (q.Length > 120 || (kind.Length > 0 && kind is not ("ONBOARDING" or "VENUE_REVISION")) ||
            (parameters.ContainsKey("limit") && (!int.TryParse(parameters["limit"], out limit) || limit is < 1 or > 100)))
            return Problem(http, 400, "VALIDATION_FAILED");
        ApprovalCursor? cursor = null;
        if (parameters.ContainsKey("before"))
        {
            var value = parameters["before"].ToString();
            if (value.Length is < 1 or > 2048) return Problem(http, 400, "VALIDATION_FAILED");
            try { cursor = JsonSerializer.Deserialize<ApprovalCursor>(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(value), Json); }
            catch (Exception error) when (error is FormatException or JsonException or ArgumentException)
            { return Problem(http, 400, "VALIDATION_FAILED"); }
            if (cursor is null || cursor.Version != 1 || cursor.Id == Guid.Empty || cursor.SubmittedAt == default ||
                cursor.SubmittedAt.Offset != TimeSpan.Zero || cursor.Query != q || cursor.Kind != kind)
                return Problem(http, 400, "VALIDATION_FAILED");
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var pendingCount = await query.CountAsync(ct);
        if (q.Length > 0)
        {
            var pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x => EF.Functions.ILike(x.businessName, pattern, "\\"));
        }
        if (kind.Length > 0) query = query.Where(x => x.Kind == kind);
        var totalCount = await query.CountAsync(ct);
        if (cursor is not null)
        {
            var afterTime = cursor.SubmittedAt; var afterId = cursor.Id;
            query = query.Where(x => x.SubmittedAt > afterTime || (x.SubmittedAt == afterTime && x.Id.CompareTo(afterId) > 0));
        }
        var rows = await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
        var items = rows.Take(limit).ToArray();
        var nextCursor = rows.Count > limit ? Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            JsonSerializer.SerializeToUtf8Bytes(new ApprovalCursor(1, items[^1].SubmittedAt, items[^1].Id, q, kind), Json)) : null;
        await tx.CommitAsync(ct);
        return Ok(http, new { items, nextCursor, totalCount, pendingCount });
    }

    private static async Task<IResult> GetApproval(HttpContext http, ShuttleBookDbContext db, Guid requestId, CancellationToken ct)
    {
        if (!Admin(await UserAsync(http, db, ct))) return Problem(http, 403, "FORBIDDEN");
        var approval = await db.ApprovalRequests.AsNoTracking().SingleOrDefaultAsync(a => a.Id == requestId, ct);
        if (approval is null) return Problem(http, 404, "NOT_FOUND");
        object? current = null;
        if (approval.Kind == "VENUE_REVISION" && approval.VenueId is Guid venueId)
        {
            current = await (from venue in db.Venues.AsNoTracking()
                where venue.Id == venueId && venue.BusinessId == approval.BusinessId
                join payment in db.VenuePaymentAccounts.AsNoTracking() on venue.Id equals payment.VenueId
                select new { venueName = venue.Name, venue.Version, venue.Address, venue.Contact, venue.Timezone,
                    venue.Latitude, venue.Longitude, payment.BankCode, payment.AccountName, payment.AccountNumber,
                    payment.QrUploadId }).SingleOrDefaultAsync(ct);
        }
        return Ok(http, new { approval.Id, approval.BusinessId, approval.VenueId, approval.Kind, approval.Status, approval.Snapshot,
            approval.Reason, approval.SubmittedAt, approval.ReviewedAt, current });
    }

    private static async Task<IResult> Approve(HttpContext http, ShuttleBookDbContext db, Guid requestId, CancellationToken ct)
    {
        var admin = await UserAsync(http, db, ct);
        if (!Admin(admin)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var approval = await db.ApprovalRequests.FromSqlInterpolated(
            $"SELECT * FROM approval_requests WHERE id = {requestId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (approval is null) return Problem(http, 404, "NOT_FOUND");
        if (approval.Status != "PENDING") return Problem(http, 409, "STATE_CONFLICT");
        if (approval.Kind == "VENUE_REVISION")
        {
            var proposed = JsonSerializer.Deserialize<RevisionSnapshot>(approval.Snapshot, Json);
            if (proposed is null || approval.VenueId is null) return Problem(http, 409, "STATE_CONFLICT");
            var venue = await db.Venues.FromSqlInterpolated(
                $"SELECT * FROM venues WHERE id = {approval.VenueId.Value} FOR UPDATE").SingleAsync(ct);
            var currentBusiness = await db.Businesses.SingleAsync(b => b.Id == venue.BusinessId, ct);
            if (venue.Status != "PUBLISHED" || currentBusiness.Status != "ACTIVE" || venue.Version != proposed.Version ||
                !await db.MediaUploads.AnyAsync(u => u.Id == proposed.QrUploadId && u.VenueId == venue.Id &&
                    u.OwnerUserId == approval.SubmittedBy && u.Purpose == "QR" && u.Status == "READY", ct))
                return Problem(http, 409, "VERSION_CONFLICT");
            var payment = await db.VenuePaymentAccounts.SingleAsync(p => p.VenueId == venue.Id, ct);
            venue.Address = proposed.Address; venue.Contact = proposed.Contact; venue.Timezone = proposed.Timezone;
            venue.Latitude = proposed.Latitude; venue.Longitude = proposed.Longitude; venue.Version++;
            venue.UpdatedAt = DateTimeOffset.UtcNow;
            payment.BankCode = proposed.BankCode; payment.AccountName = proposed.AccountName;
            payment.AccountNumber = proposed.AccountNumber; payment.QrUploadId = proposed.QrUploadId;
            approval.Status = "APPROVED"; approval.ReviewedBy = admin!.Id; approval.ReviewedAt = DateTimeOffset.UtcNow;
            Outbox(db, "APPROVAL_APPROVED", approval.SubmittedBy, approval.Id, null);
            Audit(db, admin.Id, "venue.revision_approved", "venue", venue.Id, http);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Ok(http, new { approval.Id, approval.Status, venueId = venue.Id });
        }
        var business = await db.Businesses.FromSqlInterpolated(
            $"SELECT * FROM businesses WHERE id = {approval.BusinessId} FOR UPDATE").SingleAsync(ct);
        if (business.Status != "PENDING_APPROVAL") return Problem(http, 409, "STATE_CONFLICT");
        var membership = await db.BusinessMemberships.SingleOrDefaultAsync(m => m.BusinessId == business.Id &&
            m.UserId == approval.SubmittedBy && m.Role == "OWNER" && m.Status == "PENDING", ct);
        var owner = await db.Users.SingleOrDefaultAsync(u => u.Id == approval.SubmittedBy, ct);
        var venues = await db.Venues.Where(v => v.BusinessId == business.Id).ToListAsync(ct);
        if (membership is null || owner is null || owner.AccountType != AccountType.VenueOperator ||
            owner.Status != UserStatus.PendingOnboarding || venues.Count == 0 ||
            venues.Any(v => v.Status != "PENDING_APPROVAL")) return Problem(http, 409, "STATE_CONFLICT");
        var venueIds = venues.Select(v => v.Id).ToArray();
        var courts = await db.Courts.Where(c => venueIds.Contains(c.VenueId)).ToListAsync(ct);
        if (courts.Count == 0) return Problem(http, 409, "INCOMPLETE_PROFILE");
        foreach (var venue in venues)
        {
            if (!TextOk(venue.Contact, 3, 160) || venue.ImageUploadId is null ||
                !await db.MediaUploads.AnyAsync(u => u.Id == venue.ImageUploadId && u.VenueId == venue.Id &&
                    u.Purpose == "VENUE_IMAGE" && u.Status == "READY", ct))
                return Problem(http, 409, "INCOMPLETE_PROFILE");
            var payment = await db.VenuePaymentAccounts.SingleOrDefaultAsync(p => p.VenueId == venue.Id, ct);
            if (payment is null || !await db.MediaUploads.AnyAsync(u => u.Id == payment.QrUploadId &&
                u.VenueId == venue.Id && u.Purpose == "QR" && u.Status == "READY", ct))
                return Problem(http, 409, "INCOMPLETE_PROFILE");
            var venueCourts = courts.Where(c => c.VenueId == venue.Id).ToArray();
            if (venueCourts.Length == 0) return Problem(http, 409, "INCOMPLETE_PROFILE");
            foreach (var court in venueCourts)
            {
                var hours = await db.CourtOperatingHours.Where(h => h.CourtId == court.Id).ToListAsync(ct);
                var prices = await db.PricingRules.Where(p => p.CourtId == court.Id).ToListAsync(ct);
                if (!ValidStoredSchedule(hours, prices)) return Problem(http, 409, "INCOMPLETE_PROFILE");
            }
        }
        business.Status = "ACTIVE"; business.Version++; business.UpdatedAt = DateTimeOffset.UtcNow;
        membership.Status = "ACTIVE"; owner.Status = UserStatus.Active; owner.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var venue in venues) venue.Status = "PUBLISHED";
        foreach (var court in courts) court.Status = "ACTIVE";
        approval.Status = "APPROVED"; approval.ReviewedBy = admin!.Id; approval.ReviewedAt = DateTimeOffset.UtcNow;
        Outbox(db, "APPROVAL_APPROVED", approval.SubmittedBy, approval.Id, null);
        Audit(db, admin.Id, "venue.published", "business", business.Id, http);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(http, new { approval.Id, approval.Status, businessId = business.Id });
    }

    private static async Task<IResult> RequestChanges(HttpContext http, ShuttleBookDbContext db, Guid requestId, CancellationToken ct)
    {
        var (input, error) = await Read<ReasonInput>(http, ["reason"], ct);
        if (error is not null) return error;
        if (!TextOk(input!.Reason, 10, 1000)) return Problem(http, 400, "VALIDATION_FAILED");
        var admin = await UserAsync(http, db, ct);
        if (!Admin(admin)) return Problem(http, 403, "FORBIDDEN");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var approval = await db.ApprovalRequests.FromSqlInterpolated(
            $"SELECT * FROM approval_requests WHERE id = {requestId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (approval is null) return Problem(http, 404, "NOT_FOUND");
        if (approval.Status != "PENDING") return Problem(http, 409, "STATE_CONFLICT");
        if (approval.Kind == "VENUE_REVISION")
        {
            approval.Status = "CHANGES_REQUESTED"; approval.Reason = input.Reason!.Trim();
            approval.ReviewedBy = admin!.Id; approval.ReviewedAt = DateTimeOffset.UtcNow;
            Outbox(db, "APPROVAL_CHANGES_REQUESTED", approval.SubmittedBy, approval.Id, approval.Reason);
            Audit(db, admin.Id, "venue.revision_changes_requested", "venue", approval.VenueId!.Value, http);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Ok(http, new { approval.Id, approval.Status, approval.Reason });
        }
        var business = await db.Businesses.FromSqlInterpolated(
            $"SELECT * FROM businesses WHERE id = {approval.BusinessId} FOR UPDATE").SingleAsync(ct);
        if (business.Status != "PENDING_APPROVAL") return Problem(http, 409, "STATE_CONFLICT");
        var venues = await db.Venues.Where(v => v.BusinessId == business.Id).ToListAsync(ct);
        business.Status = "DRAFT"; business.Version++; business.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var venue in venues) venue.Status = "DRAFT";
        approval.Status = "CHANGES_REQUESTED"; approval.Reason = input.Reason!.Trim();
        approval.ReviewedBy = admin!.Id; approval.ReviewedAt = DateTimeOffset.UtcNow;
        Outbox(db, "APPROVAL_CHANGES_REQUESTED", approval.SubmittedBy, approval.Id, approval.Reason);
        Audit(db, admin.Id, "venue.changes_requested", "business", business.Id, http);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Ok(http, new { approval.Id, approval.Status, approval.Reason });
    }

    private static async Task<User?> UserAsync(HttpContext http, ShuttleBookDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var id)) return null;
        return await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
    }
    private static bool Partner(User? user) => user?.AccountType == AccountType.VenueOperator &&
        user.Status is UserStatus.PendingOnboarding or UserStatus.Active;
    private static bool Admin(User? user) => user?.AccountType == AccountType.Admin && user.Status == UserStatus.Active;
    private static Task<bool> Owns(ShuttleBookDbContext db, Guid userId, Guid businessId, CancellationToken ct) =>
        db.BusinessMemberships.AnyAsync(m => m.UserId == userId && m.BusinessId == businessId && m.Role == "OWNER" &&
            (m.Status == "PENDING" || m.Status == "ACTIVE"), ct);
    private static async Task<Business?> OwnedBusiness(ShuttleBookDbContext db, Guid userId, Guid businessId, CancellationToken ct) =>
        await Owns(db, userId, businessId, ct) ? await db.Businesses.SingleOrDefaultAsync(b => b.Id == businessId, ct) : null;

    private static async Task<(Venue?, User?, IResult?)> EditableVenue(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, CancellationToken ct)
    {
        var user = await UserAsync(http, db, ct);
        if (!Partner(user)) return (null, null, Problem(http, 403, "FORBIDDEN"));
        var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == venueId, ct);
        if (venue is null || !await Owns(db, user!.Id, venue.BusinessId, ct))
            return (null, null, Problem(http, 404, "NOT_FOUND"));
        var business = await db.Businesses.SingleAsync(b => b.Id == venue.BusinessId, ct);
        if (business.Status != "DRAFT" || venue.Status != "DRAFT")
            return (null, null, Problem(http, 409, "STATE_CONFLICT"));
        return (venue, user, null);
    }
    private static async Task<(Court?, User?, IResult?)> EditableCourt(HttpContext http, ShuttleBookDbContext db,
        Guid courtId, CancellationToken ct)
    {
        var court = await db.Courts.SingleOrDefaultAsync(c => c.Id == courtId, ct);
        if (court is null) return (null, null, Problem(http, 404, "NOT_FOUND"));
        var (_, user, error) = await EditableVenue(http, db, court.VenueId, ct);
        return error is null ? (court, user, null) : (null, null, error);
    }
    private static async Task<bool> LockDraftBusiness(ShuttleBookDbContext db, Guid venueId, CancellationToken ct)
    {
        var venue = await db.Venues.SingleAsync(v => v.Id == venueId, ct);
        var business = await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id = {venue.BusinessId} FOR UPDATE")
            .SingleAsync(ct);
        return business.Status == "DRAFT" && venue.Status == "DRAFT";
    }

    private static bool ValidVenue(VenueInput input) => TextOk(input.Name, 2, 160) && TextOk(input.Address, 8, 400) &&
        TextOk(input.Contact, 3, 160) && TextOk(input.Timezone, 3, 80) &&
        TimeZoneInfo.TryFindSystemTimeZoneById(input.Timezone!.Trim(), out _) &&
        double.IsFinite(input.Latitude) && double.IsFinite(input.Longitude) &&
        input.Latitude is >= -90 and <= 90 && input.Longitude is >= -180 and <= 180;
    private static bool TextOk(string? value, int min, int max) => value is not null &&
        value.Trim().Length >= min && value.Trim().Length <= max;
    private static IResult? CheckVersion(HttpContext http, long version)
    {
        var match = http.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(match)) return Problem(http, 428, "PRECONDITION_REQUIRED");
        return match == $"\"{version}\"" ? null : Problem(http, 412, "PRECONDITION_FAILED");
    }
    private static bool SlotTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", out time) && time.Minute is 0 or 30;
    private static bool ValidStoredSchedule(List<CourtOperatingHour> hours, List<PricingRule> prices)
    {
        if (hours.Count == 0 || prices.Count == 0 || hours.Select(h => h.DayOfWeek).Distinct().Count() != hours.Count)
            return false;
        foreach (var hour in hours)
        {
            if (hour.OpensAt >= hour.ClosesAt || hour.OpensAt.Minute is not (0 or 30) ||
                hour.ClosesAt.Minute is not (0 or 30)) return false;
            var cursor = hour.OpensAt;
            foreach (var price in prices.Where(p => p.DayOfWeek == hour.DayOfWeek).OrderBy(p => p.StartsAt))
            {
                if (price.PricePerSlot <= 0 || price.StartsAt != cursor || price.EndsAt > hour.ClosesAt ||
                    price.StartsAt.Minute is not (0 or 30) || price.EndsAt.Minute is not (0 or 30)) return false;
                cursor = price.EndsAt;
            }
            if (cursor != hour.ClosesAt) return false;
        }
        return prices.All(p => hours.Any(h => h.DayOfWeek == p.DayOfWeek));
    }
    private static string Mask(string value) => value.Length <= 4 ? "****" : new string('*', value.Length - 4) + value[^4..];

    private static void Audit(ShuttleBookDbContext db, Guid actor, string action, string entityType, Guid entityId, HttpContext http) =>
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor, Action = action, EntityType = entityType,
            EntityId = entityId, CorrelationId = http.TraceIdentifier, CreatedAt = DateTimeOffset.UtcNow });
    private static void Outbox(ShuttleBookDbContext db, string eventType, Guid? targetUserId, Guid entityId, string? reason)
    {
        var now = DateTimeOffset.UtcNow;
        db.OutboxMessages.Add(new OutboxMessage { EventType = eventType, TargetUserId = targetUserId,
            EntityId = entityId, Payload = JsonSerializer.Serialize(new { reason }, Json),
            CreatedAt = now, NextAttemptAt = now });
    }
    private static object Envelope(object data, HttpContext http) => new { data, traceId = http.TraceIdentifier };
    private static IResult Ok(HttpContext http, object data) => Results.Ok(Envelope(data, http));
    private static IResult Problem(HttpContext http, int status, string code) =>
        CustomerRegistrationEndpoints.Problem(http, status, code);

    private static async Task<(T?, IResult?)> Read<T>(HttpContext http, HashSet<string> fields, CancellationToken ct)
    {
        if (http.Request.ContentLength is > 32_768) return (default, Problem(http, 400, "VALIDATION_FAILED"));
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > 32_768) return (default, Problem(http, 400, "VALIDATION_FAILED"));
                buffer.Write(chunk, 0, count);
            }
            buffer.Position = 0;
            using var document = await JsonDocument.ParseAsync(buffer,
                new JsonDocumentOptions { MaxDepth = 12 }, ct);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return (default, Problem(http, 400, "VALIDATION_FAILED"));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!fields.Contains(property.Name)) return (default, Problem(http, 400, "UNSUPPORTED_FIELD"));
                if (!seen.Add(property.Name)) return (default, Problem(http, 400, "VALIDATION_FAILED"));
            }
            if (fields.Any(field => !seen.Contains(field))) return (default, Problem(http, 400, "VALIDATION_FAILED"));
            var input = document.RootElement.Deserialize<T>(Json);
            return input is null ? (default, Problem(http, 400, "VALIDATION_FAILED")) : (input, null);
        }
        catch (JsonException) { return (default, Problem(http, 400, "VALIDATION_FAILED")); }
    }
}
