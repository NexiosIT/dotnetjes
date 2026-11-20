using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "People",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_ExternalId",
                table: "People",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_People_ExternalId",
                table: "People");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "People");
        }
    }
}
