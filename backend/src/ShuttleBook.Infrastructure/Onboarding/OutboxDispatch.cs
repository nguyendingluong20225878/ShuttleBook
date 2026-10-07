using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Infrastructure.Onboarding;

public sealed class OutboxDispatchException(Guid messageId, Exception inner) : Exception("Outbox dispatch failed.", inner)
{
    public Guid MessageId { get; } = messageId;
}

public static class OutboxDispatch
{
    public static readonly string[] OwnerEvents = ["PAYMENT_TRANSFER_REPORTED", "PAYMENT_EVIDENCE_SUPPLEMENTED", "PAYMENT_CONFIRMATION_OVERDUE"];
    private static readonly string[] CustomerEvents = ["BOOKING_CREATED", "BOOKING_EXPIRED", "PAYMENT_NEEDS_REVIEW", "PAYMENT_CONFIRMED", "PAYMENT_REJECTED"];
    private static readonly string[] ApprovalEvents = ["APPROVAL_SUBMITTED", "APPROVAL_APPROVED", "APPROVAL_CHANGES_REQUESTED"];

    public static async Task<bool> ProcessOne(ShuttleBookDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var message = await db.OutboxMessages.FromSqlInterpolated($"SELECT * FROM outbox_messages WHERE processed_at IS NULL AND next_attempt_at <= {now} ORDER BY created_at,id LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
        if (message is null) return false;
        try
        {
            Guid[] recipients;
            Booking? booking = null;
            if (OwnerEvents.Contains(message.EventType))
            {
                booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == message.EntityId, ct)
                    ?? throw new InvalidOperationException("Booking missing.");
                recipients = await (from v in db.Venues join b in db.Businesses on v.BusinessId equals b.Id
                                    join m in db.BusinessMemberships on b.Id equals m.BusinessId
                                    join u in db.Users on m.UserId equals u.Id
                                    where v.Id == booking.VenueId && b.Status == "ACTIVE" && m.Role == "OWNER" &&
                                        m.Status == "ACTIVE" && u.AccountType == AccountType.VenueOperator && u.Status == UserStatus.Active
                                    select u.Id).Distinct().ToArrayAsync(ct);
                if (message.EventType == "PAYMENT_CONFIRMATION_OVERDUE")
                {
                    var admins = await db.Users.Where(x => x.AccountType == AccountType.Admin && x.Status == UserStatus.Active).Select(x => x.Id).ToArrayAsync(ct);
                    recipients = recipients.Concat(admins).Distinct().ToArray();
                }
            }
            else if (CustomerEvents.Contains(message.EventType))
            {
                booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == message.EntityId, ct)
                    ?? throw new InvalidOperationException("Booking missing.");
                if (message.TargetUserId != booking.CustomerId) throw new InvalidOperationException("Invalid booking recipient.");
                recipients = [booking.CustomerId];
            }
            else if (ApprovalEvents.Contains(message.EventType))
            {
                recipients = message.TargetUserId is Guid userId ? [userId] :
                    await db.Users.Where(u => u.AccountType == AccountType.Admin && u.Status == UserStatus.Active).Select(u => u.Id).ToArrayAsync(ct);
            }
            else throw new InvalidOperationException("Unknown outbox event.");
            if (recipients.Length == 0) throw new InvalidOperationException("No eligible recipient.");
            string? customerLabel = null;
            if (booking is not null && OwnerEvents.Contains(message.EventType))
            {
                var customer = await db.Users.AsNoTracking().SingleAsync(x => x.Id == booking.CustomerId, ct);
                customerLabel = !string.IsNullOrEmpty(customer.Phone) ? "Khách ****" + customer.Phone[^Math.Min(4, customer.Phone.Length)..] :
                    !string.IsNullOrEmpty(customer.Email) ? "Khách " + customer.Email[0] + "***@" + customer.Email.Split('@').Last() : "Khách đặt sân";
            }
            var (title, body) = MessageText(message, booking, customerLabel);
            foreach (var recipient in recipients)
            {
                if (!await db.Notifications.AnyAsync(n => n.OutboxMessageId == message.Id && n.UserId == recipient, ct))
                    db.Notifications.Add(new Notification { OutboxMessageId = message.Id, UserId = recipient, Title = title, Body = body, CreatedAt = now });
            }
            message.ProcessedAt = now; message.LastFailureType = null;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { throw new OutboxDispatchException(message.Id, ex); }
    }

    public static async Task<int> RecordFailure(ShuttleBookDbContext db, Guid id, DateTimeOffset now, CancellationToken ct,
        string failureType = "OutboxDispatchException", int alertAttempts = 8)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var message = await db.OutboxMessages.FromSqlInterpolated($"SELECT * FROM outbox_messages WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (message is null || message.ProcessedAt is not null) return 0;
        message.Attempts++;
        message.NextAttemptAt = now.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(10, message.Attempts))));
        message.LastFailureType = failureType.Length <= 100 ? failureType : failureType[..100];
        if (message.Attempts >= Math.Max(1, alertAttempts)) message.AlertedAt ??= now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return message.Attempts;
    }

    private static (string Title, string Body) MessageText(OutboxMessage message, Booking? booking, string? customerLabel)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        var reason = payload.RootElement.TryGetProperty("reason", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
        if (reason.Length > 900) reason = reason[..900];
        var info = booking is null ? "" : $"{booking.BookingNo} · {booking.VenueName} · {booking.CourtName} · {booking.LocalDate:dd/MM/yyyy} {booking.LocalStart:HH:mm}–{booking.LocalEnd:HH:mm} · {booking.Amount:N0}đ";
        if (info.Length > 700) info = info[..700];
        if (booking is not null && customerLabel is not null)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(booking.Timezone);
            info += $" · {customerLabel} · {TimeZoneInfo.ConvertTime(message.CreatedAt, zone):dd/MM HH:mm} ({booking.Timezone})";
        }
        if (info.Length > 800) info = info[..800];
        return message.EventType switch
        {
            "APPROVAL_SUBMITTED" => ("Có hồ sơ chờ duyệt", "Mở cổng Admin để xem và quyết định hồ sơ."),
            "APPROVAL_APPROVED" => ("Hồ sơ đã được duyệt", "Cơ sở đã được công bố trên hệ thống."),
            "APPROVAL_CHANGES_REQUESTED" => ("Hồ sơ cần bổ sung", reason),
            "BOOKING_CREATED" => ("Đơn đặt sân đang giữ chỗ", "Mở Đơn của tôi để xem QR và hạn chuyển khoản."),
            "BOOKING_EXPIRED" => ("Đơn đặt sân đã hết hạn", "Khung giờ đã được giải phóng. Bạn có thể chọn lịch và tạo đơn mới."),
            "PAYMENT_TRANSFER_REPORTED" => ("Khách đã báo chuyển khoản", info + ". Mở đơn để đối chiếu và xác nhận."),
            "PAYMENT_EVIDENCE_SUPPLEMENTED" => ("Khách đã bổ sung bằng chứng", info + ". Mở đơn để tiếp tục đối chiếu."),
            "PAYMENT_NEEDS_REVIEW" => ("Đơn cần bổ sung hoặc đối chiếu", reason),
            "PAYMENT_CONFIRMED" => ("Đơn đặt sân đã xác nhận", info),
            "PAYMENT_REJECTED" => ("Sân không xác nhận được giao dịch", reason),
            "PAYMENT_CONFIRMATION_OVERDUE" => ("Đơn quá hạn đối chiếu", info + ". Khung giờ vẫn được giữ; cần xử lý đối chiếu."),
            _ => throw new InvalidOperationException("Unknown outbox event.")
        };
    }
}
