using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolymorphicUserRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "userable_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "userable_type",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_userable_type_userable_id",
                table: "users",
                columns: new[] { "userable_type", "userable_id" });

            migrationBuilder.Sql("UPDATE users SET userable_type = 'Landlord', userable_id = landlords.id FROM landlords WHERE users.id = landlords.user_id;");
            migrationBuilder.Sql("UPDATE users SET userable_type = 'Tenant', userable_id = tenants.id FROM tenants WHERE users.id = tenants.user_id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_userable_type_userable_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "userable_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "userable_type",
                table: "users");
        }
    }
}
