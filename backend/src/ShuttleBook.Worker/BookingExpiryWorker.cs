using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Worker;

public sealed class BookingExpiryWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<BookingExpiryWorker> logger) : BackgroundService
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
                    var db = scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>();
                    var now = clock.GetUtcNow();
                    var quoteExpired = await QuoteReservations.ProcessOne(db, now, ct);
                    var bookingExpired = await BookingExpiry.ProcessOne(db, now, ct);
                    if (!quoteExpired && !bookingExpired) break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Booking expiry retry after {ExceptionType}.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
}
