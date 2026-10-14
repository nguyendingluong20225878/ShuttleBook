using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Onboarding;

public static class OwnerPresentationEndpoints
{
    public static void MapOwnerPresentationEndpoints(this WebApplication app)
    {
        var owner = app.MapGroup("/api/v1/operator").RequireAuthorization();
        owner.MapPut("/venues/{venueId:guid}/image", SetVenueImage);
        owner.MapDelete("/venues/{venueId:guid}/image", RemoveVenueImage);
    }

    private static Task<IResult> SetVenueImage(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, CancellationToken ct) => SetImage(http, db, venueId, false, ct);
    private static Task<IResult> RemoveVenueImage(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, CancellationToken ct) => SetImage(http, db, venueId, true, ct);

    private static async Task<IResult> SetImage(HttpContext http, ShuttleBookDbContext db,
        Guid venueId, bool remove, CancellationToken ct)
    {
        if (http.Request.Query.Count != 0) return Problem(http, 400, "VALIDATION_FAILED");
        Guid? uploadId = null;
        if (!remove)
        {
            if (http.Request.ContentLength is > 2048) return Problem(http, 400, "VALIDATION_FAILED");
            try
            {
                using var buffer = new MemoryStream();
                var chunk = new byte[1024];
                int count;
                while ((count = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
                {
                    if (buffer.Length + count > 2048) return Problem(http, 400, "VALIDATION_FAILED");
                    buffer.Write(chunk, 0, count);
                }
                buffer.Position = 0;
                using var body = await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
                if (body.RootElement.ValueKind != JsonValueKind.Object ||
                    body.RootElement.EnumerateObject().Count() != 1 ||
                    !body.RootElement.TryGetProperty("uploadId", out var property) ||
                    property.ValueKind != JsonValueKind.String || !property.TryGetGuid(out var parsed) || parsed == Guid.Empty)
                    return Problem(http, 400, "VALIDATION_FAILED");
                uploadId = parsed;
            }
            catch (JsonException) { return Problem(http, 400, "VALIDATION_FAILED"); }
        }
        else if (http.Request.ContentLength is > 0 || await http.Request.Body.ReadAsync(new byte[1], ct) > 0)
            return Problem(http, 400, "VALIDATION_FAILED");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (user, venue, _, error) = await OwnerMediaScope.Resolve(http, db, venueId, null, true, ct);
        if (error is not null) return error;
        var version = venue!.Version;
        var match = http.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(match)) return Problem(http, 428, "PRECONDITION_REQUIRED");
        if (match != $"\"{version}\"") return Problem(http, 412, "PRECONDITION_FAILED");
        if (uploadId is Guid id && !await db.MediaUploads.AnyAsync(x => x.Id == id &&
                x.OwnerUserId == user!.Id && x.VenueId == venue!.Id && x.CourtId == null &&
                x.BookingId == null && x.Status == "READY" &&
                x.Purpose == "VENUE_IMAGE", ct))
            return Problem(http, 400, "UPLOAD_INVALID");
        venue.ImageUploadId = uploadId;
        venue.Version++;
        venue.UpdatedAt = DateTimeOffset.UtcNow;
        version = venue.Version;
        db.AuditEvents.Add(new AuditEvent { ActorUserId = user!.Id,
            Action = remove ? "venue.image_removed" : "venue.image_updated",
            EntityType = "venue", EntityId = venue.Id,
            CorrelationId = http.TraceIdentifier, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        http.Response.Headers.ETag = $"\"{version}\"";
        return Results.Ok(new { data = new { id = venue.Id, imageUploadId = uploadId, version },
            traceId = http.TraceIdentifier });
    }

    private static IResult Problem(HttpContext http, int status, string code) =>
        CustomerRegistrationEndpoints.Problem(http, status, code);
}

// The same parent/status checks protect presigning and direct image attachment.
// Writes lock business, venue, then court, matching approval submission order.
internal static class OwnerMediaScope
{
    internal static async Task<(User? User, Venue? Venue, Court? Court, IResult? Error)> Resolve(
        HttpContext http, ShuttleBookDbContext db, Guid? venueId, Guid? courtId, bool lockRows, CancellationToken ct)
    {
        var user = Guid.TryParse(http.User.FindFirstValue("sub"), out var userId)
            ? await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct) : null;
        if (user?.AccountType != AccountType.VenueOperator ||
            user.Status is not (UserStatus.PendingOnboarding or UserStatus.Active))
            return (null, null, null, Problem(http, 403, "FORBIDDEN"));
        var court = courtId is Guid requestedCourt
            ? await db.Courts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestedCourt, ct) : null;
        if (courtId.HasValue && (court is null || venueId.HasValue && court.VenueId != venueId))
            return (null, null, null, Problem(http, 404, "NOT_FOUND"));
        venueId ??= court?.VenueId;
        var venue = await db.Venues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == venueId, ct);
        if (venue is null || !await db.BusinessMemberships.AnyAsync(x => x.BusinessId == venue.BusinessId &&
                x.UserId == user.Id && x.Role == "OWNER" && (x.Status == "PENDING" || x.Status == "ACTIVE"), ct))
            return (null, null, null, Problem(http, 404, "NOT_FOUND"));
        var business = lockRows
            ? await db.Businesses.FromSqlInterpolated($"SELECT * FROM businesses WHERE id={venue.BusinessId} FOR UPDATE").SingleAsync(ct)
            : await db.Businesses.AsNoTracking().SingleAsync(x => x.Id == venue.BusinessId, ct);
        var membership = await db.BusinessMemberships.AsNoTracking().SingleOrDefaultAsync(x =>
            x.BusinessId == business.Id && x.UserId == user.Id && x.Role == "OWNER", ct);
        if (membership is null || membership.Status is not ("PENDING" or "ACTIVE"))
            return (null, null, null, Problem(http, 404, "NOT_FOUND"));
        if (business.Status is not ("DRAFT" or "ACTIVE") || business.Status == "ACTIVE" &&
            (user.Status != UserStatus.Active || membership.Status != "ACTIVE"))
            return (null, null, null, Problem(http, 409, "STATE_CONFLICT"));
        if (lockRows)
            venue = await db.Venues.FromSqlInterpolated($"SELECT * FROM venues WHERE id={venue.Id} FOR UPDATE").SingleAsync(ct);
        if (venue.Status != "DRAFT" && !(venue.Status == "PUBLISHED" && business.Status == "ACTIVE"))
            return (null, null, null, Problem(http, 409, "STATE_CONFLICT"));
        if (courtId is Guid lockedCourt)
        {
            if (lockRows)
                court = await db.Courts.FromSqlInterpolated($"SELECT * FROM courts WHERE id={lockedCourt} FOR UPDATE").SingleAsync(ct);
            if (court!.Status != "INACTIVE" && !(court.Status == "ACTIVE" && venue.Status == "PUBLISHED" && business.Status == "ACTIVE"))
                return (null, null, null, Problem(http, 409, "STATE_CONFLICT"));
        }
        return (user, venue, court, null);
    }

    private static IResult Problem(HttpContext http, int status, string code) =>
        CustomerRegistrationEndpoints.Problem(http, status, code);
}
