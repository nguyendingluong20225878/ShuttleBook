using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F02OwnerSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Legacy revisions compared the whole venue version. Add a sensitive-field
                -- baseline only when that snapshot is still current, so a direct photo edit
                -- after upgrade can coexist with approval without reviving a stale revision.
                UPDATE approval_requests AS r
                SET snapshot = jsonb_set(r.snapshot, '{baseline}', jsonb_build_object(
                    'address', v.address, 'contact', v.contact, 'timezone', v.timezone,
                    'latitude', v.latitude, 'longitude', v.longitude,
                    'bankCode', p.bank_code, 'accountName', p.account_name,
                    'accountNumber', p.account_number, 'qrUploadId', p.qr_upload_id))
                FROM venues AS v JOIN venue_payment_accounts AS p ON p.venue_id = v.id
                WHERE r.venue_id = v.id AND r.business_id = v.business_id
                    AND r.kind = 'VENUE_REVISION' AND r.status = 'PENDING'
                    AND r.snapshot ->> 'version' = v.version::text
                    AND (NOT (r.snapshot ? 'baseline') OR r.snapshot -> 'baseline' = 'null'::jsonb);
                ALTER TABLE approval_requests DROP CONSTRAINT ck_approval_kind_venue;
                ALTER TABLE courts DROP CONSTRAINT ck_courts_status;
                ALTER TABLE courts ADD CONSTRAINT ck_courts_status
                    CHECK (status IN ('INACTIVE','PENDING_APPROVAL','ACTIVE','SUSPENDED'));
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads");

            migrationBuilder.AddColumn<Guid>(
                name: "court_id",
                table: "media_uploads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "image_upload_id",
                table: "courts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "court_id",
                table: "approval_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_court_id_venue_id",
                table: "media_uploads",
                columns: new[] { "court_id", "venue_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads",
                sql: "(purpose='PAYMENT_PROOF' AND booking_id IS NOT NULL AND court_id IS NULL) OR (purpose IN ('QR','VENUE_IMAGE') AND booking_id IS NULL AND court_id IS NULL) OR (purpose='COURT_IMAGE' AND booking_id IS NULL AND court_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_courts_image_upload_id",
                table: "courts",
                column: "image_upload_id");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_court_id_venue_id",
                table: "approval_requests",
                columns: new[] { "court_id", "venue_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_approval_kind_resource",
                table: "approval_requests",
                sql: "(kind IN ('ONBOARDING','BUSINESS_REVISION') AND venue_id IS NULL AND court_id IS NULL) OR (kind IN ('VENUE_REVISION','VENUE_ADDITION') AND venue_id IS NOT NULL AND court_id IS NULL) OR (kind='COURT_ADDITION' AND venue_id IS NOT NULL AND court_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_approval_requests_courts_court_id_venue_id",
                table: "approval_requests",
                columns: new[] { "court_id", "venue_id" },
                principalTable: "courts",
                principalColumns: new[] { "id", "venue_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_courts_media_uploads_image_upload_id",
                table: "courts",
                column: "image_upload_id",
                principalTable: "media_uploads",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_media_uploads_courts_court_id_venue_id",
                table: "media_uploads",
                columns: new[] { "court_id", "venue_id" },
                principalTable: "courts",
                principalColumns: new[] { "id", "venue_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback is rejected while new approval kinds or pending courts are present.
            // Existing bookings, venue images and the old approval kinds are preserved.
            migrationBuilder.Sql("""
                ALTER TABLE courts DROP CONSTRAINT ck_courts_status;
                ALTER TABLE courts ADD CONSTRAINT ck_courts_status
                    CHECK (status IN ('INACTIVE','ACTIVE','SUSPENDED'));
                ALTER TABLE approval_requests ADD CONSTRAINT ck_approval_kind_venue
                    CHECK ((kind = 'ONBOARDING' AND venue_id IS NULL) OR
                           (kind = 'VENUE_REVISION' AND venue_id IS NOT NULL));
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_approval_requests_courts_court_id_venue_id",
                table: "approval_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_courts_media_uploads_image_upload_id",
                table: "courts");

            migrationBuilder.DropForeignKey(
                name: "FK_media_uploads_courts_court_id_venue_id",
                table: "media_uploads");

            migrationBuilder.DropIndex(
                name: "IX_media_uploads_court_id_venue_id",
                table: "media_uploads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads");

            migrationBuilder.DropIndex(
                name: "IX_courts_image_upload_id",
                table: "courts");

            migrationBuilder.DropIndex(
                name: "IX_approval_requests_court_id_venue_id",
                table: "approval_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_approval_kind_resource",
                table: "approval_requests");

            migrationBuilder.DropColumn(
                name: "court_id",
                table: "media_uploads");

            migrationBuilder.DropColumn(
                name: "image_upload_id",
                table: "courts");

            migrationBuilder.DropColumn(
                name: "court_id",
                table: "approval_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads",
                sql: "(purpose='PAYMENT_PROOF' AND booking_id IS NOT NULL) OR (purpose IN ('QR','VENUE_IMAGE') AND booking_id IS NULL)");
        }
    }
}
