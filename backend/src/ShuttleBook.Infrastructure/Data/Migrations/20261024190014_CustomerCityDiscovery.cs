using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerCityDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city_code",
                table: "venues",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_venues_city_code_status_name_id",
                table: "venues",
                columns: new[] { "city_code", "status", "name", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_venues_city_code",
                table: "venues",
                sql: "city_code IS NULL OR city_code IN ('HN', 'HCM')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_venues_city_code_status_name_id",
                table: "venues");

            migrationBuilder.DropCheckConstraint(
                name: "ck_venues_city_code",
                table: "venues");

            migrationBuilder.DropColumn(
                name: "city_code",
                table: "venues");
        }
    }
}
