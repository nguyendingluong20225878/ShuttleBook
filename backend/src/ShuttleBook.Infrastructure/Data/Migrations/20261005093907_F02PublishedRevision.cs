using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F02PublishedRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "approval_requests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ONBOARDING");

            migrationBuilder.AddColumn<Guid>(
                name: "venue_id",
                table: "approval_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_venue_id",
                table: "approval_requests",
                column: "venue_id");

            migrationBuilder.AddForeignKey(
                name: "FK_approval_requests_venues_venue_id",
                table: "approval_requests",
                column: "venue_id",
                principalTable: "venues",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                ALTER TABLE approval_requests ADD CONSTRAINT ck_approval_kind_venue
                  CHECK ((kind = 'ONBOARDING' AND venue_id IS NULL) OR
                         (kind = 'VENUE_REVISION' AND venue_id IS NOT NULL));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE approval_requests DROP CONSTRAINT ck_approval_kind_venue");
            migrationBuilder.DropForeignKey(
                name: "FK_approval_requests_venues_venue_id",
                table: "approval_requests");

            migrationBuilder.DropIndex(
                name: "IX_approval_requests_venue_id",
                table: "approval_requests");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "approval_requests");

            migrationBuilder.DropColumn(
                name: "venue_id",
                table: "approval_requests");
        }
    }
}
