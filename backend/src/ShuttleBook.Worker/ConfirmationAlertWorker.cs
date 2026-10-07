using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Worker;

public sealed class ConfirmationAlertWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ConfirmationAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < 20; i++)
                {
                    using var scope = scopes.CreateScope();
                    if (!await ConfirmationAlerts.ProcessOne(scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>(), clock.GetUtcNow(), ct)) break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Payment confirmation alert retry after {ExceptionType}.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
