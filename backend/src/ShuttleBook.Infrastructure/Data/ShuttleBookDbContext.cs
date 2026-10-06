using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Data;

public sealed class ShuttleBookDbContext(DbContextOptions<ShuttleBookDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ContactVerificationChallenge> ContactVerificationChallenges => Set<ContactVerificationChallenge>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessMembership> BusinessMemberships => Set<BusinessMembership>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<CourtOperatingHour> CourtOperatingHours => Set<CourtOperatingHour>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<CourtAllocation> CourtAllocations => Set<CourtAllocation>();
    public DbSet<CourtMaintenance> CourtMaintenance => Set<CourtMaintenance>();
    public DbSet<VenuePaymentAccount> VenuePaymentAccounts => Set<VenuePaymentAccount>();
    public DbSet<MediaUpload> MediaUploads => Set<MediaUpload>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();

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
            entity.Property(session => session.LastActivityAt).HasColumnName("last_activity_at");
            entity.Property(session => session.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(session => session.RevokedAt).HasColumnName("revoked_at");
            entity.Property(session => session.ReplacedById).HasColumnName("replaced_by_id");
            entity.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => session.FamilyId);
            entity.HasIndex(session => new { session.UserId, session.CreatedAt });
        });

        modelBuilder.Entity<Business>(entity =>
        {
            entity.ToTable("businesses"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160);
            entity.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(200);
            entity.Property(x => x.Contact).HasColumnName("contact").HasMaxLength(160);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
        modelBuilder.Entity<BusinessMembership>(entity =>
        {
            entity.ToTable("business_memberships"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.BusinessId).HasColumnName("business_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(32);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BusinessId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Role, x.Status });
        });
        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.BusinessId).HasColumnName("business_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160);
            entity.Property(x => x.Address).HasColumnName("address").HasMaxLength(400);
            entity.Property(x => x.Contact).HasColumnName("contact").HasMaxLength(160);
            entity.Property(x => x.Timezone).HasColumnName("timezone").HasMaxLength(80);
            entity.Property(x => x.Latitude).HasColumnName("latitude");
            entity.Property(x => x.Longitude).HasColumnName("longitude");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.ImageUploadId).HasColumnName("image_upload_id");
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BusinessId);
        });
        modelBuilder.Entity<Court>(entity =>
        {
            entity.ToTable("courts"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.VenueId).HasColumnName("venue_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.Version).HasColumnName("version");
            entity.Property(x => x.BookingBlockMinutes).HasColumnName("booking_block_minutes");
            entity.Property(x => x.MinimumBookingMinutes).HasColumnName("minimum_booking_minutes");
            entity.Property(x => x.HoldMinutes).HasColumnName("hold_minutes");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.VenueId, x.Name }).IsUnique();
        });
        modelBuilder.Entity<CourtOperatingHour>(entity =>
        {
            entity.ToTable("court_operating_hours"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CourtId).HasColumnName("court_id");
            entity.Property(x => x.DayOfWeek).HasColumnName("day_of_week");
            entity.Property(x => x.OpensAt).HasColumnName("opens_at");
            entity.Property(x => x.ClosesAt).HasColumnName("closes_at");
            entity.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CourtId, x.DayOfWeek }).IsUnique();
        });
        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.ToTable("pricing_rules"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CourtId).HasColumnName("court_id");
            entity.Property(x => x.DayOfWeek).HasColumnName("day_of_week");
            entity.Property(x => x.StartsOn).HasColumnName("starts_on");
            entity.Property(x => x.EndsOn).HasColumnName("ends_on");
            entity.Property(x => x.StartsAt).HasColumnName("starts_at");
            entity.Property(x => x.EndsAt).HasColumnName("ends_at");
            entity.Property(x => x.PricePerSlot).HasColumnName("price_per_slot");
            entity.Property(x => x.Priority).HasColumnName("priority");
            entity.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CourtId, x.DayOfWeek, x.StartsOn, x.EndsOn, x.StartsAt, x.EndsAt, x.Priority }).IsUnique();
        });
        modelBuilder.Entity<CourtAllocation>(entity =>
        {
            entity.ToTable("court_allocations"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CourtId).HasColumnName("court_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32);
            entity.Property(x => x.StartsAt).HasColumnName("starts_at");
            entity.Property(x => x.EndsAt).HasColumnName("ends_at");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ReleasedAt).HasColumnName("released_at");
            entity.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CourtId, x.StartsAt, x.EndsAt });
        });
        modelBuilder.Entity<CourtMaintenance>(entity =>
        {
            entity.ToTable("court_maintenance"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CourtId).HasColumnName("court_id");
            entity.Property(x => x.AllocationId).HasColumnName("allocation_id");
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
            entity.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            entity.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CourtAllocation>().WithMany().HasForeignKey(x => x.AllocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.AllocationId).IsUnique();
            entity.HasIndex(x => new { x.CourtId, x.Status, x.CreatedAt });
        });
        modelBuilder.Entity<VenuePaymentAccount>(entity =>
        {
            entity.ToTable("venue_payment_accounts"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.VenueId).HasColumnName("venue_id");
            entity.Property(x => x.BankCode).HasColumnName("bank_code").HasMaxLength(32);
            entity.Property(x => x.AccountName).HasColumnName("account_name").HasMaxLength(160);
            entity.Property(x => x.AccountNumber).HasColumnName("account_number").HasMaxLength(40);
            entity.Property(x => x.QrUploadId).HasColumnName("qr_upload_id");
            entity.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.VenueId).IsUnique();
        });
        modelBuilder.Entity<MediaUpload>(entity =>
        {
            entity.ToTable("media_uploads"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(x => x.VenueId).HasColumnName("venue_id");
            entity.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(32);
            entity.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(300);
            entity.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100);
            entity.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            entity.Property(x => x.Sha256Base64).HasColumnName("sha256_base64").HasMaxLength(100);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ObjectKey).IsUnique();
        });
        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("approval_requests"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.BusinessId).HasColumnName("business_id");
            entity.Property(x => x.VenueId).HasColumnName("venue_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32);
            entity.Property(x => x.SubmittedBy).HasColumnName("submitted_by");
            entity.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            entity.Property(x => x.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
            entity.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            entity.HasOne<Business>().WithMany().HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BusinessId, x.Status });
        });
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80);
            entity.Property(x => x.TargetUserId).HasColumnName("target_user_id");
            entity.Property(x => x.EntityId).HasColumnName("entity_id");
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(x => x.Attempts).HasColumnName("attempts");
            entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.TargetUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
        });
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OutboxMessageId).HasColumnName("outbox_message_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(160);
            entity.Property(x => x.Body).HasColumnName("body").HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ReadAt).HasColumnName("read_at");
            entity.HasOne<OutboxMessage>().WithMany().HasForeignKey(x => x.OutboxMessageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OutboxMessageId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
        });
    }
}
