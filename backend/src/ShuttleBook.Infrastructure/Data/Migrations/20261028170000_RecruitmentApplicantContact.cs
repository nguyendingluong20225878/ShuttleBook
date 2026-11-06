using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShuttleBook.Infrastructure.Data.Migrations;

[DbContext(typeof(ShuttleBookDbContext))]
[Migration("20261028170000_RecruitmentApplicantContact")]
public sealed class RecruitmentApplicantContact : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("contact_phone", "recruitment_requests",
            type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<bool>("share_phone_with_accepted", "recruitment_requests",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.DropCheckConstraint("ck_recruitment_request_valid", "recruitment_requests");
        migrationBuilder.AddCheckConstraint("ck_recruitment_request_valid", "recruitment_requests",
            "level IN ('BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('PENDING','ACCEPTED','DECLINED','WITHDRAWN') AND version > 0 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20) AND (share_phone_with_accepted = false OR contact_phone IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_recruitment_request_valid", "recruitment_requests");
        migrationBuilder.DropColumn("contact_phone", "recruitment_requests");
        migrationBuilder.DropColumn("share_phone_with_accepted", "recruitment_requests");
        migrationBuilder.AddCheckConstraint("ck_recruitment_request_valid", "recruitment_requests",
            "level IN ('BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('PENDING','ACCEPTED','DECLINED','WITHDRAWN') AND version > 0");
    }
}
