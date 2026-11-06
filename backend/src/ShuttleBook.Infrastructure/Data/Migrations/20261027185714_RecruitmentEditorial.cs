using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentEditorial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_recruitment_post_valid",
                table: "recruitment_posts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads");

            migrationBuilder.AddColumn<string>(
                name: "caption",
                table: "recruitment_posts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_name",
                table: "recruitment_posts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "recruitment_posts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "image_upload_id",
                table: "recruitment_posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "share_phone_with_accepted",
                table: "recruitment_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_posts_image_upload_id",
                table: "recruitment_posts",
                column: "image_upload_id");

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_posts_status_created_at_id",
                table: "recruitment_posts",
                columns: new[] { "status", "created_at", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_recruitment_post_valid",
                table: "recruitment_posts",
                sql: "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0 AND char_length(caption) <= 1000 AND char_length(contact_name) <= 100 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads",
                sql: "(purpose IN ('PAYMENT_PROOF','RECRUITMENT_IMAGE') AND booking_id IS NOT NULL AND court_id IS NULL) OR (purpose IN ('QR','VENUE_IMAGE') AND booking_id IS NULL AND court_id IS NULL) OR (purpose='COURT_IMAGE' AND booking_id IS NULL AND court_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_recruitment_posts_media_uploads_image_upload_id",
                table: "recruitment_posts",
                column: "image_upload_id",
                principalTable: "media_uploads",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recruitment_posts_media_uploads_image_upload_id",
                table: "recruitment_posts");

            migrationBuilder.DropIndex(
                name: "IX_recruitment_posts_image_upload_id",
                table: "recruitment_posts");

            migrationBuilder.DropIndex(
                name: "IX_recruitment_posts_status_created_at_id",
                table: "recruitment_posts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recruitment_post_valid",
                table: "recruitment_posts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads");

            migrationBuilder.DropColumn(
                name: "caption",
                table: "recruitment_posts");

            migrationBuilder.DropColumn(
                name: "contact_name",
                table: "recruitment_posts");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "recruitment_posts");

            migrationBuilder.DropColumn(
                name: "image_upload_id",
                table: "recruitment_posts");

            migrationBuilder.DropColumn(
                name: "share_phone_with_accepted",
                table: "recruitment_posts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recruitment_post_valid",
                table: "recruitment_posts",
                sql: "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads",
                sql: "(purpose='PAYMENT_PROOF' AND booking_id IS NOT NULL AND court_id IS NULL) OR (purpose IN ('QR','VENUE_IMAGE') AND booking_id IS NULL AND court_id IS NULL) OR (purpose='COURT_IMAGE' AND booking_id IS NULL AND court_id IS NOT NULL)");
        }
    }
}
