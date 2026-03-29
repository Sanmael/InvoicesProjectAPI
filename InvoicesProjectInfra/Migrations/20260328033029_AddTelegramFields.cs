using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesProjectInfra.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "NotificationPreferences",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramLinkToken",
                table: "NotificationPreferences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TelegramNotificationsEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                table: "NotificationPreferences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Debts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Outros");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "CardPurchases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Outros");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TelegramLinkToken",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TelegramNotificationsEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "CardPurchases");
        }
    }
}
