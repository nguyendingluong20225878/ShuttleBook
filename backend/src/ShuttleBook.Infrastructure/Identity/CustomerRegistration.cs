using System.Net;
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

public sealed record RegisterCustomerCommand(string ContactType, string Contact, string Password, string CorrelationId);
public sealed record VerifyContactCommand(string ContactType, string Contact, string Code, string CorrelationId);
public sealed record ResendVerificationCommand(string ContactType, string Contact, string CorrelationId);

public enum RegistrationResult { Accepted, ValidationFailed, UnsupportedField, RateLimited, DeliveryUnavailable }
public enum VerificationResult { Verified, InvalidCode, ValidationFailed, RateLimited }

public interface ICustomerRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegisterCustomerCommand command, CancellationToken cancellationToken);
    Task<VerificationResult> VerifyAsync(VerifyContactCommand command, CancellationToken cancellationToken);
    Task<RegistrationResult> ResendAsync(ResendVerificationCommand command, CancellationToken cancellationToken);
}

public interface IContactVerificationDelivery
{
    Task DeliverAsync(ContactType contactType, string normalizedContact, string code, CancellationToken cancellationToken);
}

public sealed class SmtpContactVerificationDelivery(IConfiguration configuration) : IContactVerificationDelivery
{
    public async Task DeliverAsync(ContactType contactType, string normalizedContact, string code, CancellationToken cancellationToken)
    {
        var host = configuration["Identity:Delivery:SmtpHost"];
        var port = int.TryParse(configuration["Identity:Delivery:SmtpPort"], out var configuredPort) ? configuredPort : 0;
        var sender = configuration["Identity:Delivery:From"];
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535 || string.IsNullOrWhiteSpace(sender))
        {
            throw new InvalidOperationException("Identity contact delivery is not configured.");
        }

        // Mailpit accepts this synthetic address for local phone verification. A production SMS adapter replaces it.
        var recipient = contactType == ContactType.Email
            ? normalizedContact
            : $"{normalizedContact.TrimStart('+')}@phone.shuttlebook.test";
        using var message = new MailMessage(sender, recipient)
        {
            Subject = "Mã xác minh ShuttleBook",
            Body = $"Mã xác minh của bạn là: {code}. Mã hết hạn sau 10 phút.",
            IsBodyHtml = false
        };
        using var smtp = new SmtpClient(host, port) { EnableSsl = false, DeliveryMethod = SmtpDeliveryMethod.Network };
        await smtp.SendMailAsync(message, cancellationToken);
    }
}

