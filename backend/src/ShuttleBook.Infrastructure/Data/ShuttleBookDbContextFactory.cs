using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShuttleBook.Infrastructure.Data;

public sealed class ShuttleBookDbContextFactory : IDesignTimeDbContextFactory<ShuttleBookDbContext>
{
    public ShuttleBookDbContext CreateDbContext(string[] args)
    {
        var connectionString = DatabaseConfiguration.RequireConnectionString(
            Environment.GetEnvironmentVariable("ConnectionStrings__ShuttleBook"));
        return new ShuttleBookDbContext(new DbContextOptionsBuilder<ShuttleBookDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
