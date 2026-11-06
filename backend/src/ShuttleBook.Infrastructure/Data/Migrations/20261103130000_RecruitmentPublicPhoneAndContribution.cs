using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShuttleBook.Infrastructure.Data.Migrations;

[DbContext(typeof(ShuttleBookDbContext))]
[Migration("20261103130000_RecruitmentPublicPhoneAndContribution")]
public sealed class RecruitmentPublicPhoneAndContribution : Migration
{
    private const string Valid = "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED','NEWBIE','Y_MINUS','Y','Y_PLUS','TBY_MINUS','TBY','TBY_PLUS','TB_MINUS','TB','TB_PLUS','TBK_MINUS','TBK','TBK_PLUS') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0 AND char_length(caption) <= 1000 AND char_length(contact_name) <= 100 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20) AND NOT (publish_contact_phone AND share_phone_with_accepted) AND (NOT (publish_contact_phone OR share_phone_with_accepted) OR contact_phone IS NOT NULL) AND contribution_mode IN ('UNSPECIFIED','COMMON','BY_GENDER') AND ((contribution_mode = 'UNSPECIFIED' AND common_contribution_vnd IS NULL AND male_contribution_vnd IS NULL AND female_contribution_vnd IS NULL) OR (contribution_mode = 'COMMON' AND common_contribution_vnd IS NOT NULL AND common_contribution_vnd BETWEEN 0 AND 2000000 AND male_contribution_vnd IS NULL AND female_contribution_vnd IS NULL) OR (contribution_mode = 'BY_GENDER' AND common_contribution_vnd IS NULL AND male_contribution_vnd IS NOT NULL AND male_contribution_vnd BETWEEN 0 AND 2000000 AND female_contribution_vnd IS NOT NULL AND male_contribution_vnd BETWEEN 0 AND 2000000))";
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("publish_contact_phone", "recruitment_posts", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("contribution_mode", "recruitment_posts", type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "UNSPECIFIED");
        migrationBuilder.AddColumn<int>("common_contribution_vnd", "recruitment_posts", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>("male_contribution_vnd", "recruitment_posts", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>("female_contribution_vnd", "recruitment_posts", type: "integer", nullable: true);
        migrationBuilder.DropCheckConstraint("ck_recruitment_post_valid", "recruitment_posts");
        migrationBuilder.AddCheckConstraint("ck_recruitment_post_valid", "recruitment_posts", Valid);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM recruitment_posts WHERE publish_contact_phone OR contribution_mode <> 'UNSPECIFIED'
                OR common_contribution_vnd IS NOT NULL OR male_contribution_vnd IS NOT NULL OR female_contribution_vnd IS NOT NULL) THEN
                RAISE EXCEPTION 'Cannot downgrade recruitment phone/contribution data without losing data';
              END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint("ck_recruitment_post_valid", "recruitment_posts");
        migrationBuilder.DropColumn("publish_contact_phone", "recruitment_posts");
        migrationBuilder.DropColumn("contribution_mode", "recruitment_posts");
        migrationBuilder.DropColumn("common_contribution_vnd", "recruitment_posts");
        migrationBuilder.DropColumn("male_contribution_vnd", "recruitment_posts");
        migrationBuilder.DropColumn("female_contribution_vnd", "recruitment_posts");
        migrationBuilder.AddCheckConstraint("ck_recruitment_post_valid", "recruitment_posts",
            "slots_needed BETWEEN 1 AND 8 AND level IN ('ANY','BEGINNER','INTERMEDIATE','ADVANCED','NEWBIE','Y_MINUS','Y','Y_PLUS','TBY_MINUS','TBY','TBY_PLUS','TB_MINUS','TB','TB_PLUS','TBK_MINUS','TBK','TBK_PLUS') AND status IN ('OPEN','CLOSED','HIDDEN') AND version > 0 AND char_length(caption) <= 1000 AND char_length(contact_name) <= 100 AND (contact_phone IS NULL OR char_length(contact_phone) <= 20)");
    }
}
