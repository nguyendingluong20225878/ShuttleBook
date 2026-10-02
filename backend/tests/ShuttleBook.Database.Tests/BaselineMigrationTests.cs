using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Health;

namespace ShuttleBook.Database.Tests;

public sealed class BaselineMigrationTests
{
    [Fact]
    public async Task Fresh_database_requires_baseline_and_extensions_and_migration_is_repeatable()
    {
        var supplied = Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(supplied),
            "SHUTTLEBOOK_TEST_CONNECTION_STRING is required. Run this project against a local/CI PostgreSQL server with PostGIS available and CREATE DATABASE/extension privileges. Missing infrastructure is NOT a pass.");

        var settings = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.RequireConnectionString(supplied))
        {
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 30
        };
        var databaseName = $"shuttlebook_f00_test_{Guid.NewGuid():N}";
        // The identifier is generated here; only this temporary database is ever dropped.
        var quotedName = new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);
        await using var control = new NpgsqlConnection(settings.ConnectionString);
        await control.OpenAsync();
        await ExecuteAsync(control, $"CREATE DATABASE {quotedName}");
        try
        {
            settings.Database = databaseName;
            await using var database = new ShuttleBookDbContext(new DbContextOptionsBuilder<ShuttleBookDbContext>()
                .UseNpgsql(settings.ConnectionString).Options);
            async Task<bool> IsReadyAsync()
            {
                await using var probeDatabase = new ShuttleBookDbContext(new DbContextOptionsBuilder<ShuttleBookDbContext>()
                    .UseNpgsql(settings.ConnectionString).Options);
                var probe = new PostgresReadinessProbe(probeDatabase, NullLogger<PostgresReadinessProbe>.Instance);
                return await probe.IsReadyAsync(CancellationToken.None);
            }

            Assert.False(await IsReadyAsync());

            await database.Database.MigrateAsync();
            var firstRun = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Contains(firstRun, migration => migration.Contains("BaselinePostgresExtensions", StringComparison.Ordinal));
            Assert.Contains(firstRun, migration => migration.Contains("CustomerRegistration", StringComparison.Ordinal));
            Assert.Contains(firstRun, migration => migration.Contains("AuthSessions", StringComparison.Ordinal));
            Assert.Equal(database.Database.GetMigrations(), firstRun);
            var extensionCount = await database.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname IN ('postgis', 'btree_gist')")
                .SingleAsync();
            Assert.Equal(2, extensionCount);
            Assert.True(await IsReadyAsync());

            await database.Database.MigrateAsync();
            Assert.Equal(firstRun, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await database.Database.GetPendingMigrationsAsync());
            Assert.False(database.Database.HasPendingModelChanges());
            Assert.True(await IsReadyAsync());

            await database.Database.ExecuteSqlRawAsync("DROP EXTENSION btree_gist");
            Assert.False(await IsReadyAsync());
            await database.Database.ExecuteSqlRawAsync("CREATE EXTENSION btree_gist");
            Assert.True(await IsReadyAsync());

            await database.Database.ExecuteSqlRawAsync("DELETE FROM \"__EFMigrationsHistory\"");
            Assert.False(await IsReadyAsync());
        }
        finally
        {
            await ExecuteAsync(control, $"DROP DATABASE {quotedName} WITH (FORCE)");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
