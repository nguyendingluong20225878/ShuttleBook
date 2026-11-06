using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShuttleBook.Infrastructure.Data.Migrations;

[DbContext(typeof(ShuttleBookDbContext))]
[Migration("20261103100000_RecruitmentPlayerNotesAndLevels")]
public sealed class RecruitmentPlayerNotesAndLevels : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("message", "recruitment_requests",
            type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.DropCheckConstraint("ck_recruitment_post_valid", "recruitment_posts");
        migrationBuilder.AddCheckConstraint("ck_recruitment_post_valid", "recruitment_posts",
            "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED','NEWBIE','Y_MINUS','Y','Y_PLUS','TBY_MINUS','TBY','TBY_PLUS','TB_MINUS','TB','TB_PLUS','TBK_MINUS','TBK','TBK_PLUS') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0 AND char_length(caption) <= 1000 AND char_length(contact_name) <= 100 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20)");
        migrationBuilder.DropCheckConstraint("ck_recruitment_request_valid", "recruitment_requests");
        migrationBuilder.AddCheckConstraint("ck_recruitment_request_valid", "recruitment_requests",
            "level IN ('BEGINNER','INTERMEDIATE','ADVANCED','UNSPECIFIED') AND status IN ('PENDING','ACCEPTED','DECLINED','WITHDRAWN') AND version > 0 AND char_length(message) <= 500 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20) AND (share_phone_with_accepted = false OR contact_phone IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM recruitment_posts WHERE level NOT IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED'))
                OR EXISTS (SELECT 1 FROM recruitment_requests WHERE level = 'UNSPECIFIED' OR message <> '') THEN
                RAISE EXCEPTION 'Cannot downgrade recruitment levels or notes without losing data';
              END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint("ck_recruitment_post_valid", "recruitment_posts");
        migrationBuilder.AddCheckConstraint("ck_recruitment_post_valid", "recruitment_posts",
            "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0 AND char_length(caption) <= 1000 AND char_length(contact_name) <= 100 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20)");
        migrationBuilder.DropCheckConstraint("ck_recruitment_request_valid", "recruitment_requests");
        migrationBuilder.DropColumn("message", "recruitment_requests");
        migrationBuilder.AddCheckConstraint("ck_recruitment_request_valid", "recruitment_requests",
            "level IN ('BEGINNER','INTERMEDIATE','ADVANCED') AND status IN ('PENDING','ACCEPTED','DECLINED','WITHDRAWN') AND version > 0 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20) AND (share_phone_with_accepted = false OR contact_phone IS NOT NULL)");
    }
}
