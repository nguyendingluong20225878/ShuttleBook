using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

if (args.Length != 1 || args[0] is not ("bootstrap" or "rotate" or "suspend" or "activate" or "revoke-sessions"))
{
    Console.Error.WriteLine("Usage: admin-cli bootstrap|rotate|suspend|activate|revoke-sessions");
    return 2;
}

try
{
    var connection = DatabaseConfiguration.RequireConnectionString(
        Environment.GetEnvironmentVariable("ConnectionStrings__ShuttleBook"));
    var options = new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(connection).Options;
    await using var database = new ShuttleBookDbContext(options);
    // Schema changes are a separate explicit operation; the CLI requires a migrated database.
    if (await database.Database.GetPendingMigrationsAsync() is { } pending && pending.Any())
        throw new InvalidOperationException("Database migrations are pending.");
    var operations = new AdminOperations(database, new PasswordHasher<User>(), TimeProvider.System);
    var correlationId = Guid.CreateVersion7().ToString();
    AdminOperationResult result;
    switch (args[0])
    {
        case "bootstrap":
            Console.Write("Contact type (email/phone): ");
            var contactType = Console.ReadLine();
            Console.Write("Contact: ");
            var contact = ReadSecret(320);
            Console.Write("Password: ");
            var bootstrapPassword = ReadSecret();
            if (contactType is null || contact is null || bootstrapPassword is null)
                throw new InvalidOperationException("Required input is missing.");
            result = await operations.BootstrapAsync(contactType, contact, bootstrapPassword, correlationId, CancellationToken.None);
            break;
        case "rotate":
            Console.Write("New password: ");
            var newPassword = ReadSecret();
            if (newPassword is null) throw new InvalidOperationException("Required input is missing.");
            result = await operations.RotateAsync(newPassword, correlationId, CancellationToken.None);
            break;
        case "suspend":
            result = await operations.SuspendAsync(correlationId, CancellationToken.None);
            break;
        case "activate":
            result = await operations.ActivateAsync(correlationId, CancellationToken.None);
            break;
        default:
            result = await operations.RevokeSessionsAsync(correlationId, CancellationToken.None);
            break;
    }
    if (result == AdminOperationResult.Applied)
    {
        Console.WriteLine("Admin operation completed.");
        return 0;
    }
    if (result == AdminOperationResult.AlreadyExists)
    {
        Console.WriteLine("Admin already exists; no changes made.");
        return 0;
    }
    Console.Error.WriteLine(result switch
    {
        AdminOperationResult.InvalidInput => "Invalid contact or password policy.",
        AdminOperationResult.ContactInUse => "Contact is unavailable.",
        AdminOperationResult.InvalidState => "Admin account is not in the required state.",
        _ => "Admin account is unavailable."
    });
    return 1;
}
catch (Exception)
{
    // Driver exceptions can contain connection information. Never print them here.
    Console.Error.WriteLine("Admin operation failed. Check database configuration, migration and connectivity.");
    return 1;
}

static string? ReadSecret(int maxLength = 128)
{
    if (Console.IsInputRedirected) return Console.ReadLine();
    var buffer = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return buffer.ToString();
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Length > 0) buffer.Length--;
            continue;
        }
        if (!char.IsControl(key.KeyChar) && buffer.Length <= maxLength) buffer.Append(key.KeyChar);
    }
}
