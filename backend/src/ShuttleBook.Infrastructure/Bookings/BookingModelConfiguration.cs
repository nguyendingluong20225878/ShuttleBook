using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Infrastructure.Bookings;

public static class BookingModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<BookingQuote>(e =>
        {
            e.ToTable("booking_quotes", t => t.HasCheckConstraint("ck_booking_quote_interval",
                "ends_at > starts_at AND amount >= 0 AND expires_at > created_at"));
            e.Property(x => x.Slots).HasColumnType("jsonb");
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.Timezone).HasMaxLength(100);
            e.Property(x => x.Amount).HasColumnType("numeric(18,0)");
            e.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ExpiresAt);
        });
        model.Entity<Booking>(e =>
        {
            e.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("ck_booking_valid", "ends_at > starts_at AND amount >= 0 AND version > 0 AND hold_minutes BETWEEN 5 AND 60 AND booking_type = 'CASUAL' AND status IN ('AWAITING_TRANSFER','AWAITING_OWNER_CONFIRMATION','NEEDS_REVIEW','CONFIRMED','EXPIRED','PAYMENT_REJECTED')");
                t.HasCheckConstraint("ck_booking_grid", "MOD(EXTRACT(EPOCH FROM (ends_at-starts_at))::bigint,1800)=0 AND EXTRACT(SECOND FROM starts_at)=0 AND EXTRACT(SECOND FROM ends_at)=0");
            });
            e.Property(x => x.Slots).HasColumnType("jsonb");
            e.Property(x => x.Amount).HasColumnType("numeric(18,0)");
            e.Property(x => x.BookingNo).HasMaxLength(40);
            e.Property(x => x.BookingType).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Timezone).HasMaxLength(100);
            e.HasIndex(x => x.BookingNo).IsUnique();
            e.HasIndex(x => x.AllocationId).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.Id });
            e.HasIndex(x => new { x.VenueId, x.Status, x.LocalDate, x.Id });
            e.HasIndex(x => new { x.Status, x.PaymentDeadline });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Court>().WithMany().HasForeignKey(x => new { x.CourtId, x.VenueId })
                .HasPrincipalKey(x => new { x.Id, x.VenueId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CourtAllocation>().WithMany().HasForeignKey(x => new { x.AllocationId, x.CourtId })
                .HasPrincipalKey(x => new { x.Id, x.CourtId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BookingPayment>(e =>
        {
            e.ToTable("payments", t =>
            {
                t.HasCheckConstraint("ck_payment_valid", "expected_amount >= 0 AND status IN ('AWAITING_TRANSFER','TRANSFER_REPORTED','NEEDS_REVIEW','PAID','EXPIRED','REJECTED')");
                t.HasCheckConstraint("ck_payment_confirmation", "status <> 'PAID' OR (confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL AND confirmed_amount IS NOT NULL AND confirmed_amount >= 0)");
                t.HasCheckConstraint("ck_payment_exact_amount", "status <> 'PAID' OR confirmed_amount = expected_amount");
            });
            e.Property(x => x.RecipientSnapshot).HasColumnType("jsonb");
            e.Property(x => x.ExpectedAmount).HasColumnType("numeric(18,0)");
            e.Property(x => x.ConfirmedAmount).HasColumnType("numeric(18,0)");
            e.Property(x => x.Status).HasMaxLength(32);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MediaUpload>().WithMany().HasForeignKey(x => x.QrUploadId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FirstReportedAt, x.ConfirmationAlertedAt });
        });
        model.Entity<BookingIdempotency>(e =>
        {
            e.ToTable("idempotency_records");
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.Operation).HasMaxLength(32);
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.HasIndex(x => new { x.ActorUserId, x.Operation, x.Key }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PaymentEvidence>(e =>
        {
            e.ToTable("payment_evidence", t => t.HasCheckConstraint("ck_evidence_kind", "kind IN ('INITIAL','SUPPLEMENT')"));
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.BankReference).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne<BookingPayment>().WithMany().HasForeignKey(x => new { x.PaymentId, x.BookingId })
                .HasPrincipalKey(x => new { x.Id, x.BookingId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Booking>().WithMany().HasForeignKey(x => new { x.BookingId, x.CustomerId, x.VenueId })
                .HasPrincipalKey(x => new { x.Id, x.CustomerId, x.VenueId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MediaUpload>().WithMany().HasForeignKey(x => x.ProofUploadId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ProofUploadId).IsUnique().HasFilter("proof_upload_id IS NOT NULL");
            e.HasIndex(x => new { x.PaymentId, x.ReportedAt, x.Id });
        });
        model.Entity<PaymentDecision>(e =>
        {
            e.ToTable("payment_decisions", t => t.HasCheckConstraint("ck_decision_valid",
                "(resolution='CONFIRMED' AND confirmed_amount IS NOT NULL AND confirmed_amount >= 0) OR (resolution IN ('NEEDS_REVIEW','FINAL_REJECTION') AND reason_code IS NOT NULL AND reason IS NOT NULL AND length(btrim(reason))>0)"));
            e.Property(x => x.Resolution).HasMaxLength(32);
            e.Property(x => x.ReasonCode).HasMaxLength(32);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.BankReference).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.ConfirmedAmount).HasColumnType("numeric(18,0)");
            e.HasOne<BookingPayment>().WithMany().HasForeignKey(x => new { x.PaymentId, x.BookingId })
                .HasPrincipalKey(x => new { x.Id, x.BookingId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PaymentId, x.DecidedAt, x.Id });
        });
        foreach (var type in new[] { typeof(BookingQuote), typeof(Booking), typeof(BookingPayment), typeof(BookingIdempotency), typeof(PaymentEvidence), typeof(PaymentDecision) })
            foreach (var property in model.Entity(type).Metadata.GetProperties())
                property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
