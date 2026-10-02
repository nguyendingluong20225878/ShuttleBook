namespace ShuttleBook.Worker;

public sealed class FoundationWorker(ILogger<FoundationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Foundation worker started. No booking or outbox processing is implemented in F00.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Foundation worker stopped.");
        }
    }
}
