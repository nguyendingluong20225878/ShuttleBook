using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F07FixedSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_booking_valid",
                table: "bookings");

            migrationBuilder.AddColumn<Guid>(
                name: "payment_scope_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "payment_scope_id",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "series_id",
                table: "bookings",
                type: "uuid",
                nullable: true);

            // Preserve every existing casual payment/evidence/idempotency target.
            // Empty defaults are temporary only during the backfill.
            migrationBuilder.Sql("UPDATE bookings SET payment_scope_id=id; UPDATE payments SET payment_scope_id=booking_id; ALTER TABLE bookings ALTER COLUMN payment_scope_id DROP DEFAULT; ALTER TABLE payments ALTER COLUMN payment_scope_id DROP DEFAULT;");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_bookings_id_payment_scope_id",
                table: "bookings",
                columns: new[] { "id", "payment_scope_id" });

            migrationBuilder.CreateTable(
                name: "booking_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    local_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_series", x => x.id);
                    table.UniqueConstraint("AK_booking_series_id_customer_id_venue_id_court_id", x => new { x.id, x.customer_id, x.venue_id, x.court_id });
                    table.CheckConstraint("ck_series_valid", "ends_on >= (starts_on + INTERVAL '1 month')::date AND day_of_week BETWEEN 0 AND 6 AND duration_minutes >= 120 AND MOD(duration_minutes,30)=0 AND occurrence_count BETWEEN 1 AND 12 AND amount >= 0 AND payment_plan='FULL_SERIES'");
                    table.ForeignKey(
                        name: "FK_booking_series_courts_court_id_venue_id",
                        columns: x => new { x.court_id, x.venue_id },
                        principalTable: "courts",
                        principalColumns: new[] { "id", "venue_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_series_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_series_quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    local_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurrences = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_series_quotes", x => x.id);
                    table.CheckConstraint("ck_series_quote_valid", "ends_on >= (starts_on + INTERVAL '1 month')::date AND day_of_week BETWEEN 0 AND 6 AND duration_minutes >= 120 AND MOD(duration_minutes,30)=0 AND amount >= 0 AND expires_at > created_at");
                    table.ForeignKey(
                        name: "FK_booking_series_quotes_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_series_quotes_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_booking_id_payment_scope_id",
                table: "payments",
                columns: new[] { "booking_id", "payment_scope_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_scope_id",
                table: "payments",
                column: "payment_scope_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_payment_scope_id",
                table: "bookings",
                column: "payment_scope_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_series_id_customer_id_venue_id_court_id",
                table: "bookings",
                columns: new[] { "series_id", "customer_id", "venue_id", "court_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_series_id_local_date",
                table: "bookings",
                columns: new[] { "series_id", "local_date" },
                unique: true,
                filter: "series_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_booking_payment_scope",
                table: "bookings",
                sql: "payment_scope_id = COALESCE(series_id,id)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_booking_valid",
                table: "bookings",
                sql: "ends_at > starts_at AND amount >= 0 AND version > 0 AND hold_minutes BETWEEN 5 AND 60 AND ((booking_type='CASUAL' AND series_id IS NULL) OR (booking_type='RECURRING_OCCURRENCE' AND series_id IS NOT NULL)) AND status IN ('AWAITING_TRANSFER','AWAITING_OWNER_CONFIRMATION','NEEDS_REVIEW','CONFIRMED','EXPIRED','PAYMENT_REJECTED')");

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_court_id_venue_id",
                table: "booking_series",
                columns: new[] { "court_id", "venue_id" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_customer_id",
                table: "booking_series",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_series_no",
                table: "booking_series",
                column: "series_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_quotes_court_id",
                table: "booking_series_quotes",
                column: "court_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_quotes_expires_at",
                table: "booking_series_quotes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_booking_series_quotes_venue_id",
                table: "booking_series_quotes",
                column: "venue_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_booking_series_series_id_customer_id_venue_id_cour~",
                table: "bookings",
                columns: new[] { "series_id", "customer_id", "venue_id", "court_id" },
                principalTable: "booking_series",
                principalColumns: new[] { "id", "customer_id", "venue_id", "court_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_bookings_booking_id_payment_scope_id",
                table: "payments",
                columns: new[] { "booking_id", "payment_scope_id" },
                principalTable: "bookings",
                principalColumns: new[] { "id", "payment_scope_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS(SELECT 1 FROM booking_series) OR EXISTS(SELECT 1 FROM bookings WHERE series_id IS NOT NULL) THEN RAISE EXCEPTION 'F07 downgrade refused: fixed series must be preserved.'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_booking_series_series_id_customer_id_venue_id_cour~",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_bookings_booking_id_payment_scope_id",
                table: "payments");

            migrationBuilder.DropTable(
                name: "booking_series");

            migrationBuilder.DropTable(
                name: "booking_series_quotes");

            migrationBuilder.DropIndex(
                name: "IX_payments_booking_id_payment_scope_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_payment_scope_id",
                table: "payments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_bookings_id_payment_scope_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_payment_scope_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_series_id_customer_id_venue_id_court_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_series_id_local_date",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_booking_payment_scope",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_booking_valid",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "payment_scope_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "payment_scope_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "series_id",
                table: "bookings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_booking_valid",
                table: "bookings",
                sql: "ends_at > starts_at AND amount >= 0 AND version > 0 AND hold_minutes BETWEEN 5 AND 60 AND booking_type = 'CASUAL' AND status IN ('AWAITING_TRANSFER','AWAITING_OWNER_CONFIRMATION','NEEDS_REVIEW','CONFIRMED','EXPIRED','PAYMENT_REJECTED')");
        }
    }
}
