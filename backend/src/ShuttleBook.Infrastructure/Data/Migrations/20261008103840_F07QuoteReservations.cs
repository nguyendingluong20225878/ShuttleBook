using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F07QuoteReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE court_allocations DROP CONSTRAINT ck_f03_allocation_valid; ALTER TABLE court_allocations ADD CONSTRAINT ck_f03_allocation_valid CHECK (ends_at > starts_at AND kind IN ('MAINTENANCE','BOOKING','QUOTE_HOLD') AND status IN ('RESERVED','RELEASED'));");
            migrationBuilder.AddColumn<Guid>(
                name: "quote_reservation_id",
                table: "court_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quote_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_reservations", x => x.id);
                    table.UniqueConstraint("AK_quote_reservations_id_court_id", x => new { x.id, x.court_id });
                    table.CheckConstraint("ck_quote_reservation_valid", "expires_at > created_at AND kind IN ('CASUAL','SERIES') AND NOT (consumed_at IS NOT NULL AND released_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_quote_reservations_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quote_reservations_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_court_allocations_quote_reservation_id_court_id",
                table: "court_allocations",
                columns: new[] { "quote_reservation_id", "court_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_allocation_scope",
                table: "court_allocations",
                sql: "(kind='QUOTE_HOLD' AND quote_reservation_id IS NOT NULL) OR kind='BOOKING' OR (kind='MAINTENANCE' AND quote_reservation_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_quote_reservations_court_id",
                table: "quote_reservations",
                column: "court_id");

            migrationBuilder.CreateIndex(
                name: "IX_quote_reservations_customer_id_court_id",
                table: "quote_reservations",
                columns: new[] { "customer_id", "court_id" },
                unique: true,
                filter: "consumed_at IS NULL AND released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_quote_reservations_expires_at",
                table: "quote_reservations",
                column: "expires_at");

            migrationBuilder.AddForeignKey(
                name: "FK_court_allocations_quote_reservations_quote_reservation_id_c~",
                table: "court_allocations",
                columns: new[] { "quote_reservation_id", "court_id" },
                principalTable: "quote_reservations",
                principalColumns: new[] { "id", "court_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only ephemeral quote holds are removed. Consumed BOOKING allocations remain intact.
            migrationBuilder.Sql("DELETE FROM court_allocations WHERE kind='QUOTE_HOLD'; ALTER TABLE court_allocations DROP CONSTRAINT ck_f03_allocation_valid; ALTER TABLE court_allocations ADD CONSTRAINT ck_f03_allocation_valid CHECK (ends_at > starts_at AND kind IN ('MAINTENANCE','BOOKING') AND status IN ('RESERVED','RELEASED'));");
            migrationBuilder.DropForeignKey(
                name: "FK_court_allocations_quote_reservations_quote_reservation_id_c~",
                table: "court_allocations");

            migrationBuilder.DropTable(
                name: "quote_reservations");

            migrationBuilder.DropIndex(
                name: "IX_court_allocations_quote_reservation_id_court_id",
                table: "court_allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_allocation_scope",
                table: "court_allocations");

            migrationBuilder.DropColumn(
                name: "quote_reservation_id",
                table: "court_allocations");
        }
    }
}
