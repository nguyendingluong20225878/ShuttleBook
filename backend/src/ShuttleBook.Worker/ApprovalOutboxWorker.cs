using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Worker;

public sealed class ApprovalOutboxWorker(IServiceScopeFactory scopes, ILogger<ApprovalOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var count = 0; count < 20 && await ProcessOne(stoppingToken); count++) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Approval notification retry after {ExceptionType}.", exception.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task<bool> ProcessOne(CancellationToken ct)
    {
        Guid failedId = Guid.Empty;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var message = await db.OutboxMessages.FromSqlRaw("""
                SELECT * FROM outbox_messages WHERE processed_at IS NULL AND next_attempt_at <= now()
                ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
                """).SingleOrDefaultAsync(ct);
            if (message is null) return false;
            failedId = message.Id;
            var recipients = message.TargetUserId is Guid userId ? [userId] :
                await db.Users.Where(u => u.AccountType == AccountType.Admin && u.Status == UserStatus.Active)
                    .Select(u => u.Id).ToArrayAsync(ct);
            if (recipients.Length == 0) throw new InvalidOperationException("No approval recipient.");
            var (title, body) = MessageText(message);
            foreach (var recipient in recipients)
            {
                if (!await db.Notifications.AnyAsync(n => n.OutboxMessageId == message.Id && n.UserId == recipient, ct))
                    db.Notifications.Add(new Notification { OutboxMessageId = message.Id, UserId = recipient,
                        Title = title, Body = body, CreatedAt = DateTimeOffset.UtcNow });
            }
            message.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            if (failedId != Guid.Empty) await RecordFailure(failedId, ct);
            throw;
        }
    }

    private async Task RecordFailure(Guid id, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var message = await db.OutboxMessages.FromSqlInterpolated(
            $"SELECT * FROM outbox_messages WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (message?.ProcessedAt is null && message is not null)
        {
            message.Attempts++;
            message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(10, message.Attempts))));
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private static (string Title, string Body) MessageText(OutboxMessage message)
    {
        var reason = "";
        using (var payload = JsonDocument.Parse(message.Payload))
        {
            if (payload.RootElement.TryGetProperty("reason", out var value) && value.ValueKind == JsonValueKind.String)
                reason = value.GetString() ?? "";
        }
        return message.EventType switch
        {
            "APPROVAL_SUBMITTED" => ("Có hồ sơ chờ duyệt", "Mở cổng Admin để xem và quyết định hồ sơ."),
            "APPROVAL_APPROVED" => ("Hồ sơ đã được duyệt", "Cơ sở đã được công bố trên hệ thống."),
            "APPROVAL_CHANGES_REQUESTED" => ("Hồ sơ cần bổ sung", reason),
            _ => throw new InvalidOperationException("Unknown approval event.")
        };
    }
}
