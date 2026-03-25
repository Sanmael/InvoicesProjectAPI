using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoicesProjectInfra.Migrations
{
    /// <inheritdoc />
    public partial class UseDateOnlyForDebtAndReceivableDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Receivables\" ALTER COLUMN \"ExpectedDate\" TYPE date USING \"ExpectedDate\"::date;");
            migrationBuilder.Sql("ALTER TABLE \"Debts\" ALTER COLUMN \"DueDate\" TYPE date USING \"DueDate\"::date;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Receivables\" ALTER COLUMN \"ExpectedDate\" TYPE timestamp with time zone USING (\"ExpectedDate\"::timestamp AT TIME ZONE 'UTC');");
            migrationBuilder.Sql("ALTER TABLE \"Debts\" ALTER COLUMN \"DueDate\" TYPE timestamp with time zone USING (\"DueDate\"::timestamp AT TIME ZONE 'UTC');");
        }
    }
}
