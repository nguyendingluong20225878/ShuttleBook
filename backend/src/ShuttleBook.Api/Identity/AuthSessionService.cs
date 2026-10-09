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
public sealed record BrowserAuthTokens(AuthTokens Tokens, DateTimeOffset IdleExpiresAt);

public interface IBrowserAuthSessionService
{
    Task<BrowserAuthTokens?> BrowserLoginAsync(AccountType role, string contactType, string contact, string password, string traceId, CancellationToken cancellationToken);
    Task<BrowserAuthTokens?> RestoreBrowserAsync(AccountType role, string refreshToken, bool activity, string traceId, CancellationToken cancellationToken);
    Task LogoutBrowserAsync(AccountType role, string refreshToken, string traceId, CancellationToken cancellationToken);
}

public interface IAuthSessionService
{
    Task<AuthTokens?> LoginAsync(string contactType, string contact, string password, string traceId, CancellationToken cancellationToken);
    Task<AuthTokens?> AdminLoginAsync(string contactType, string contact, string password, string traceId, CancellationToken cancellationToken);
    Task<AuthTokens?> RestoreAdminAsync(string refreshToken, string traceId, CancellationToken cancellationToken);
    Task<AuthTokens?> RefreshAsync(string refreshToken, string traceId, CancellationToken cancellationToken);
    Task<bool> LogoutAsync(Guid userId, Guid familyId, string refreshToken, string traceId, CancellationToken cancellationToken);
}

