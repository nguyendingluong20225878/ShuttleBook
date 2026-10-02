namespace ShuttleBook.Infrastructure.Health;

public interface IReadinessProbe
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
