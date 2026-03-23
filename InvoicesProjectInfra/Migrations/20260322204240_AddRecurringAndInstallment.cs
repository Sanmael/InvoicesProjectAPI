using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesProjectInfra.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringAndInstallment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Receivables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurrenceGroupId",
                table: "Receivables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurringDay",
                table: "Receivables",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstallmentGroupId",
                table: "Debts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentNumber",
                table: "Debts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInstallment",
                table: "Debts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TotalInstallments",
                table: "Debts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Receivables");

            migrationBuilder.DropColumn(
                name: "RecurrenceGroupId",
                table: "Receivables");

            migrationBuilder.DropColumn(
                name: "RecurringDay",
                table: "Receivables");

            migrationBuilder.DropColumn(
                name: "InstallmentGroupId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "InstallmentNumber",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "IsInstallment",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "TotalInstallments",
                table: "Debts");
        }
    }
}
