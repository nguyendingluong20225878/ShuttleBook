using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Bookings;

public static partial class PaymentEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] Statuses = ["AWAITING_TRANSFER", "AWAITING_OWNER_CONFIRMATION", "NEEDS_REVIEW", "CONFIRMED", "EXPIRED", "PAYMENT_REJECTED"];

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/operator/venues/{venueId:guid}/bookings", OperatorList).RequireAuthorization();
        app.MapGet("/api/v1/operator/bookings/{id:guid}", OperatorDetail).RequireAuthorization();
        app.MapGet("/api/v1/payments/{id:guid}", PaymentDetail).RequireAuthorization();
        app.MapPost("/api/v1/bookings/{id:guid}/transfer-evidence", Report).RequireAuthorization().RequireRateLimiting("booking-create");
        app.MapPost("/api/v1/operator/bookings/{id:guid}/confirm-payment", Confirm).RequireAuthorization().RequireRateLimiting("booking-create");
        app.MapPost("/api/v1/operator/bookings/{id:guid}/reject-payment", Decide).RequireAuthorization().RequireRateLimiting("booking-create");
    }

    private static async Task<IResult> OperatorList(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid venueId, CancellationToken ct)
    {
        var (user, error) = await CurrentUser(http, db, AccountType.VenueOperator, ct);
        if (error is not null) return error;
        var query = http.Request.Query; var limit = 20; Guid? before = null;
        DateOnly? from = null, to = null;
        if (query.Any(x => x.Key is not ("status" or "dateFrom" or "dateTo" or "limit" or "before") || x.Value.Count != 1) ||
            (query.ContainsKey("limit") && (!int.TryParse(query["limit"], out limit) || limit is < 1 or > 100)) ||
            (query.ContainsKey("before") && (!Guid.TryParse(query["before"], out var parsed) || (before = parsed) == Guid.Empty)) ||
            (query.ContainsKey("status") && !Statuses.Contains(query["status"].ToString()))) return Error(http, 400, "VALIDATION_FAILED");
        if (query.ContainsKey("dateFrom"))
        {
            if (!DateOnly.TryParseExact(query["dateFrom"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) return Error(http, 400, "VALIDATION_FAILED");
            from = value;
        }
        if (query.ContainsKey("dateTo"))
        {
            if (!DateOnly.TryParseExact(query["dateTo"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) return Error(http, 400, "VALIDATION_FAILED");
            to = value;
        }
        if (from > to || (from is DateOnly f && to is DateOnly t && t.DayNumber - f.DayNumber > 366)) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        if (!await HasVenueScope(db, user!.Id, venueId, ct)) return Error(http, 404, "NOT_FOUND");
        var rows = db.Bookings.AsNoTracking().Where(x => x.VenueId == venueId);
        if (from is DateOnly first) rows = rows.Where(x => x.LocalDate >= first);
        if (to is DateOnly last) rows = rows.Where(x => x.LocalDate <= last);
        var counts = new
        {
            awaitingOwnerConfirmation = await rows.CountAsync(x => x.Status == "AWAITING_OWNER_CONFIRMATION", ct),
            needsReview = await rows.CountAsync(x => x.Status == "NEEDS_REVIEW", ct)
        };
        if (query.ContainsKey("status")) { var status = query["status"].ToString(); rows = rows.Where(x => x.Status == status); }
        if (before is Guid cursor) rows = rows.Where(x => x.Id.CompareTo(cursor) < 0);
        var overdueCutoff = clock.GetUtcNow().AddMinutes(-ConfirmationAlerts.SlaMinutes);
        var items = await (from b in rows join p in db.BookingPayments on b.Id equals p.BookingId
                           orderby b.Id descending select new
                           {
                               bookingId = b.Id, b.BookingNo, b.Status, b.VenueId, b.CourtId, b.VenueName, b.CourtName,
                               b.Timezone, amountExact = b.Amount.ToString(CultureInfo.InvariantCulture), date = b.LocalDate.ToString("yyyy-MM-dd"), localStart = b.LocalStart.ToString("HH:mm"),
                               localEnd = b.LocalEnd.ToString("HH:mm"), b.StartsAt, b.EndsAt, b.Amount, currency = "VND",
                               b.PaymentDeadline, b.Version, b.CreatedAt, firstReportedAt = p.FirstReportedAt,
                               isOverdue = p.FirstReportedAt <= overdueCutoff && (b.Status == "AWAITING_OWNER_CONFIRMATION" || b.Status == "NEEDS_REVIEW")
                           }).Take(limit + 1).ToListAsync(ct);
        return Ok(http, new { items = items.Take(limit), nextCursor = items.Count > limit ? items[limit - 1].bookingId.ToString() : null, counts });
    }

    private static async Task<IResult> OperatorDetail(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var (user, error) = await CurrentUser(http, db, AccountType.VenueOperator, ct); if (error is not null) return error;
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var b = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (b is null || !await HasVenueScope(db, user!.Id, b.VenueId, ct)) return Error(http, 404, "NOT_FOUND");
        return Ok(http, await BookingReadModel.Data(db, id, true, ct, clock.GetUtcNow()));
    }

    private static async Task<IResult> PaymentDetail(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct)
    {
        var (user, error) = await CurrentUser(http, db, null, ct); if (error is not null) return error;
        if (user!.AccountType == AccountType.Admin) return Error(http, 403, "FORBIDDEN");
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var b = await (from booking in db.Bookings join payment in db.BookingPayments on booking.Id equals payment.BookingId
                       where payment.Id == id select booking).AsNoTracking().SingleOrDefaultAsync(ct);
        if (b is null || (user.AccountType == AccountType.Customer ? b.CustomerId != user.Id : !await HasVenueScope(db, user.Id, b.VenueId, ct))) return Error(http, 404, "NOT_FOUND");
        var detail = JsonSerializer.SerializeToElement(await BookingReadModel.Data(db, b.Id, user.AccountType == AccountType.VenueOperator, ct, clock.GetUtcNow()), Json);
        return Ok(http, new { bookingId = b.Id, b.Version, payment = detail.GetProperty("payment") });
    }

    public static async Task<bool> HasVenueScope(ShuttleBookDbContext db, Guid userId, Guid venueId, CancellationToken ct) =>
        await (from v in db.Venues join b in db.Businesses on v.BusinessId equals b.Id
               join m in db.BusinessMemberships on b.Id equals m.BusinessId
               where v.Id == venueId && b.Status == "ACTIVE" && m.UserId == userId && m.Role == "OWNER" && m.Status == "ACTIVE" &&
                   db.Users.Any(u => u.Id == userId && u.Status == UserStatus.Active && u.AccountType == AccountType.VenueOperator)
               select m.Id).AnyAsync(ct);

    private static async Task<(User?, IResult?)> CurrentUser(HttpContext http, ShuttleBookDbContext db, AccountType? type, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var id)) return (null, Error(http, 401, "UNAUTHORIZED"));
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || user.Status != UserStatus.Active) return (null, Error(http, 401, "UNAUTHORIZED"));
        if (type is not null && user.AccountType != type) return (null, Error(http, 403, "FORBIDDEN"));
        return (user, null);
    }

    private static IResult Ok(HttpContext http, object data) { http.Response.Headers.CacheControl = "no-store"; return Results.Ok(new { data, traceId = http.TraceIdentifier }); }
    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
}
