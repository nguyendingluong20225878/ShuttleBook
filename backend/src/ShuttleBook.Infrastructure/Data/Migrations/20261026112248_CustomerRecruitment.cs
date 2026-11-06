using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRecruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_bookings_id_customer_id",
                table: "bookings",
                columns: new[] { "id", "customer_id" });

            migrationBuilder.CreateTable(
                name: "recruitment_posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slots_needed = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_posts", x => x.id);
                    table.CheckConstraint("ck_recruitment_post_valid", "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0");
                    table.ForeignKey(
                        name: "FK_recruitment_posts_bookings_booking_id_owner_user_id",
                        columns: x => new { x.booking_id, x.owner_user_id },
                        principalTable: "bookings",
                        principalColumns: new[] { "id", "customer_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recruitment_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_reports", x => x.id);
                    table.CheckConstraint("ck_recruitment_report_valid", "reason IN ('SPAM','MISLEADING','UNSAFE') AND status IN ('OPEN','DISMISSED','RESOLVED') AND ((status='OPEN' AND resolved_at IS NULL AND resolved_by IS NULL) OR (status<>'OPEN' AND resolved_at IS NOT NULL AND resolved_by IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_recruitment_reports_recruitment_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "recruitment_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recruitment_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recruitment_reports_users_resolved_by",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recruitment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recruitment_requests", x => x.id);
                    table.CheckConstraint("ck_recruitment_request_valid", "level IN ('BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('PENDING','ACCEPTED','DECLINED','WITHDRAWN') AND version > 0");
                    table.ForeignKey(
                        name: "FK_recruitment_requests_recruitment_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "recruitment_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recruitment_requests_users_applicant_user_id",
                        column: x => x.applicant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_posts_booking_id",
                table: "recruitment_posts",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_posts_booking_id_owner_user_id",
                table: "recruitment_posts",
                columns: new[] { "booking_id", "owner_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_posts_owner_user_id_status",
                table: "recruitment_posts",
                columns: new[] { "owner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_posts_status",
                table: "recruitment_posts",
                column: "status",
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_reports_post_id_reporter_user_id",
                table: "recruitment_reports",
                columns: new[] { "post_id", "reporter_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_reports_reporter_user_id",
                table: "recruitment_reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_reports_resolved_by",
                table: "recruitment_reports",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_reports_status_created_at",
                table: "recruitment_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_requests_applicant_user_id_id",
                table: "recruitment_requests",
                columns: new[] { "applicant_user_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_requests_post_id_applicant_user_id",
                table: "recruitment_requests",
                columns: new[] { "post_id", "applicant_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recruitment_requests_post_id_status",
                table: "recruitment_requests",
                columns: new[] { "post_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recruitment_reports");

            migrationBuilder.DropTable(
                name: "recruitment_requests");

            migrationBuilder.DropTable(
                name: "recruitment_posts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_bookings_id_customer_id",
                table: "bookings");
        }
    }
}
