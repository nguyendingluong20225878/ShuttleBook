using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Api.Bookings;

/// <summary>Private booking DTO. Booking/payment are read together; callers provide a snapshot transaction or booking lock.</summary>
public static class BookingReadModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<object> Data(ShuttleBookDbContext db, Guid id, bool operatorView, CancellationToken ct, DateTimeOffset? now = null)
    {
        var row = await (from booking in db.Bookings.AsNoTracking()
                         join payment in db.BookingPayments.AsNoTracking() on booking.Id equals payment.BookingId
                         join v in db.Venues.AsNoTracking() on booking.VenueId equals v.Id
                         join u in db.Users.AsNoTracking() on booking.CustomerId equals u.Id
                         where booking.Id == id
                         select new { Booking = booking, Payment = payment, v.BusinessId, u.Email, u.Phone }).SingleAsync(ct);
        var b = row.Booking; var p = row.Payment;
        var r = JsonSerializer.Deserialize<BookingEndpoints.Recipient>(p.RecipientSnapshot, Json)!;
        var evidence = await db.PaymentEvidence.AsNoTracking().Where(x => x.BookingId == id)
            .OrderBy(x => x.ReportedAt).ThenBy(x => x.Id).Select(x => new
            {
                evidenceId = x.Id, x.Kind, x.BankReference, x.Note, x.ReportedAt,
                proofUrl = x.ProofUploadId == null ? null : "/api/v1/uploads/" + x.ProofUploadId + "/view"
            }).ToListAsync(ct);
        var decisions = await db.PaymentDecisions.AsNoTracking().Where(x => x.BookingId == id)
            .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).Select(x => new
            {
                decisionId = x.Id, x.Resolution, x.ReasonCode, x.Reason, x.ConfirmedAmount,
                x.BankReference, x.Note, x.DecidedAt,
                confirmedAmountExact = x.ConfirmedAmount == null ? null : x.ConfirmedAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }).ToListAsync(ct);
        return new
        {
            bookingId = b.Id, b.BookingNo, b.BookingType, b.Status, b.VenueId, b.CourtId, b.VenueName, b.CourtName,
            b.Timezone, date = b.LocalDate.ToString("yyyy-MM-dd"), localStart = b.LocalStart.ToString("HH:mm"),
            localEnd = b.LocalEnd.ToString("HH:mm"), b.StartsAt, b.EndsAt, b.Amount,
            amountExact = b.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), currency = "VND", b.PaymentDeadline,
            b.Version, b.CreatedAt, b.ExpiredAt,
            slots = JsonSerializer.Deserialize<List<BookingEndpoints.PriceSlot>>(b.Slots, Json),
            b.BookingBlockMinutes, b.MinimumBookingMinutes, b.HoldMinutes,
            businessId = operatorView ? (Guid?)row.BusinessId : null,
            customer = operatorView ? new { maskedContact = Mask(row.Email, row.Phone) } : null,
            payment = new
            {
                paymentId = p.Id, p.Status, p.ExpectedAmount,
                expectedAmountExact = p.ExpectedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                confirmedAmountExact = p.ConfirmedAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture), r.BankCode, r.AccountName,
                maskedAccountNumber = new string('*', Math.Max(0, r.AccountNumber.Length - 4)) + r.AccountNumber[^Math.Min(4, r.AccountNumber.Length)..],
                qrUrl = !operatorView && b.Status == "AWAITING_TRANSFER" ? $"/api/v1/bookings/{b.Id}/qr" : null,
                transferContent = b.BookingNo, p.FirstReportedAt, p.LastReportedAt, p.ConfirmedAmount, p.ConfirmedAt, p.ConfirmedBy
            },
            evidence, decisions, confirmationDueAt = p.FirstReportedAt?.AddMinutes(ConfirmationAlerts.SlaMinutes),
            isOverdue = p.FirstReportedAt is DateTimeOffset reportedAt && (now ?? DateTimeOffset.UtcNow) >= reportedAt.AddMinutes(ConfirmationAlerts.SlaMinutes)
                && b.Status is "AWAITING_OWNER_CONFIRMATION" or "NEEDS_REVIEW"
        };
    }

    public static string Mask(string? email, string? phone)
    {
        if (!string.IsNullOrEmpty(phone)) return "Khách ****" + phone[^Math.Min(4, phone.Length)..];
        if (!string.IsNullOrEmpty(email)) return "Khách " + email[0] + "***@" + email.Split('@').Last();
        return "Khách đặt sân";
    }
}
