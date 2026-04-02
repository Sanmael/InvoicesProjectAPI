using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesProjectInfra.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppLinked",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppPhoneNumber",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_WhatsAppPhoneNumber",
                table: "Users",
                column: "WhatsAppPhoneNumber",
                unique: true,
                filter: "\"WhatsAppPhoneNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_WhatsAppPhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WhatsAppLinked",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WhatsAppPhoneNumber",
                table: "Users");
        }
    }
}
