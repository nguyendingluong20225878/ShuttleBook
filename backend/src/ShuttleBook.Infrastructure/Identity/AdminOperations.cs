using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Infrastructure.Identity;

public enum AdminOperationResult { Applied, AlreadyExists, InvalidInput, ContactInUse, MissingAdmin, InvalidState }

// This service is only constructed by the internal CLI and database integration tests.
public sealed class AdminOperations(ShuttleBookDbContext database, IPasswordHasher<User> passwordHasher, TimeProvider clock)
{
    public async Task<AdminOperationResult> BootstrapAsync(string contactType, string contact, string password,
        string correlationId, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(contactType, contact, out var type, out var normalized) ||
            !PasswordPolicy.IsValid(password)) return AdminOperationResult.InvalidInput;

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await AcquireLockAsync(database, cancellationToken);
        if (await database.Users.AnyAsync(user => user.AccountType == AccountType.Admin, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return AdminOperationResult.AlreadyExists;
        }
        var contactInUse = type == ContactType.Email
            ? await database.Users.AnyAsync(user => user.NormalizedEmail == normalized, cancellationToken)
            : await database.Users.AnyAsync(user => user.NormalizedPhone == normalized, cancellationToken);
        if (contactInUse) return AdminOperationResult.ContactInUse;

        var now = clock.GetUtcNow();
        var admin = new User
        {
            AccountType = AccountType.Admin,
            Status = UserStatus.Active,
            Email = type == ContactType.Email ? normalized : null,
            NormalizedEmail = type == ContactType.Email ? normalized : null,
            Phone = type == ContactType.Phone ? normalized : null,
            NormalizedPhone = type == ContactType.Phone ? normalized : null,
            EmailVerifiedAt = type == ContactType.Email ? now : null,
            PhoneVerifiedAt = type == ContactType.Phone ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        database.Users.Add(admin);
        database.AuditEvents.Add(Audit("admin.bootstrap_succeeded", admin.Id, correlationId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminOperationResult.Applied;
    }

    public async Task<AdminOperationResult> RotateAsync(string password, string correlationId, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(password)) return AdminOperationResult.InvalidInput;
        return await ChangeAsync("admin.password_rotated", correlationId, (admin, now) =>
        {
            admin.PasswordHash = passwordHasher.HashPassword(admin, password);
            admin.UpdatedAt = now;
        }, revokeSessions: true, cancellationToken);
    }

    public Task<AdminOperationResult> SuspendAsync(string correlationId, CancellationToken cancellationToken) =>
        ChangeAsync("admin.suspended", correlationId, (admin, now) =>
        {
            admin.Status = UserStatus.Suspended;
            admin.UpdatedAt = now;
        }, revokeSessions: true, cancellationToken);

    public Task<AdminOperationResult> ActivateAsync(string correlationId, CancellationToken cancellationToken) =>
        ChangeAsync("admin.activated", correlationId, (admin, now) =>
        {
            admin.Status = UserStatus.Active;
            admin.UpdatedAt = now;
        }, revokeSessions: false, cancellationToken);

    public Task<AdminOperationResult> RevokeSessionsAsync(string correlationId, CancellationToken cancellationToken) =>
        ChangeAsync("admin.sessions_revoked", correlationId, (_, _) => { }, revokeSessions: true, cancellationToken);

    private async Task<AdminOperationResult> ChangeAsync(string action, string correlationId,
        Action<User, DateTimeOffset> update, bool revokeSessions, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await AcquireLockAsync(database, cancellationToken);
        var admin = await database.Users.SingleOrDefaultAsync(user => user.AccountType == AccountType.Admin, cancellationToken);
        if (admin is null) return AdminOperationResult.MissingAdmin;
        if (action == "admin.suspended" && admin.Status != UserStatus.Active ||
            action == "admin.activated" && admin.Status != UserStatus.Suspended)
            return AdminOperationResult.InvalidState;
        var now = clock.GetUtcNow();
        update(admin, now);
        if (revokeSessions)
            await database.RefreshSessions.Where(session => session.UserId == admin.Id && session.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);
        database.AuditEvents.Add(Audit(action, admin.Id, correlationId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AdminOperationResult.Applied;
    }

    public static async Task AcquireLockAsync(ShuttleBookDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.SqlQueryRaw<int>(
            "SELECT 1 AS \"Value\" FROM pg_advisory_xact_lock(731014)").SingleAsync(cancellationToken);
    }

    private static AuditEvent Audit(string action, Guid adminId, string correlationId, DateTimeOffset now) => new()
    {
        Action = action, EntityType = "user", EntityId = adminId,
        CorrelationId = correlationId, CreatedAt = now
    };
}
