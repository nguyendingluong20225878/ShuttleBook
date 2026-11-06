using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Recruitment;

public static class RecruitmentModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<RecruitmentPost>(e =>
        {
            e.ToTable("recruitment_posts", t => t.HasCheckConstraint("ck_recruitment_post_valid",
                "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED','NEWBIE','Y_MINUS','Y','Y_PLUS','TBY_MINUS','TBY','TBY_PLUS','TB_MINUS','TB','TB_PLUS','TBK_MINUS','TBK','TBK_PLUS') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0 AND char_length(caption) <= 1000 AND char_length(contact_name) <= 100 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20) AND NOT (publish_contact_phone AND share_phone_with_accepted) AND (NOT (publish_contact_phone OR share_phone_with_accepted) OR contact_phone IS NOT NULL) AND contribution_mode IN ('UNSPECIFIED','COMMON','BY_GENDER') AND ((contribution_mode = 'UNSPECIFIED' AND common_contribution_vnd IS NULL AND male_contribution_vnd IS NULL AND female_contribution_vnd IS NULL) OR (contribution_mode = 'COMMON' AND common_contribution_vnd IS NOT NULL AND common_contribution_vnd BETWEEN 0 AND 2000000 AND male_contribution_vnd IS NULL AND female_contribution_vnd IS NULL) OR (contribution_mode = 'BY_GENDER' AND common_contribution_vnd IS NULL AND male_contribution_vnd IS NOT NULL AND male_contribution_vnd BETWEEN 0 AND 2000000 AND female_contribution_vnd IS NOT NULL AND male_contribution_vnd BETWEEN 0 AND 2000000))"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BookingId).HasColumnName("booking_id");
            e.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            e.Property(x => x.SlotsNeeded).HasColumnName("slots_needed");
            e.Property(x => x.Level).HasColumnName("level").HasMaxLength(16);
            e.Property(x => x.Caption).HasColumnName("caption").HasMaxLength(1000);
            e.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(100);
            e.Property(x => x.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20);
            e.Property(x => x.SharePhoneWithAccepted).HasColumnName("share_phone_with_accepted");
            e.Property(x => x.PublishContactPhone).HasColumnName("publish_contact_phone");
            e.Property(x => x.ContributionMode).HasColumnName("contribution_mode").HasMaxLength(16);
            e.Property(x => x.CommonContributionVnd).HasColumnName("common_contribution_vnd");
            e.Property(x => x.MaleContributionVnd).HasColumnName("male_contribution_vnd");
            e.Property(x => x.FemaleContributionVnd).HasColumnName("female_contribution_vnd");
            e.Property(x => x.ImageUploadId).HasColumnName("image_upload_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<Booking>().WithMany().HasForeignKey(x => new { x.BookingId, x.OwnerUserId })
                .HasPrincipalKey(x => new { x.Id, x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MediaUpload>().WithMany().HasForeignKey(x => x.ImageUploadId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasIndex(x => new { x.OwnerUserId, x.Status });
            e.HasIndex(x => x.Status).HasFilter("status = 'OPEN'");
            e.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
        });
        model.Entity<RecruitmentRequest>(e =>
        {
            e.ToTable("recruitment_requests", t => t.HasCheckConstraint("ck_recruitment_request_valid",
                "level IN ('BEGINNER','INTERMEDIATE','ADVANCED','UNSPECIFIED') AND status IN ('PENDING','ACCEPTED','DECLINED','WITHDRAWN') AND version > 0 AND char_length(message) <= 500 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20) AND (share_phone_with_accepted = false OR contact_phone IS NOT NULL)"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PostId).HasColumnName("post_id");
            e.Property(x => x.ApplicantUserId).HasColumnName("applicant_user_id");
            e.Property(x => x.Level).HasColumnName("level").HasMaxLength(16);
            e.Property(x => x.Message).HasColumnName("message").HasMaxLength(500);
            e.Property(x => x.ContactPhone).HasColumnName("contact_phone").HasMaxLength(20);
            e.Property(x => x.SharePhoneWithAccepted).HasColumnName("share_phone_with_accepted");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<RecruitmentPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ApplicantUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PostId, x.ApplicantUserId }).IsUnique();
            e.HasIndex(x => new { x.PostId, x.Status });
            e.HasIndex(x => new { x.ApplicantUserId, x.Id });
        });
        model.Entity<RecruitmentReport>(e =>
        {
            e.ToTable("recruitment_reports", t => t.HasCheckConstraint("ck_recruitment_report_valid",
                "reason IN ('SPAM','MISLEADING','UNSAFE') AND status IN ('OPEN','DISMISSED','RESOLVED') AND ((status='OPEN' AND resolved_at IS NULL AND resolved_by IS NULL) OR (status<>'OPEN' AND resolved_at IS NOT NULL AND resolved_by IS NOT NULL))"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PostId).HasColumnName("post_id");
            e.Property(x => x.ReporterUserId).HasColumnName("reporter_user_id");
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(16);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            e.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            e.HasOne<RecruitmentPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PostId, x.ReporterUserId }).IsUnique();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
    }
}
