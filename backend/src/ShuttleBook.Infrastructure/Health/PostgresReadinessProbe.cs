using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Infrastructure.Health;

public sealed class PostgresReadinessProbe(ShuttleBookDbContext database, ILogger<PostgresReadinessProbe> logger) : IReadinessProbe
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var known = database.Database.GetMigrations().ToArray();
            var applied = (await database.Database.GetAppliedMigrationsAsync(timeout.Token)).ToHashSet(StringComparer.Ordinal);
            if (known.Length == 0 || known.Any(migration => !applied.Contains(migration)))
            {
                return false;
            }

            var extensionCount = await database.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname IN ('postgis', 'btree_gist')")
                .SingleAsync(timeout.Token);
            return extensionCount == 2;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (DbException)
        {
            // Log no exception message or connection metadata; a probe is intentionally public.
            logger.LogWarning("Database readiness check failed.");
            return false;
        }
    }
}
