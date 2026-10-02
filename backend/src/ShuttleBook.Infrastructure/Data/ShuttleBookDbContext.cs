using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Infrastructure.Data;

public sealed class ShuttleBookDbContext(DbContextOptions<ShuttleBookDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ContactVerificationChallenge> ContactVerificationChallenges => Set<ContactVerificationChallenge>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", table => table.HasCheckConstraint(
                "ck_users_exactly_one_contact", "(normalized_email IS NOT NULL) <> (normalized_phone IS NOT NULL)"));
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.AccountType).HasColumnName("account_type").HasConversion<string>().HasMaxLength(32);
            entity.Property(user => user.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(320);
            entity.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320);
            entity.Property(user => user.Phone).HasColumnName("phone").HasMaxLength(32);
            entity.Property(user => user.NormalizedPhone).HasColumnName("normalized_phone").HasMaxLength(32);
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(512);
            entity.Property(user => user.EmailVerifiedAt).HasColumnName("email_verified_at");
            entity.Property(user => user.PhoneVerifiedAt).HasColumnName("phone_verified_at");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(user => user.NormalizedEmail).HasDatabaseName("ux_users_normalized_email").IsUnique()
                .HasFilter("normalized_email IS NOT NULL");
            entity.HasIndex(user => user.NormalizedPhone).HasDatabaseName("ux_users_normalized_phone").IsUnique()
                .HasFilter("normalized_phone IS NOT NULL");
        });

        modelBuilder.Entity<ContactVerificationChallenge>(entity =>
        {
            entity.ToTable("contact_verification_challenges");
            entity.HasKey(challenge => challenge.Id);
            entity.Property(challenge => challenge.Id).HasColumnName("id");
            entity.Property(challenge => challenge.UserId).HasColumnName("user_id");
            entity.Property(challenge => challenge.ContactType).HasColumnName("contact_type").HasConversion<string>().HasMaxLength(16);
            entity.Property(challenge => challenge.CodeHash).HasColumnName("code_hash");
            entity.Property(challenge => challenge.ExpiresAt).HasColumnName("expires_at");
            entity.Property(challenge => challenge.AttemptCount).HasColumnName("attempt_count");
            entity.Property(challenge => challenge.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(challenge => challenge.InvalidatedAt).HasColumnName("invalidated_at");
            entity.Property(challenge => challenge.CreatedAt).HasColumnName("created_at");
            entity.HasOne(challenge => challenge.User).WithMany(user => user.VerificationChallenges)
                .HasForeignKey(challenge => challenge.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(challenge => new { challenge.UserId, challenge.CreatedAt })
                .HasDatabaseName("ix_verification_challenges_user_created");
            entity.HasIndex(challenge => new { challenge.UserId, challenge.ContactType }).IsUnique()
                .HasDatabaseName("ux_verification_challenges_one_active")
                .HasFilter("consumed_at IS NULL AND invalidated_at IS NULL");
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Id).HasColumnName("id");
            entity.Property(audit => audit.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(audit => audit.Action).HasColumnName("action").HasMaxLength(100);
            entity.Property(audit => audit.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            entity.Property(audit => audit.EntityId).HasColumnName("entity_id");
            entity.Property(audit => audit.CorrelationId).HasColumnName("correlation_id").HasMaxLength(256);
            entity.Property(audit => audit.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(audit => audit.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(audit => new { audit.EntityType, audit.EntityId, audit.CreatedAt });
        });

        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("refresh_sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Id).HasColumnName("id");
            entity.Property(session => session.FamilyId).HasColumnName("family_id");
            entity.Property(session => session.UserId).HasColumnName("user_id");
            entity.Property(session => session.TokenHash).HasColumnName("token_hash");
            entity.Property(session => session.CreatedAt).HasColumnName("created_at");
            entity.Property(session => session.ExpiresAt).HasColumnName("expires_at");
            entity.Property(session => session.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(session => session.RevokedAt).HasColumnName("revoked_at");
            entity.Property(session => session.ReplacedById).HasColumnName("replaced_by_id");
            entity.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => session.FamilyId);
            entity.HasIndex(session => new { session.UserId, session.CreatedAt });
        });
    }
}
