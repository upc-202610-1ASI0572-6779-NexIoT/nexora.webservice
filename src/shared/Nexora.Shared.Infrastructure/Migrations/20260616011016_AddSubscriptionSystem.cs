using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_subscription_plans_subscription_plan_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_subscription_plan_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "subscription_plan_id",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "subscription_plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_price",
                table: "subscription_plans",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "unlimited_properties",
                table: "subscription_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    landlord_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscriptions_landlords_landlord_id",
                        column: x => x.landlord_id,
                        principalTable: "landlords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscriptions_subscription_plans_subscription_plan_id",
                        column: x => x.subscription_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscription_events_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_subscription_id",
                table: "invoices",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_invoice_id",
                table: "payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_events_subscription_id",
                table: "subscription_events",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_landlord_id",
                table: "subscriptions",
                column: "landlord_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_subscription_plan_id",
                table: "subscriptions",
                column: "subscription_plan_id");

            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[] { "id", "name", "monthly_price", "max_properties_limit", "unlimited_properties", "is_active" },
                values: new object[,]
                {
                    { 1L, "Basic", 32.12m, 2, false, true },
                    { 2L, "Professional", 44.20m, 0, true, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "subscription_plans",
                keyColumn: "id",
                keyValues: new object[] { 1L, 2L });

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "subscription_events");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "monthly_price",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "unlimited_properties",
                table: "subscription_plans");

            migrationBuilder.AddColumn<long>(
                name: "subscription_plan_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "subscription_plans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_users_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_subscription_plans_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
