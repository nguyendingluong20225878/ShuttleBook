using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Worker;

public sealed class ApprovalOutboxWorker(IServiceScopeFactory scopes, TimeProvider clock, IConfiguration config,
    ILogger<ApprovalOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { for (var count = 0; count < 20 && await ProcessOne(stoppingToken); count++) { } }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Outbox retry after {ExceptionType}.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task<bool> ProcessOne(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            return await OutboxDispatch.ProcessOne(scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>(), clock.GetUtcNow(), ct);
        }
        catch (OutboxDispatchException ex)
        {
            using var scope = scopes.CreateScope();
            var threshold = Math.Max(1, config.GetValue("Outbox:AlertAttempts", 8));
            var attempts = await OutboxDispatch.RecordFailure(scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>(), ex.MessageId,
                clock.GetUtcNow(), ct, ex.InnerException?.GetType().Name ?? ex.GetType().Name, threshold);
            if (attempts >= threshold) logger.LogError("Outbox {MessageId} needs attention after {Attempts} attempts.", ex.MessageId, attempts);
            return true;
        }
    }
}
