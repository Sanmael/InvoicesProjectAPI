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
            migrationBuilder.Sql("ALTER TABLE \"Receivables\" ADD COLUMN IF NOT EXISTS \"TagEventoId\" uuid;");
            migrationBuilder.Sql("ALTER TABLE \"Debts\" ADD COLUMN IF NOT EXISTS \"TagEventoId\" uuid;");
            migrationBuilder.Sql("ALTER TABLE \"CardPurchases\" ADD COLUMN IF NOT EXISTS \"TagEventoId\" uuid;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""TagEventos"" (
    ""Id"" uuid NOT NULL,
    ""Nome"" character varying(200) NOT NULL,
    ""Descricao"" character varying(1000),
    ""DataInicio"" timestamp with time zone,
    ""DataFim"" timestamp with time zone,
    ""UserId"" uuid NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone,
    CONSTRAINT ""PK_TagEventos"" PRIMARY KEY (""Id"")
);
");

            migrationBuilder.Sql("ALTER TABLE \"TagEventos\" ADD COLUMN IF NOT EXISTS \"DataInicio\" timestamp with time zone;");
            migrationBuilder.Sql("ALTER TABLE \"TagEventos\" ADD COLUMN IF NOT EXISTS \"DataFim\" timestamp with time zone;");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_TagEventos_Users_UserId'
    ) THEN
        ALTER TABLE ""TagEventos""
        ADD CONSTRAINT ""FK_TagEventos_Users_UserId""
        FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE;
    END IF;
END $$;
");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Receivables_TagEventoId\" ON \"Receivables\" (\"TagEventoId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Debts_TagEventoId\" ON \"Debts\" (\"TagEventoId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_CardPurchases_TagEventoId\" ON \"CardPurchases\" (\"TagEventoId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_TagEventos_UserId\" ON \"TagEventos\" (\"UserId\");");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_CardPurchases_TagEventos_TagEventoId'
    ) THEN
        ALTER TABLE ""CardPurchases""
        ADD CONSTRAINT ""FK_CardPurchases_TagEventos_TagEventoId""
        FOREIGN KEY (""TagEventoId"") REFERENCES ""TagEventos"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Debts_TagEventos_TagEventoId'
    ) THEN
        ALTER TABLE ""Debts""
        ADD CONSTRAINT ""FK_Debts_TagEventos_TagEventoId""
        FOREIGN KEY (""TagEventoId"") REFERENCES ""TagEventos"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Receivables_TagEventos_TagEventoId'
    ) THEN
        ALTER TABLE ""Receivables""
        ADD CONSTRAINT ""FK_Receivables_TagEventos_TagEventoId""
        FOREIGN KEY (""TagEventoId"") REFERENCES ""TagEventos"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"CardPurchases\" DROP CONSTRAINT IF EXISTS \"FK_CardPurchases_TagEventos_TagEventoId\";");
            migrationBuilder.Sql("ALTER TABLE \"Debts\" DROP CONSTRAINT IF EXISTS \"FK_Debts_TagEventos_TagEventoId\";");
            migrationBuilder.Sql("ALTER TABLE \"Receivables\" DROP CONSTRAINT IF EXISTS \"FK_Receivables_TagEventos_TagEventoId\";");

            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Receivables_TagEventoId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Debts_TagEventoId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_CardPurchases_TagEventoId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_TagEventos_UserId\";");

            migrationBuilder.Sql("ALTER TABLE \"Receivables\" DROP COLUMN IF EXISTS \"TagEventoId\";");
            migrationBuilder.Sql("ALTER TABLE \"Debts\" DROP COLUMN IF EXISTS \"TagEventoId\";");
            migrationBuilder.Sql("ALTER TABLE \"CardPurchases\" DROP COLUMN IF EXISTS \"TagEventoId\";");

            migrationBuilder.Sql("DROP TABLE IF EXISTS \"TagEventos\";");
        }
    }
}
