using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F06PaymentConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_idempotency_records_users_customer_id",
                table: "idempotency_records");

            migrationBuilder.RenameColumn(
                name: "customer_id",
                table: "idempotency_records",
                newName: "actor_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_idempotency_records_customer_id_operation_key",
                table: "idempotency_records",
                newName: "IX_idempotency_records_actor_user_id_operation_key");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmation_alerted_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "confirmed_amount",
                table: "payments",
                type: "numeric(18,0)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_reported_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_reported_at",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "booking_id",
                table: "media_uploads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_payments_id_booking_id",
                table: "payments",
                columns: new[] { "id", "booking_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_bookings_id_customer_id_venue_id",
                table: "bookings",
                columns: new[] { "id", "customer_id", "venue_id" });

            migrationBuilder.CreateTable(
                name: "payment_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolution = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    confirmed_amount = table.Column<decimal>(type: "numeric(18,0)", nullable: true),
                    bank_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_decisions", x => x.id);
                    table.CheckConstraint("ck_decision_valid", "(resolution='CONFIRMED' AND confirmed_amount IS NOT NULL AND confirmed_amount >= 0 AND bank_reference IS NOT NULL) OR (resolution IN ('NEEDS_REVIEW','FINAL_REJECTION') AND reason_code IS NOT NULL AND reason IS NOT NULL AND length(btrim(reason))>0)");
                    table.ForeignKey(
                        name: "FK_payment_decisions_payments_payment_id_booking_id",
                        columns: x => new { x.payment_id, x.booking_id },
                        principalTable: "payments",
                        principalColumns: new[] { "id", "booking_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_decisions_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proof_upload_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_evidence", x => x.id);
                    table.CheckConstraint("ck_evidence_kind", "kind IN ('INITIAL','SUPPLEMENT')");
                    table.ForeignKey(
                        name: "FK_payment_evidence_bookings_booking_id_customer_id_venue_id",
                        columns: x => new { x.booking_id, x.customer_id, x.venue_id },
                        principalTable: "bookings",
                        principalColumns: new[] { "id", "customer_id", "venue_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_evidence_media_uploads_proof_upload_id",
                        column: x => x.proof_upload_id,
                        principalTable: "media_uploads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_evidence_payments_payment_id_booking_id",
                        columns: x => new { x.payment_id, x.booking_id },
                        principalTable: "payments",
                        principalColumns: new[] { "id", "booking_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_confirmed_by",
                table: "payments",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "IX_payments_first_reported_at_confirmation_alerted_at",
                table: "payments",
                columns: new[] { "first_reported_at", "confirmation_alerted_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_confirmation",
                table: "payments",
                sql: "status <> 'PAID' OR (confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL AND confirmed_amount IS NOT NULL AND confirmed_amount >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_booking_id_owner_user_id_venue_id",
                table: "media_uploads",
                columns: new[] { "booking_id", "owner_user_id", "venue_id" });

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_id_booking_id_owner_user_id_venue_id",
                table: "media_uploads",
                columns: new[] { "id", "booking_id", "owner_user_id", "venue_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads",
                sql: "(purpose='PAYMENT_PROOF' AND booking_id IS NOT NULL) OR (purpose IN ('QR','VENUE_IMAGE') AND booking_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_venue_id_status_local_date_id",
                table: "bookings",
                columns: new[] { "venue_id", "status", "local_date", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_decisions_actor_user_id",
                table: "payment_decisions",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_decisions_payment_id_booking_id",
                table: "payment_decisions",
                columns: new[] { "payment_id", "booking_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_decisions_payment_id_decided_at_id",
                table: "payment_decisions",
                columns: new[] { "payment_id", "decided_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_evidence_booking_id_customer_id_venue_id",
                table: "payment_evidence",
                columns: new[] { "booking_id", "customer_id", "venue_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_evidence_payment_id_booking_id",
                table: "payment_evidence",
                columns: new[] { "payment_id", "booking_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_evidence_payment_id_reported_at_id",
                table: "payment_evidence",
                columns: new[] { "payment_id", "reported_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_evidence_proof_upload_id",
                table: "payment_evidence",
                column: "proof_upload_id",
                unique: true,
                filter: "proof_upload_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_idempotency_records_users_actor_user_id",
                table: "idempotency_records",
                column: "actor_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_media_uploads_bookings_booking_id_owner_user_id_venue_id",
                table: "media_uploads",
                columns: new[] { "booking_id", "owner_user_id", "venue_id" },
                principalTable: "bookings",
                principalColumns: new[] { "id", "customer_id", "venue_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_confirmed_by",
                table: "payments",
                column: "confirmed_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // EF alternate keys cannot be nullable, while legacy QR/venue uploads have no booking.
            // PostgreSQL's nullable unique index permits the scoped composite proof attachment FK.
            migrationBuilder.Sql("""
                ALTER TABLE payment_evidence ADD CONSTRAINT fk_evidence_proof_scope
                  FOREIGN KEY (proof_upload_id, booking_id, customer_id, venue_id)
                  REFERENCES media_uploads (id, booking_id, owner_user_id, venue_id) ON DELETE RESTRICT;
                CREATE FUNCTION f06_evidence_ready() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.proof_upload_id IS NOT NULL AND NOT EXISTS (
                    SELECT 1 FROM media_uploads WHERE id=NEW.proof_upload_id
                      AND purpose='PAYMENT_PROOF' AND status='READY') THEN
                    RAISE EXCEPTION 'Proof must be a verified payment upload' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER f06_evidence_ready BEFORE INSERT OR UPDATE ON payment_evidence
                  FOR EACH ROW EXECUTE FUNCTION f06_evidence_ready();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER f06_evidence_ready ON payment_evidence; DROP FUNCTION f06_evidence_ready();");
            migrationBuilder.DropForeignKey(
                name: "FK_idempotency_records_users_actor_user_id",
                table: "idempotency_records");

            migrationBuilder.DropForeignKey(
                name: "FK_media_uploads_bookings_booking_id_owner_user_id_venue_id",
                table: "media_uploads");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_confirmed_by",
                table: "payments");

            migrationBuilder.DropTable(
                name: "payment_decisions");

            migrationBuilder.DropTable(
                name: "payment_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_payments_id_booking_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_confirmed_by",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_first_reported_at_confirmation_alerted_at",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_confirmation",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_media_uploads_booking_id_owner_user_id_venue_id",
                table: "media_uploads");

            migrationBuilder.DropIndex(
                name: "IX_media_uploads_id_booking_id_owner_user_id_venue_id",
                table: "media_uploads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_booking_purpose",
                table: "media_uploads");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_bookings_id_customer_id_venue_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_venue_id_status_local_date_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "confirmation_alerted_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmed_amount",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "confirmed_by",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "first_reported_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "last_reported_at",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "booking_id",
                table: "media_uploads");

            migrationBuilder.RenameColumn(
                name: "actor_user_id",
                table: "idempotency_records",
                newName: "customer_id");

            migrationBuilder.RenameIndex(
                name: "IX_idempotency_records_actor_user_id_operation_key",
                table: "idempotency_records",
                newName: "IX_idempotency_records_customer_id_operation_key");

            migrationBuilder.AddForeignKey(
                name: "FK_idempotency_records_users_customer_id",
                table: "idempotency_records",
                column: "customer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
