using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlanIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_preferences_users_UserId",
                table: "notification_preferences");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "subscription_plans",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "subscription_plans",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "MaxPropertiesLimit",
                table: "subscription_plans",
                newName: "max_properties_limit");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "notification_preferences",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "notification_preferences",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "ReceiveSmsAlerts",
                table: "notification_preferences",
                newName: "receive_sms_alerts");

            migrationBuilder.RenameColumn(
                name: "ReceiveEmailAlerts",
                table: "notification_preferences",
                newName: "receive_email_alerts");

            migrationBuilder.RenameIndex(
                name: "IX_notification_preferences_UserId",
                table: "notification_preferences",
                newName: "IX_notification_preferences_user_id");

            migrationBuilder.AddColumn<long>(
                name: "subscription_plan_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id");

            migrationBuilder.AddForeignKey(
                name: "FK_notification_preferences_users_user_id",
                table: "notification_preferences",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_subscription_plans_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_preferences_users_user_id",
                table: "notification_preferences");

            migrationBuilder.DropForeignKey(
                name: "FK_users_subscription_plans_subscription_plan_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_subscription_plan_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "subscription_plan_id",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "subscription_plans",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "subscription_plans",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "max_properties_limit",
                table: "subscription_plans",
                newName: "MaxPropertiesLimit");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "notification_preferences",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "notification_preferences",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "receive_sms_alerts",
                table: "notification_preferences",
                newName: "ReceiveSmsAlerts");

            migrationBuilder.RenameColumn(
                name: "receive_email_alerts",
                table: "notification_preferences",
                newName: "ReceiveEmailAlerts");

            migrationBuilder.RenameIndex(
                name: "IX_notification_preferences_user_id",
                table: "notification_preferences",
                newName: "IX_notification_preferences_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_notification_preferences_users_UserId",
                table: "notification_preferences",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
