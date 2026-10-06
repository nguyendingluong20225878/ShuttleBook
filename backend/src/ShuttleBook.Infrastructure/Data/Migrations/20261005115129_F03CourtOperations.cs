using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F03CourtOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pricing_rules_court_id_day_of_week_starts_at_ends_at",
                table: "pricing_rules");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ends_on",
                table: "pricing_rules",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(9999, 12, 31));

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "pricing_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "starts_on",
                table: "pricing_rules",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "courts",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "court_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_court_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_court_allocations_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "court_maintenance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    court_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_court_maintenance", x => x.id);
                    table.ForeignKey(
                        name: "FK_court_maintenance_court_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalTable: "court_allocations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_court_maintenance_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_court_maintenance_users_cancelled_by",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_court_maintenance_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_court_id_day_of_week_starts_on_ends_on_starts~",
                table: "pricing_rules",
                columns: new[] { "court_id", "day_of_week", "starts_on", "ends_on", "starts_at", "ends_at", "priority" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_court_allocations_court_id_starts_at_ends_at",
                table: "court_allocations",
                columns: new[] { "court_id", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "IX_court_maintenance_allocation_id",
                table: "court_maintenance",
                column: "allocation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_court_maintenance_cancelled_by",
                table: "court_maintenance",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_court_maintenance_court_id_status_created_at",
                table: "court_maintenance",
                columns: new[] { "court_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_court_maintenance_created_by",
                table: "court_maintenance",
                column: "created_by");

            migrationBuilder.Sql("""
                ALTER TABLE courts ADD CONSTRAINT ck_courts_version_positive CHECK (version > 0);
                ALTER TABLE pricing_rules ADD CONSTRAINT ck_f03_pricing_rule_valid CHECK (
                    starts_on <= ends_on AND day_of_week BETWEEN 0 AND 6 AND priority >= 0 AND
                    price_per_slot > 0 AND starts_at < ends_at AND
                    EXTRACT(MINUTE FROM starts_at) IN (0, 30) AND EXTRACT(SECOND FROM starts_at) = 0 AND
                    EXTRACT(MINUTE FROM ends_at) IN (0, 30) AND EXTRACT(SECOND FROM ends_at) = 0);
                ALTER TABLE pricing_rules ADD CONSTRAINT ex_f03_pricing_same_priority EXCLUDE USING gist (
                    court_id WITH =, day_of_week WITH =,
                    (daterange(starts_on, ends_on, '[]')) WITH &&,
                    (int4range(EXTRACT(EPOCH FROM starts_at)::integer,
                               EXTRACT(EPOCH FROM ends_at)::integer, '[)')) WITH &&,
                    priority WITH =);
                ALTER TABLE court_allocations ADD CONSTRAINT ck_f03_allocation_valid CHECK (
                    ends_at > starts_at AND kind IN ('MAINTENANCE', 'BOOKING') AND
                    status IN ('RESERVED', 'RELEASED'));
                ALTER TABLE court_allocations ADD CONSTRAINT ex_f03_allocation_no_overlap EXCLUDE USING gist (
                    court_id WITH =, (tstzrange(starts_at, ends_at, '[)')) WITH &&)
                    WHERE (status = 'RESERVED');
                ALTER TABLE court_maintenance ADD CONSTRAINT ck_f03_maintenance_valid CHECK (
                    status IN ('ACTIVE', 'CANCELLED') AND length(trim(reason)) BETWEEN 5 AND 500);
                CREATE UNIQUE INDEX ux_f03_allocation_id_court ON court_allocations (id, court_id);
                ALTER TABLE court_maintenance ADD CONSTRAINT fk_f03_maintenance_allocation_court
                    FOREIGN KEY (allocation_id, court_id) REFERENCES court_allocations (id, court_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE pricing_rules DROP CONSTRAINT ex_f03_pricing_same_priority;");
            migrationBuilder.Sql("ALTER TABLE pricing_rules DROP CONSTRAINT ck_f03_pricing_rule_valid;");
            migrationBuilder.Sql("ALTER TABLE courts DROP CONSTRAINT ck_courts_version_positive;");
            migrationBuilder.DropTable(
                name: "court_maintenance");

            migrationBuilder.DropTable(
                name: "court_allocations");

            migrationBuilder.DropIndex(
                name: "IX_pricing_rules_court_id_day_of_week_starts_on_ends_on_starts~",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "ends_on",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "starts_on",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "version",
                table: "courts");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_court_id_day_of_week_starts_at_ends_at",
                table: "pricing_rules",
                columns: new[] { "court_id", "day_of_week", "starts_at", "ends_at" },
                unique: true);
        }
    }
}
