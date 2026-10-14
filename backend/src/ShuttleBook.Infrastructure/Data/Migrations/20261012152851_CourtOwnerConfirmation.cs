using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CourtOwnerConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The original constraint was created with SQL and is absent from the prior EF model.
            migrationBuilder.Sql("""
                ALTER TABLE approval_requests DROP CONSTRAINT ck_approval_requests_status;
                ALTER TABLE approval_requests ADD CONSTRAINT ck_approval_requests_status
                    CHECK (status IN ('PENDING','APPROVED','CHANGES_REQUESTED','SUPERSEDED'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM approval_requests WHERE status='SUPERSEDED') THEN
                        RAISE EXCEPTION 'Cannot downgrade court confirmation while SUPERSEDED approval history exists';
                    END IF;
                END $$;
                ALTER TABLE approval_requests DROP CONSTRAINT ck_approval_requests_status;
                ALTER TABLE approval_requests ADD CONSTRAINT ck_approval_requests_status
                    CHECK (status IN ('PENDING','APPROVED','CHANGES_REQUESTED'));
                """);
        }
    }
}
