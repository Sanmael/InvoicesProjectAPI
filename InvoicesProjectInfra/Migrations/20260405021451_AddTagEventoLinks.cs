using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesProjectInfra.Migrations
{
    /// <inheritdoc />
    public partial class AddTagEventoLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TagEventoId",
                table: "Receivables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TagEventoId",
                table: "Debts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TagEventoId",
                table: "CardPurchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TagEventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataFim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagEventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagEventos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receivables_TagEventoId",
                table: "Receivables",
                column: "TagEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_TagEventoId",
                table: "Debts",
                column: "TagEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_CardPurchases_TagEventoId",
                table: "CardPurchases",
                column: "TagEventoId");

            migrationBuilder.CreateIndex(
                name: "IX_TagEventos_UserId",
                table: "TagEventos",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CardPurchases_TagEventos_TagEventoId",
                table: "CardPurchases",
                column: "TagEventoId",
                principalTable: "TagEventos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Debts_TagEventos_TagEventoId",
                table: "Debts",
                column: "TagEventoId",
                principalTable: "TagEventos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Receivables_TagEventos_TagEventoId",
                table: "Receivables",
                column: "TagEventoId",
                principalTable: "TagEventos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardPurchases_TagEventos_TagEventoId",
                table: "CardPurchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Debts_TagEventos_TagEventoId",
                table: "Debts");

            migrationBuilder.DropForeignKey(
                name: "FK_Receivables_TagEventos_TagEventoId",
                table: "Receivables");

            migrationBuilder.DropTable(
                name: "TagEventos");

            migrationBuilder.DropIndex(
                name: "IX_Receivables_TagEventoId",
                table: "Receivables");

            migrationBuilder.DropIndex(
                name: "IX_Debts_TagEventoId",
                table: "Debts");

            migrationBuilder.DropIndex(
                name: "IX_CardPurchases_TagEventoId",
                table: "CardPurchases");

            migrationBuilder.DropColumn(
                name: "TagEventoId",
                table: "Receivables");

            migrationBuilder.DropColumn(
                name: "TagEventoId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "TagEventoId",
                table: "CardPurchases");
        }
    }
}
