using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F05CasualBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_courts_id_venue_id",
                table: "courts",
                columns: new[] { "id", "venue_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_court_allocations_id_court_id",
                table: "court_allocations",
                columns: new[] { "id", "court_id" });

            migrationBuilder.CreateTable(
                name: "booking_quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    local_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    local_end = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    slots = table.Column<string>(type: "jsonb", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    booking_block_minutes = table.Column<int>(type: "integer", nullable: false),
                    minimum_booking_minutes = table.Column<int>(type: "integer", nullable: false),
                    hold_minutes = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_quotes", x => x.id);
                    table.CheckConstraint("ck_booking_quote_interval", "ends_at > starts_at AND amount >= 0 AND expires_at > created_at");
                    table.ForeignKey(
                        name: "FK_booking_quotes_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_quotes_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    venue_name = table.Column<string>(type: "text", nullable: false),
                    court_name = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    local_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    local_end = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    slots = table.Column<string>(type: "jsonb", nullable: false),
                    booking_block_minutes = table.Column<int>(type: "integer", nullable: false),
                    minimum_booking_minutes = table.Column<int>(type: "integer", nullable: false),
                    hold_minutes = table.Column<int>(type: "integer", nullable: false),
                    payment_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                    table.CheckConstraint("ck_booking_grid", "MOD(EXTRACT(EPOCH FROM (ends_at-starts_at))::bigint,1800)=0 AND EXTRACT(SECOND FROM starts_at)=0 AND EXTRACT(SECOND FROM ends_at)=0");
                    table.CheckConstraint("ck_booking_valid", "ends_at > starts_at AND amount >= 0 AND version > 0 AND hold_minutes BETWEEN 5 AND 60 AND booking_type = 'CASUAL' AND status IN ('AWAITING_TRANSFER','AWAITING_OWNER_CONFIRMATION','NEEDS_REVIEW','CONFIRMED','EXPIRED','PAYMENT_REJECTED')");
                    table.ForeignKey(
                        name: "FK_bookings_court_allocations_allocation_id_court_id",
                        columns: x => new { x.allocation_id, x.court_id },
                        principalTable: "court_allocations",
                        principalColumns: new[] { "id", "court_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_courts_court_id_venue_id",
                        columns: x => new { x.court_id, x.venue_id },
                        principalTable: "courts",
                        principalColumns: new[] { "id", "venue_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bookings_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_idempotency_records_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_idempotency_records_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    recipient_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    qr_upload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("ck_payment_valid", "expected_amount >= 0 AND status IN ('AWAITING_TRANSFER','TRANSFER_REPORTED','NEEDS_REVIEW','PAID','EXPIRED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_media_uploads_qr_upload_id",
                        column: x => x.qr_upload_id,
                        principalTable: "media_uploads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_quotes_court_id",
                table: "booking_quotes",
                column: "court_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_quotes_expires_at",
                table: "booking_quotes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_booking_quotes_venue_id",
                table: "booking_quotes",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_allocation_id",
                table: "bookings",
                column: "allocation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_allocation_id_court_id",
                table: "bookings",
                columns: new[] { "allocation_id", "court_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_booking_no",
                table: "bookings",
                column: "booking_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_court_id_venue_id",
                table: "bookings",
                columns: new[] { "court_id", "venue_id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_customer_id_id",
                table: "bookings",
                columns: new[] { "customer_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_status_payment_deadline",
                table: "bookings",
                columns: new[] { "status", "payment_deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_booking_id",
                table: "idempotency_records",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_customer_id_operation_key",
                table: "idempotency_records",
                columns: new[] { "customer_id", "operation", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_booking_id",
                table: "payments",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_qr_upload_id",
                table: "payments",
                column: "qr_upload_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_quotes");

            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_courts_id_venue_id",
                table: "courts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_court_allocations_id_court_id",
                table: "court_allocations");
        }
    }
}
