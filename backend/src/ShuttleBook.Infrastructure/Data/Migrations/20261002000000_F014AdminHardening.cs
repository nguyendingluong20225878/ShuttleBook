using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShuttleBook.Infrastructure.Data.Migrations;

[DbContext(typeof(ShuttleBookDbContext))]
[Migration("20261002000000_F014AdminHardening")]
public sealed class F014AdminHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE UNIQUE INDEX ux_users_single_admin ON users ((1)) WHERE account_type = 'Admin'");
        migrationBuilder.Sql("""
            ALTER TABLE users ADD CONSTRAINT ck_users_admin_verified_active
            CHECK (account_type <> 'Admin' OR
              (status IN ('Active', 'Suspended') AND
                ((normalized_email IS NOT NULL AND email_verified_at IS NOT NULL) OR
                 (normalized_phone IS NOT NULL AND phone_verified_at IS NOT NULL))))
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE users DROP CONSTRAINT ck_users_admin_verified_active");
        migrationBuilder.Sql("DROP INDEX ux_users_single_admin");
    }
}
