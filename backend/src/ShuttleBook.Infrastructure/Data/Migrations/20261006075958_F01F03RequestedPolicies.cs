using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F01F03RequestedPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_activity_at",
                table: "refresh_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "booking_block_minutes",
                table: "courts",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "hold_minutes",
                table: "courts",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "minimum_booking_minutes",
                table: "courts",
                type: "integer",
                nullable: false,
                defaultValue: 30);
            migrationBuilder.Sql("""
                UPDATE refresh_sessions SET last_activity_at = created_at
                WHERE user_id IN (SELECT id FROM users WHERE account_type = 'Admin');
                ALTER TABLE courts ADD CONSTRAINT ck_courts_booking_policy CHECK (
                    booking_block_minutes IN (30, 60, 90) AND
                    minimum_booking_minutes BETWEEN booking_block_minutes AND 480 AND
                    minimum_booking_minutes % booking_block_minutes = 0 AND
                    hold_minutes BETWEEN 5 AND 60);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE courts DROP CONSTRAINT ck_courts_booking_policy;");
            migrationBuilder.DropColumn(
                name: "last_activity_at",
                table: "refresh_sessions");

            migrationBuilder.DropColumn(
                name: "booking_block_minutes",
                table: "courts");

            migrationBuilder.DropColumn(
                name: "hold_minutes",
                table: "courts");

            migrationBuilder.DropColumn(
                name: "minimum_booking_minutes",
                table: "courts");
        }
    }
}
