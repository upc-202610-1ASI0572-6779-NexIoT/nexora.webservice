using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These plans may already exist from AddSubscriptionSystem migration;
            // use ON CONFLICT DO NOTHING to avoid duplicate key errors.
            migrationBuilder.Sql(@"
                INSERT INTO subscription_plans (id, name, monthly_price, max_properties_limit, unlimited_properties, is_active)
                VALUES (1, 'Basic', 32.12, 2, FALSE, TRUE)
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO subscription_plans (id, name, monthly_price, max_properties_limit, unlimited_properties, is_active)
                VALUES (2, 'Professional', 44.2, 0, TRUE, TRUE)
                ON CONFLICT (id) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "subscription_plans",
                keyColumn: "id",
                keyValues: new object[] { 1L, 2L });
        }
    }
}
