using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMacAddressToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Column already exists in database relation
            /*
            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
            */

            migrationBuilder.CreateIndex(
                name: "IX_devices_MacAddress",
                table: "devices",
                column: "MacAddress",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_MacAddress",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "devices");
        }
    }
}
