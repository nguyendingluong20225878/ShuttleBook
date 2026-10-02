using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ShuttleBook.Infrastructure.Data;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddShuttleBookDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = RequireConnectionString(configuration.GetConnectionString("ShuttleBook"));
        services.AddDbContext<ShuttleBookDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }

    public static string RequireConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__ShuttleBook. Configure a local PostgreSQL connection before starting this process.");
        }

        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Database))
            {
                throw new ArgumentException();
            }

            // PostgreSQL detail can contain user data; never enable it through application configuration.
            settings.IncludeErrorDetail = false;
            return settings.ConnectionString;
        }
        catch (ArgumentException)
        {
            // Do not attach the original exception: it can echo configuration values.
            throw new InvalidOperationException("Invalid ConnectionStrings__ShuttleBook. It must specify Host and Database using PostgreSQL connection-string syntax.");
        }
    }
}
