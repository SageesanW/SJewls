using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJewelleryPlanCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"JewelleryPlans\" ADD COLUMN IF NOT EXISTS \"CategoryId\" uuid;");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""JewelleryCategories"" (
                    ""Id"" uuid NOT NULL,
                    ""BranchId"" uuid NOT NULL,
                    ""Name"" text NOT NULL,
                    ""NormalizedName"" text NOT NULL,
                    ""Description"" text,
                    ""ImageUrl"" text,
                    ""IsActive"" boolean NOT NULL,
                    ""CreatedAtUtc"" timestamp with time zone NOT NULL,
                    ""UpdatedAtUtc"" timestamp with time zone,
                    CONSTRAINT ""PK_JewelleryCategories"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_JewelleryCategories_Branches_BranchId"" FOREIGN KEY (""BranchId"") REFERENCES ""Branches"" (""Id"") ON DELETE RESTRICT
                );
            ");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JewelleryPlans_CategoryId\" ON \"JewelleryPlans\" (\"CategoryId\");");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_JewelleryCategories_BranchId_NormalizedName\" ON \"JewelleryCategories\" (\"BranchId\", \"NormalizedName\");");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_JewelleryPlans_JewelleryCategories_CategoryId'
                    ) THEN
                        ALTER TABLE ""JewelleryPlans""
                        ADD CONSTRAINT ""FK_JewelleryPlans_JewelleryCategories_CategoryId""
                        FOREIGN KEY (""CategoryId"") REFERENCES ""JewelleryCategories"" (""Id"") ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JewelleryPlans_JewelleryCategories_CategoryId",
                table: "JewelleryPlans");

            migrationBuilder.DropTable(
                name: "JewelleryCategories");

            migrationBuilder.DropIndex(
                name: "IX_JewelleryPlans_CategoryId",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "JewelleryPlans");
        }
    }
}
