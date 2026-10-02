using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleBook.Infrastructure.Data;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    // Migration errors can include SQL or connection metadata. Report only a safe summary here.
    builder.Logging.ClearProviders();
    builder.Services.AddShuttleBookDatabase(builder.Configuration);
    using var host = builder.Build();
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    await using var scope = host.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ShuttleBookDbContext>();
    await database.Database.MigrateAsync(cancellation.Token);
    Console.WriteLine("Database migrations applied successfully (including any already applied).");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Database migration cancelled.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Database migration failed ({exception.GetType().Name}). Check ConnectionStrings__ShuttleBook, PostgreSQL connectivity and extension permissions. No configuration values are logged.");
    return 1;
}