public sealed class AuthSessionService(
    ShuttleBookDbContext database,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    TimeProvider clock) : IAuthSessionService, IBrowserAuthSessionService
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    private static readonly string DummyAdminHash = new PasswordHasher<User>().HashPassword(
        new User(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public Task<AuthTokens?> LoginAsync(string contactType, string contact, string password, string traceId, CancellationToken cancellationToken) =>
        LoginForPortalAsync(contactType, contact, password, traceId, adminPortal: false, cancellationToken);

    public async Task<AuthTokens?> AdminLoginAsync(string contactType, string contact, string password, string traceId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdminOperations.AcquireLockAsync(database, cancellationToken);
        var tokens = await LoginForPortalAsync(contactType, contact, password, traceId, adminPortal: true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tokens;
    }

    public async Task<AuthTokens?> RestoreAdminAsync(string refreshToken, string traceId,
        CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256) return null;
        var now = clock.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AdminOperations.AcquireLockAsync(database, cancellationToken);
        var hash = HashToken(refreshToken);
        var session = await database.RefreshSessions.SingleOrDefaultAsync(item => item.TokenHash == hash,
            cancellationToken);
        if (session is null || session.ConsumedAt is not null || session.RevokedAt is not null ||
            session.ExpiresAt <= now) return null;
        var user = await database.Users.SingleAsync(item => item.Id == session.UserId, cancellationToken);
        if (!CanAdminLogin(user)) return null;
        if (session.LastActivityAt is null || session.LastActivityAt <= now.AddMinutes(-30))
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        session.LastActivityAt = now;
        database.AuditEvents.Add(Audit("admin.session_restored", user.Id, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Tokens(user, session.FamilyId, refreshToken, now, session.ExpiresAt);
    }

    private async Task<AuthTokens?> LoginForPortalAsync(string contactType, string contact, string password,
        string traceId, bool adminPortal, CancellationToken cancellationToken, AccountType? browserRole = null)
    {
        if (!ContactNormalizer.TryNormalize(contactType, contact, out var type, out var normalized) || password.Length is < 1 or > 128)
            return null;
        var user = type == ContactType.Email
            ? await database.Users.SingleOrDefaultAsync(item => item.NormalizedEmail == normalized, cancellationToken)
            : await database.Users.SingleOrDefaultAsync(item => item.NormalizedPhone == normalized, cancellationToken);
        var eligible = user is not null && (adminPortal ? CanAdminLogin(user) : CanLogin(user)) &&
            (browserRole is null || user.AccountType == browserRole);
        var hash = adminPortal && !eligible ? DummyAdminHash : user?.PasswordHash;
        var passwordMatches = hash is not null && passwordHasher.VerifyHashedPassword(user ?? new User(), hash, password)
            != PasswordVerificationResult.Failed;
        if (user is null || !eligible || !passwordMatches)
        {
            database.AuditEvents.Add(new AuditEvent
            {
                Action = adminPortal ? "admin.login_failed" : "auth.login_failed", EntityType = "user", EntityId = user?.Id ?? Guid.Empty,
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
            CreatedAt = now, ExpiresAt = now.Add(RefreshLifetime),
            LastActivityAt = adminPortal || browserRole is not null ? now : null
        });
        database.AuditEvents.Add(Audit(adminPortal ? "admin.login_succeeded" : "auth.login_succeeded", user.Id, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        return Tokens(user, familyId, refresh, now);
    }

    public async Task<AuthTokens?> RefreshAsync(string refreshToken, string traceId, CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256) return null;
        var hash = HashToken(refreshToken);
        var lookup = await database.RefreshSessions.AsNoTracking().Where(item => item.TokenHash == hash)
            .Select(item => new { item.FamilyId, item.User.AccountType }).SingleOrDefaultAsync(cancellationToken);
        if (lookup is null) return null;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (lookup.AccountType == AccountType.Admin)
            await AdminOperations.AcquireLockAsync(database, cancellationToken);
        await AcquireFamilyLockAsync(database, lookup.FamilyId, cancellationToken);
        var now = clock.GetUtcNow();
        var session = await database.RefreshSessions
            .FromSqlInterpolated($"SELECT * FROM refresh_sessions WHERE token_hash = {hash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null) return null;
        await database.Entry(session).ReloadAsync(cancellationToken);
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
        if ((user.AccountType == AccountType.Admin || session.LastActivityAt is not null) &&
            (session.LastActivityAt is null || session.LastActivityAt <= now.AddMinutes(-30)))
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!CanUseSession(user))
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var replacementToken = NewRefreshToken();
        var replacement = new RefreshSession
        {
            FamilyId = session.FamilyId, UserId = user.Id, TokenHash = HashToken(replacementToken),
            CreatedAt = now, ExpiresAt = session.ExpiresAt, LastActivityAt = session.LastActivityAt
        };
        session.ConsumedAt = now;
        session.ReplacedById = replacement.Id;
        database.RefreshSessions.Add(replacement);
        database.AuditEvents.Add(Audit("auth.refresh_rotated", user.Id, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Tokens(user, session.FamilyId, replacementToken, now, session.ExpiresAt);
    }

    public async Task<bool> LogoutAsync(Guid userId, Guid familyId, string refreshToken, string traceId, CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256) return false;
        var tokenHash = HashToken(refreshToken);
        var isAdmin = await database.Users.AnyAsync(user => user.Id == userId && user.AccountType == AccountType.Admin,
            cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (isAdmin)
            await AdminOperations.AcquireLockAsync(database, cancellationToken);
        await AcquireFamilyLockAsync(database, familyId, cancellationToken);
        var exists = await database.RefreshSessions.AnyAsync(item => item.UserId == userId &&
            item.FamilyId == familyId && item.TokenHash == tokenHash &&
            item.RevokedAt == null, cancellationToken);
        if (!exists) return false;
        var now = clock.GetUtcNow();
        await RevokeFamilyAsync(familyId, now, cancellationToken);
        database.AuditEvents.Add(Audit("auth.logout", userId, traceId, now));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<BrowserAuthTokens?> BrowserLoginAsync(AccountType role, string contactType,
        string contact, string password, string traceId, CancellationToken cancellationToken)
    {
        if (role is not (AccountType.Customer or AccountType.VenueOperator)) return null;
        var tokens = await LoginForPortalAsync(contactType, contact, password, traceId, false, cancellationToken, role);
        if (tokens is null) return null;
        var activity = await database.RefreshSessions.Where(item => item.TokenHash == HashToken(tokens.RefreshToken))
            .Select(item => item.LastActivityAt).SingleAsync(cancellationToken);
        return new(tokens, activity!.Value.AddMinutes(30));
    }

    public async Task<BrowserAuthTokens?> RestoreBrowserAsync(AccountType role, string refreshToken,
        bool activity, string traceId, CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256 || role is not (AccountType.Customer or AccountType.VenueOperator)) return null;
        var hash = HashToken(refreshToken);
        var family = await database.RefreshSessions.AsNoTracking().Where(item => item.TokenHash == hash)
            .Select(item => (Guid?)item.FamilyId).SingleOrDefaultAsync(cancellationToken);
        if (family is null) return null;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AcquireFamilyLockAsync(database, family.Value, cancellationToken);
        var session = await database.RefreshSessions.SingleAsync(item => item.TokenHash == hash, cancellationToken);
        await database.Entry(session).ReloadAsync(cancellationToken);
        var user = await database.Users.AsNoTracking().SingleAsync(item => item.Id == session.UserId, cancellationToken);
        var now = clock.GetUtcNow();
        if (session.ConsumedAt is not null || session.RevokedAt is not null || user.AccountType != role) return null;
        if (session.ExpiresAt <= now || session.LastActivityAt is null ||
            session.LastActivityAt <= now.AddMinutes(-30) || !CanLogin(user))
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (activity)
        {
            session.LastActivityAt = now;
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        var idle = session.LastActivityAt.Value.AddMinutes(30);
        return new(Tokens(user, session.FamilyId, refreshToken, now, session.ExpiresAt),
            idle < session.ExpiresAt ? idle : session.ExpiresAt);
    }

    public async Task LogoutBrowserAsync(AccountType role, string refreshToken, string traceId, CancellationToken cancellationToken)
    {
        if (refreshToken.Length is < 32 or > 256) return;
        var hash = HashToken(refreshToken);
        var lookup = await database.RefreshSessions.AsNoTracking().Where(item => item.TokenHash == hash && item.User.AccountType == role)
            .Select(item => new { item.UserId, item.FamilyId }).SingleOrDefaultAsync(cancellationToken);
        if (lookup is not null) await LogoutAsync(lookup.UserId, lookup.FamilyId, refreshToken, traceId, cancellationToken);
    }

    // Transaction-scoped family lock serializes replacement insertion and family revocation.
    public static Task AcquireFamilyLockAsync(ShuttleBookDbContext database, Guid familyId, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({familyId.ToString()}, 771904))", cancellationToken);

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        database.RefreshSessions.Where(item => item.FamilyId == familyId && item.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(item => item.RevokedAt, now), cancellationToken);

    private AuthTokens Tokens(User user, Guid familyId, string refresh, DateTimeOffset now,
        DateTimeOffset? refreshExpiresAt = null)
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
            (int)AccessLifetime.TotalSeconds, refresh, refreshExpiresAt ?? now.Add(RefreshLifetime),
            new AuthUser(user.Id, AccountTypeName(user.AccountType), StatusName(user.Status)));
    }

    public static bool CanLogin(User user) =>
        user.AccountType == AccountType.Customer && user.Status == UserStatus.Active ||
        user.AccountType == AccountType.VenueOperator && user.Status is UserStatus.PendingOnboarding or UserStatus.Active;

    public static bool CanAdminLogin(User user) =>
        user.AccountType == AccountType.Admin && user.Status == UserStatus.Active;

    public static bool CanUseSession(User user) => CanLogin(user) || CanAdminLogin(user);

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
