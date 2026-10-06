using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F02VenueContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact",
                table: "venues",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("""
                UPDATE venues AS venue SET contact = business.contact
                FROM businesses AS business WHERE venue.business_id = business.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contact",
                table: "venues");
        }
    }
}
