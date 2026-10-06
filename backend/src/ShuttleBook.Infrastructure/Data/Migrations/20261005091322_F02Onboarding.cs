using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F02Onboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_requests_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_requests_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_requests_users_submitted_by",
                        column: x => x.submitted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "business_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_business_memberships_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_business_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    timezone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    image_upload_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.id);
                    table.ForeignKey(
                        name: "FK_venues_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courts", x => x.id);
                    table.ForeignKey(
                        name: "FK_courts_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "media_uploads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256_base64 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_uploads", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_uploads_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_media_uploads_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "venue_payment_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    account_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    qr_upload_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_payment_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_venue_payment_accounts_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "court_operating_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_court_operating_hours", x => x.id);
                    table.ForeignKey(
                        name: "FK_court_operating_hours_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ends_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    price_per_slot = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_pricing_rules_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_business_id_status",
                table: "approval_requests",
                columns: new[] { "business_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_reviewed_by",
                table: "approval_requests",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_submitted_by",
                table: "approval_requests",
                column: "submitted_by");

            migrationBuilder.CreateIndex(
                name: "IX_business_memberships_business_id_user_id",
                table: "business_memberships",
                columns: new[] { "business_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_memberships_user_id_role_status",
                table: "business_memberships",
                columns: new[] { "user_id", "role", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_court_operating_hours_court_id_day_of_week",
                table: "court_operating_hours",
                columns: new[] { "court_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courts_venue_id_name",
                table: "courts",
                columns: new[] { "venue_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_object_key",
                table: "media_uploads",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_owner_user_id",
                table: "media_uploads",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_uploads_venue_id",
                table: "media_uploads",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_court_id_day_of_week_starts_at_ends_at",
                table: "pricing_rules",
                columns: new[] { "court_id", "day_of_week", "starts_at", "ends_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venue_payment_accounts_venue_id",
                table: "venue_payment_accounts",
                column: "venue_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venues_business_id",
                table: "venues",
                column: "business_id");

            migrationBuilder.Sql("""
                ALTER TABLE venues ADD COLUMN location geography(Point,4326)
                GENERATED ALWAYS AS (ST_SetSRID(ST_MakePoint(longitude, latitude),4326)::geography) STORED;
                CREATE INDEX ix_venues_location ON venues USING gist (location);
                ALTER TABLE venues ADD CONSTRAINT ck_venues_coordinates
                  CHECK (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180);
                ALTER TABLE businesses ADD CONSTRAINT ck_businesses_status
                  CHECK (status IN ('DRAFT','PENDING_APPROVAL','ACTIVE','SUSPENDED'));
                ALTER TABLE venues ADD CONSTRAINT ck_venues_status
                  CHECK (status IN ('DRAFT','PENDING_APPROVAL','PUBLISHED','SUSPENDED'));
                ALTER TABLE courts ADD CONSTRAINT ck_courts_status
                  CHECK (status IN ('INACTIVE','ACTIVE','SUSPENDED'));
                ALTER TABLE business_memberships ADD CONSTRAINT ck_business_memberships_role_status
                  CHECK (role = 'OWNER' AND status IN ('PENDING','ACTIVE','REVOKED'));
                ALTER TABLE approval_requests ADD CONSTRAINT ck_approval_requests_status
                  CHECK (status IN ('PENDING','APPROVED','CHANGES_REQUESTED'));
                ALTER TABLE media_uploads ADD CONSTRAINT ck_media_uploads_size_status
                  CHECK (size_bytes > 0 AND size_bytes <= 5242880 AND status IN ('PENDING','READY'));
                ALTER TABLE court_operating_hours ADD CONSTRAINT ck_court_operating_hours
                  CHECK (day_of_week BETWEEN 0 AND 6 AND opens_at < closes_at AND
                         EXTRACT(MINUTE FROM opens_at) IN (0,30) AND EXTRACT(MINUTE FROM closes_at) IN (0,30));
                ALTER TABLE pricing_rules ADD CONSTRAINT ck_pricing_rules
                  CHECK (day_of_week BETWEEN 0 AND 6 AND starts_at < ends_at AND price_per_slot > 0 AND
                         EXTRACT(MINUTE FROM starts_at) IN (0,30) AND EXTRACT(MINUTE FROM ends_at) IN (0,30));
                CREATE UNIQUE INDEX ux_approval_requests_one_pending ON approval_requests (business_id) WHERE status = 'PENDING';
                CREATE UNIQUE INDEX ux_business_memberships_one_pending_owner ON business_memberships (user_id)
                  WHERE role = 'OWNER' AND status = 'PENDING';
                ALTER TABLE venue_payment_accounts ADD CONSTRAINT fk_payment_account_qr_upload
                  FOREIGN KEY (qr_upload_id) REFERENCES media_uploads(id) ON DELETE RESTRICT;
                ALTER TABLE venues ADD CONSTRAINT fk_venue_image_upload
                  FOREIGN KEY (image_upload_id) REFERENCES media_uploads(id) ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE venues DROP CONSTRAINT fk_venue_image_upload;
                ALTER TABLE venue_payment_accounts DROP CONSTRAINT fk_payment_account_qr_upload;
                """);

            migrationBuilder.DropTable(
                name: "approval_requests");

            migrationBuilder.DropTable(
                name: "business_memberships");

            migrationBuilder.DropTable(
                name: "court_operating_hours");

            migrationBuilder.DropTable(
                name: "media_uploads");

            migrationBuilder.DropTable(
                name: "pricing_rules");

            migrationBuilder.DropTable(
                name: "venue_payment_accounts");

            migrationBuilder.DropTable(
                name: "courts");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropTable(
                name: "businesses");
        }
    }
}
