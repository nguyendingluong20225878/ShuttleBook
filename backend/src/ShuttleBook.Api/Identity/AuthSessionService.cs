using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Identity;

public sealed record AuthUser(Guid Id, string AccountType, string Status);
public sealed record AuthTokens(string TokenType, string AccessToken, int ExpiresInSeconds,
    string RefreshToken, DateTimeOffset RefreshExpiresAt, AuthUser User);

public interface IAuthSessionService
{
    Task<AuthTokens?> LoginAsync(string contactType, string contact, string password, string traceId, CancellationToken cancellationToken);
    Task<AuthTokens?> RefreshAsync(string refreshToken, string traceId, CancellationToken cancellationToken);
    Task<bool> LogoutAsync(Guid userId, Guid familyId, string refreshToken, string traceId, CancellationToken cancellationToken);
}

public sealed class AuthSessionService(
    ShuttleBookDbContext database,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    TimeProvider clock) : IAuthSessionService
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<AuthTokens?> LoginAsync(string contactType, string contact, string password, string traceId, CancellationToken cancellationToken)
    {
        if (!ContactNormalizer.TryNormalize(contactType, contact, out var type, out var normalized) || password.Length is < 1 or > 128)
            return null;
        var user = type == ContactType.Email
            ? await database.Users.SingleOrDefaultAsync(item => item.NormalizedEmail == normalized, cancellationToken)
            : await database.Users.SingleOrDefaultAsync(item => item.NormalizedPhone == normalized, cancellationToken);
        if (user is null || !CanLogin(user) ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            database.AuditEvents.Add(new AuditEvent
            {
                Action = "auth.login_failed", EntityType = "user", EntityId = user?.Id ?? Guid.Empty,
                CorrelationId = traceId, CreatedAt = clock.GetUtcNow()
            });
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
        var now = clock.GetUtcNow();
        var familyId = Guid.CreateVersion7();
        var refresh = NewRefreshToken();
        database.RefreshSessions.Add(new RefreshSession
        {
            FamilyId = familyId, UserId = user.Id, TokenHash = HashToken(refresh),
            CreatedAt = now, ExpiresAt = now.Add(RefreshLifetime)
        });
        database.AuditEvents.Add(Audit("auth.login_succeeded", user.Id, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        return Tokens(user, familyId, refresh, now);
    }

    public async Task<AuthTokens?> RefreshAsync(string refreshToken, string traceId, CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256) return null;
        var hash = HashToken(refreshToken);
        var now = clock.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var session = await database.RefreshSessions
            .FromSqlInterpolated($"SELECT * FROM refresh_sessions WHERE token_hash = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null) return null;
        if (session.ConsumedAt is not null)
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            database.AuditEvents.Add(Audit("auth.refresh_reuse_detected", session.UserId, traceId, now));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (session.RevokedAt is not null || session.ExpiresAt <= now) return null;
        var user = await database.Users.SingleAsync(item => item.Id == session.UserId, cancellationToken);
        if (!CanLogin(user))
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var replacementToken = NewRefreshToken();
        var replacement = new RefreshSession
        {
            FamilyId = session.FamilyId, UserId = user.Id, TokenHash = HashToken(replacementToken),
            CreatedAt = now, ExpiresAt = now.Add(RefreshLifetime)
        };
        session.ConsumedAt = now;
        session.ReplacedById = replacement.Id;
        database.RefreshSessions.Add(replacement);
        database.AuditEvents.Add(Audit("auth.refresh_rotated", user.Id, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Tokens(user, session.FamilyId, replacementToken, now);
    }

    public async Task<bool> LogoutAsync(Guid userId, Guid familyId, string refreshToken, string traceId, CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256) return false;
        var tokenHash = HashToken(refreshToken);
        var exists = await database.RefreshSessions.AnyAsync(item => item.UserId == userId &&
            item.FamilyId == familyId && item.TokenHash == tokenHash && item.ConsumedAt == null &&
            item.RevokedAt == null, cancellationToken);
        if (!exists) return false;
        var now = clock.GetUtcNow();
        await RevokeFamilyAsync(familyId, now, cancellationToken);
        database.AuditEvents.Add(Audit("auth.logout", userId, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        database.RefreshSessions.Where(item => item.FamilyId == familyId && item.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.RevokedAt, now), cancellationToken);

    private AuthTokens Tokens(User user, Guid familyId, string refresh, DateTimeOffset now)
    {
        var secret = configuration["Identity:JwtSigningKey"] ?? throw new InvalidOperationException("Identity signing key is not configured.");
        var issuer = configuration["Identity:JwtIssuer"] ?? "ShuttleBook";
        var audience = configuration["Identity:JwtAudience"] ?? "ShuttleBook.Web";
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(issuer, audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("accountType", AccountTypeName(user.AccountType)),
                new Claim("sid", familyId.ToString()),
                new Claim("status", StatusName(user.Status)),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
            ], now.UtcDateTime, now.Add(AccessLifetime).UtcDateTime, credentials);
        return new AuthTokens("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt),
            (int)AccessLifetime.TotalSeconds, refresh, now.Add(RefreshLifetime),
            new AuthUser(user.Id, AccountTypeName(user.AccountType), StatusName(user.Status)));
    }

    public static bool CanLogin(User user) =>
        user.AccountType == AccountType.Customer && user.Status == UserStatus.Active ||
        user.AccountType == AccountType.VenueOperator && user.Status is UserStatus.PendingOnboarding or UserStatus.Active;

    public static string AccountTypeName(AccountType type) => type switch
    {
        AccountType.Customer => "CUSTOMER",
        AccountType.VenueOperator => "VENUE_OPERATOR",
        AccountType.Admin => "ADMIN",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public static string StatusName(UserStatus status) => status switch
    {
        UserStatus.Active => "ACTIVE",
        UserStatus.PendingOnboarding => "PENDING_ONBOARDING",
        UserStatus.PendingVerification => "PENDING_VERIFICATION",
        UserStatus.Suspended => "SUSPENDED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    private static AuditEvent Audit(string action, Guid userId, string traceId, DateTimeOffset now) => new()
    {
        ActorUserId = userId, Action = action, EntityType = "user", EntityId = userId,
        CorrelationId = traceId, CreatedAt = now
    };
}