public sealed class CustomerRegistrationService(
    ShuttleBookDbContext database,
    IPasswordHasher<User> passwordHasher,
    IContactVerificationDelivery delivery,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<CustomerRegistrationService> logger) : ICustomerRegistrationService
{
    private const int MaxVerificationAttempts = 5;
    private static readonly TimeSpan VerificationLifetime = TimeSpan.FromMinutes(10);

    public async Task<RegistrationResult> RegisterAsync(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(command.ContactType, command.Contact, out var type, out var normalized)
            || !PasswordPolicy.IsValid(command.Password))
        {
            return RegistrationResult.ValidationFailed;
        }

        var now = clock.GetUtcNow();
        var user = await FindUserAsync(type, normalized, cancellationToken);
        if (user is not null && (user.AccountType != AccountType.Customer || user.Status != UserStatus.PendingVerification))
        {
            return RegistrationResult.Accepted;
        }

        var code = GenerateCode();
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            user = await FindUserAsync(type, normalized, cancellationToken);
            if (user is not null && (user.AccountType != AccountType.Customer || user.Status != UserStatus.PendingVerification))
            {
                await transaction.CommitAsync(cancellationToken);
                return RegistrationResult.Accepted;
            }

            if (user is null)
            {
                user = CreateCustomer(type, normalized, command.Password, now);
                database.Users.Add(user);
                database.AuditEvents.Add(NewAudit("user.registration_started", user.Id, command.CorrelationId, now));
            }

            await database.ContactVerificationChallenges
                .Where(challenge => challenge.UserId == user.Id && challenge.ConsumedAt == null && challenge.InvalidatedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.InvalidatedAt, now), cancellationToken);

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
            // A concurrent registration won the unique contact insert. Preserve the indistinguishable public result.
            logger.LogInformation("Customer registration raced on a normalized contact; trace {TraceId}.", command.CorrelationId);
            return RegistrationResult.Accepted;
        }

        try
        {
            await delivery.DeliverAsync(type, normalized, code, cancellationToken);
            return RegistrationResult.Accepted;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogWarning("Contact verification delivery unavailable; trace {TraceId}.", command.CorrelationId);
            return RegistrationResult.DeliveryUnavailable;
        }
    }

    public async Task<VerificationResult> VerifyAsync(VerifyContactCommand command, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(command.ContactType, command.Contact, out var type, out var normalized)
            || command.Code.Length != 6 || command.Code.Any(character => character is < '0' or > '9'))
        {
            return VerificationResult.ValidationFailed;
        }

        var now = clock.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await FindUserAsync(type, normalized, cancellationToken);
        if (user is null || user.AccountType != AccountType.Customer || user.Status != UserStatus.PendingVerification)
        {
            return VerificationResult.InvalidCode;
        }

        var challenge = await database.ContactVerificationChallenges
            .FromSqlInterpolated($"SELECT * FROM contact_verification_challenges WHERE user_id = {user.Id} AND consumed_at IS NULL AND invalidated_at IS NULL ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (challenge is null || challenge.ContactType != type || challenge.ExpiresAt <= now || challenge.AttemptCount >= MaxVerificationAttempts)
        {
            database.AuditEvents.Add(NewAudit("user.verification_failed", user.Id, command.CorrelationId, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return VerificationResult.InvalidCode;
        }

        if (!CryptographicOperations.FixedTimeEquals(challenge.CodeHash, HashCode(user.Id, command.Code)))
        {
            challenge.AttemptCount++;
            database.AuditEvents.Add(NewAudit("user.verification_failed", user.Id, command.CorrelationId, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return VerificationResult.InvalidCode;
        }

        challenge.ConsumedAt = now;
        user.Status = UserStatus.Active;
        user.UpdatedAt = now;
        if (type == ContactType.Email) user.EmailVerifiedAt = now;
        else user.PhoneVerifiedAt = now;
        database.AuditEvents.Add(NewAudit("user.contact_verified", user.Id, command.CorrelationId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return VerificationResult.Verified;
    }

    public async Task<RegistrationResult> ResendAsync(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(command.ContactType, command.Contact, out var type, out var normalized))
        {
            return RegistrationResult.ValidationFailed;
        }

        var now = clock.GetUtcNow();
        var user = await FindUserAsync(type, normalized, cancellationToken);
        // This public result intentionally does not reveal whether the contact has an account.
        if (user is null || user.AccountType != AccountType.Customer || user.Status != UserStatus.PendingVerification) return RegistrationResult.Accepted;

        var code = GenerateCode();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        user = await FindUserAsync(type, normalized, cancellationToken);
        if (user is null || user.AccountType != AccountType.Customer || user.Status != UserStatus.PendingVerification)
        {
            await transaction.CommitAsync(cancellationToken);
            return RegistrationResult.Accepted;
        }

        await database.ContactVerificationChallenges
            .Where(challenge => challenge.UserId == user.Id && challenge.ConsumedAt == null && challenge.InvalidatedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.InvalidatedAt, now), cancellationToken);
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

        try
        {
            await delivery.DeliverAsync(type, normalized, code, cancellationToken);
            return RegistrationResult.Accepted;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            logger.LogWarning("Contact verification resend delivery unavailable; trace {TraceId}.", command.CorrelationId);
            return RegistrationResult.DeliveryUnavailable;
        }
    }

    private async Task<User?> FindUserAsync(ContactType type, string normalized, CancellationToken cancellationToken) =>
        type == ContactType.Email
            ? await database.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalized, cancellationToken)
            : await database.Users.SingleOrDefaultAsync(user => user.NormalizedPhone == normalized, cancellationToken);

    private User CreateCustomer(ContactType type, string normalized, string password, DateTimeOffset now)
    {
        var user = new User
        {
            AccountType = AccountType.Customer,
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

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static AuditEvent NewAudit(string action, Guid entityId, string traceId, DateTimeOffset now) => new()
    {
        Action = action, EntityType = "user", EntityId = entityId, CorrelationId = traceId, CreatedAt = now
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public static class ContactNormalizer
{
    public static bool TryNormalize(string contactType, string contact, out ContactType type, out string normalized)
    {
        normalized = string.Empty;
        if (string.Equals(contactType, "email", StringComparison.OrdinalIgnoreCase))
        {
            type = ContactType.Email;
            normalized = contact.Trim().ToLowerInvariant();
            return normalized.Length is > 3 and <= 320 && normalized.Count(character => character == '@') == 1
                && !normalized.Any(char.IsWhiteSpace);
        }
        if (string.Equals(contactType, "phone", StringComparison.OrdinalIgnoreCase))
        {
            type = ContactType.Phone;
            normalized = contact.Trim();
            return System.Text.RegularExpressions.Regex.IsMatch(normalized, "^\\+[1-9][0-9]{7,14}$");
        }
        type = default;
        return false;
    }
}

public static class PasswordPolicy
{
    public static bool IsValid(string password)
    {
        if (password.Length is < 12 or > 128) return false;
        var groups = 0;
        if (password.Any(char.IsLower)) groups++;
        if (password.Any(char.IsUpper)) groups++;
        if (password.Any(char.IsDigit)) groups++;
        if (password.Any(character => !char.IsLetterOrDigit(character))) groups++;
        return groups >= 3;
    }
}
