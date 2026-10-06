namespace ShuttleBook.Infrastructure.Identity;

public enum AccountType
{
    Customer,
    VenueOperator,
    Admin
}

public enum UserStatus
{
    PendingVerification,
    Active,
    PendingOnboarding,
    Suspended
}

public enum ContactType
{
    Email,
    Phone
}

public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public AccountType AccountType { get; set; }
    public UserStatus Status { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? Phone { get; set; }
    public string? NormalizedPhone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ContactVerificationChallenge> VerificationChallenges { get; } = [];
}

public sealed class ContactVerificationChallenge
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public ContactType ContactType { get; set; }
    public byte[] CodeHash { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
}
