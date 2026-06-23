using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Migrations
{
    public partial class MakePropertyCodeNotNullAndUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill: assign sequential codes per property type for existing NULL property_code rows
            // This ensures the migration works even when applied via CLI (not just through Program.cs)
            foreach (var type in new[] { "HOUSE", "APARTMENT", "ROOM", "OFFICE", "COMMERCIAL" })
            {
                var prefix = type switch
                {
                    "HOUSE" => "HSE-",
                    "APARTMENT" => "APT-",
                    "ROOM" => "ROM-",
                    "OFFICE" => "OFC-",
                    "COMMERCIAL" => "COM-",
                    _ => "PRP-"
                };

                migrationBuilder.Sql($@"
                    UPDATE properties
                    SET property_code = '{prefix}' || LPAD(seq.num::text, 3, '0')
                    FROM (
                        SELECT id, ROW_NUMBER() OVER (ORDER BY id) AS num
                        FROM properties p2
                        WHERE p2.property_code IS NULL
                          AND p2.property_type = '{type}'
                    ) seq
                    WHERE properties.id = seq.id
                      AND properties.property_code IS NULL;
                ");
            }

            // Now safe to make NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "property_code",
                table: "properties",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            // Add unique index
            migrationBuilder.CreateIndex(
                name: "IX_properties_property_code",
                table: "properties",
                column: "property_code",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_properties_property_code",
                table: "properties");

            migrationBuilder.AlterColumn<string>(
                name: "property_code",
                table: "properties",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);
        }
    }
}
