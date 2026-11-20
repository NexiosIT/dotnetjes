using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCharger",
                table: "Resources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Resources",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCharger",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Resources");
        }
    }
}
