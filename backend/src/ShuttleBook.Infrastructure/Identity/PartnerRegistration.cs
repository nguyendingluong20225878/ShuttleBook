using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Infrastructure.Data;

namespace ShuttleBook.Infrastructure.Identity;

public sealed record RegisterPartnerCommand(string ContactType, string Contact, string Password, string CorrelationId);
public sealed record VerifyPartnerContactCommand(string ContactType, string Contact, string Code, string CorrelationId);
public sealed record ResendPartnerVerificationCommand(string ContactType, string Contact, string CorrelationId);

public interface IPartnerRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegisterPartnerCommand command, CancellationToken cancellationToken);
    Task<VerificationResult> VerifyAsync(VerifyPartnerContactCommand command, CancellationToken cancellationToken);
    Task<RegistrationResult> ResendAsync(ResendPartnerVerificationCommand command, CancellationToken cancellationToken);
}

public sealed class PartnerRegistrationService(
    ShuttleBookDbContext database,
    IPasswordHasher<User> passwordHasher,
    IContactVerificationDelivery delivery,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<PartnerRegistrationService> logger) : IPartnerRegistrationService
{
    private const int MaxVerificationAttempts = 5;
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromMinutes(10);

    public async Task<RegistrationResult> RegisterAsync(RegisterPartnerCommand command, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(command.ContactType, command.Contact, out var type, out var normalized)
            || !PasswordPolicy.IsValid(command.Password))
        {
            return RegistrationResult.ValidationFailed;
        }

        var now = clock.GetUtcNow();
        var code = GenerateCode();
        User? user;
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            user = await FindUserForUpdateAsync(type, normalized, cancellationToken);
            // The contact is globally unique across account types. Never reclassify a customer or Admin.
            if (user is not null && (user.AccountType != AccountType.VenueOperator || user.Status != UserStatus.PendingVerification))
            {
                await transaction.CommitAsync(cancellationToken);
                return RegistrationResult.Accepted;
            }

            if (user is null)
            {
                user = CreatePartner(type, normalized, command.Password, now);
                database.Users.Add(user);
                database.AuditEvents.Add(NewAudit("partner.registration_started", user.Id, command.CorrelationId, now));
            }
            else
            {
                // Re-registration can issue a fresh OTP, but cannot reset the pending account password.
                await InvalidateChallengesAsync(user.Id, now, cancellationToken);
            }

            database.ContactVerificationChallenges.Add(new ContactVerificationChallenge
            {
                User = user,
                ContactType = type,
                CodeHash = HashCode(user.Id, code),
                ExpiresAt = now.Add(VerificationLifetime),
                CreatedAt = now
            });
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // A concurrent request already owns the normalized contact; keep the public response generic.
            logger.LogInformation("Partner registration raced on a normalized contact; trace {TraceId}.", command.CorrelationId);
            return RegistrationResult.Accepted;
        }

        return await DeliverAsync(type, normalized, code, command.CorrelationId, cancellationToken);
    }

    public async Task<VerificationResult> VerifyAsync(VerifyPartnerContactCommand command, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(command.ContactType, command.Contact, out var type, out var normalized)
            || command.Code.Length != 6 || command.Code.Any(character => character is < '0' or > '9'))
        {
            return VerificationResult.ValidationFailed;
        }

        var now = clock.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await FindUserForUpdateAsync(type, normalized, cancellationToken);
        if (user is null || user.AccountType != AccountType.VenueOperator || user.Status != UserStatus.PendingVerification)
        {
            return VerificationResult.InvalidCode;
        }

        var challenge = await database.ContactVerificationChallenges
            .FromSqlInterpolated($"SELECT * FROM contact_verification_challenges WHERE user_id = {user.Id} AND consumed_at IS NULL AND invalidated_at IS NULL ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (challenge is null || challenge.ContactType != type || challenge.ExpiresAt <= now || challenge.AttemptCount >= MaxVerificationAttempts)
        {
            database.AuditEvents.Add(NewAudit("partner.verification_failed", user.Id, command.CorrelationId, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return VerificationResult.InvalidCode;
        }

        if (!CryptographicOperations.FixedTimeEquals(challenge.CodeHash, HashCode(user.Id, command.Code)))
        {
            challenge.AttemptCount++;
            database.AuditEvents.Add(NewAudit("partner.verification_failed", user.Id, command.CorrelationId, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return VerificationResult.InvalidCode;
        }

        challenge.ConsumedAt = now;
        user.Status = UserStatus.PendingOnboarding;
        user.UpdatedAt = now;
        if (type == ContactType.Email) user.EmailVerifiedAt = now;
        else user.PhoneVerifiedAt = now;
        database.AuditEvents.Add(NewAudit("partner.contact_verified", user.Id, command.CorrelationId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return VerificationResult.Verified;
    }

    public async Task<RegistrationResult> ResendAsync(ResendPartnerVerificationCommand command, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(command.ContactType, command.Contact, out var type, out var normalized))
        {
            return RegistrationResult.ValidationFailed;
        }

        var now = clock.GetUtcNow();
        var code = GenerateCode();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await FindUserForUpdateAsync(type, normalized, cancellationToken);
        if (user is null || user.AccountType != AccountType.VenueOperator || user.Status != UserStatus.PendingVerification)
        {
            await transaction.CommitAsync(cancellationToken);
            return RegistrationResult.Accepted;
        }

        await InvalidateChallengesAsync(user.Id, now, cancellationToken);
        database.ContactVerificationChallenges.Add(new ContactVerificationChallenge
        {
            UserId = user.Id,
            ContactType = type,
            CodeHash = HashCode(user.Id, code),
            ExpiresAt = now.Add(VerificationLifetime),
            CreatedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await DeliverAsync(type, normalized, code, command.CorrelationId, cancellationToken);
    }

    private async Task<User?> FindUserForUpdateAsync(ContactType type, string normalized, CancellationToken cancellationToken) =>
        type == ContactType.Email
            ? await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE normalized_email = {normalized} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : await database.Users.FromSqlInterpolated($"SELECT * FROM users WHERE normalized_phone = {normalized} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);

    private Task<int> InvalidateChallengesAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        database.ContactVerificationChallenges
            .Where(challenge => challenge.UserId == userId && challenge.ConsumedAt == null && challenge.InvalidatedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.InvalidatedAt, now), cancellationToken);

    private User CreatePartner(ContactType type, string normalized, string password, DateTimeOffset now)
    {
        var user = new User
        {
            AccountType = AccountType.VenueOperator,
            Status = UserStatus.PendingVerification,
            Email = type == ContactType.Email ? normalized : null,
            NormalizedEmail = type == ContactType.Email ? normalized : null,
            Phone = type == ContactType.Phone ? normalized : null,
            NormalizedPhone = type == ContactType.Phone ? normalized : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        return user;
    }

    private byte[] HashCode(Guid userId, string code)
    {
        var pepper = configuration["Identity:OtpPepper"];
        if (string.IsNullOrWhiteSpace(pepper)) throw new InvalidOperationException("Identity OTP pepper is not configured.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes($"{userId:N}:{code}"));
    }

    private async Task<RegistrationResult> DeliverAsync(ContactType type, string normalized, string code, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await delivery.DeliverAsync(type, normalized, code, cancellationToken);
            return RegistrationResult.Accepted;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogWarning("Partner verification delivery unavailable; trace {TraceId}.", correlationId);
            return RegistrationResult.DeliveryUnavailable;
        }
    }

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static AuditEvent NewAudit(string action, Guid entityId, string traceId, DateTimeOffset now) => new()
    {
        Action = action, EntityType = "user", EntityId = entityId, CorrelationId = traceId, CreatedAt = now
    };
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
