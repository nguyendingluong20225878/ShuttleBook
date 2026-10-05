using Npgsql;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Database.Tests;

internal static class LocalPostgresTestServer
{
    public static NpgsqlConnectionStringBuilder Require(string connection)
    {
        var settings = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.RequireConnectionString(connection));
        if (settings.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            !string.Equals(settings.Database, "postgres", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Disposable database tests require a loopback PostgreSQL host and the postgres control database.");
        return settings;
    }
}

public sealed class LocalPostgresTestServerTests
{
    [Theory]
    [InlineData("Host=remote.example.test;Database=postgres;Username=test")]
    [InlineData("Host=127.0.0.1;Database=customer_data;Username=test")]
    [InlineData("Server=remote.example.test;Database=postgres;Username=test")]
    public void Rejects_non_disposable_database_targets(string connection)
    {
        Assert.Throws<InvalidOperationException>(() => LocalPostgresTestServer.Require(connection));
    }
}
