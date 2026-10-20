using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChituPlanRedemptionAndRaffleEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChituPlans_Branches_BranchId",
                table: "ChituPlans");

            migrationBuilder.DropIndex(
                name: "IX_ChituPlans_BranchId",
                table: "ChituPlans");

            migrationBuilder.DropIndex(
                name: "IX_ChituDraws_ChituPlanId",
                table: "ChituDraws");

            migrationBuilder.DropIndex(
                name: "IX_ChituDraws_WinningSlotId",
                table: "ChituDraws");

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceExcessAmount",
                table: "WinnerBenefits",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RedemptionType",
                table: "WinnerBenefits",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "PrepaidExcessAmount",
                table: "ChituSlots",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SlotDisplayCode",
                table: "ChituSlots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WinningRoundIndex",
                table: "ChituSlots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "ChituPlans",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<int>(
                name: "RedemptionType",
                table: "ChituPlans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Safe backfill for existing historical records
            migrationBuilder.Sql(@"
                UPDATE ""ChituPlans"" 
                SET ""EndDate"" = (""StartDate"" + (((""DurationMonths"" - 1) || ' month')::interval))::date 
                WHERE ""EndDate"" = '0001-01-01';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""ChituSlots"" s 
                SET ""SlotDisplayCode"" = p.""Code"" || '-' || LPAD(s.""SlotNumber""::text, 3, '0') 
                FROM ""ChituPlans"" p 
                WHERE s.""ChituPlanId"" = p.""Id"" AND (s.""SlotDisplayCode"" IS NULL OR s.""SlotDisplayCode"" = '');
            ");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordedAtUtc",
                table: "ChituDraws",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "ChituDraws",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChituPlans_BranchId_Code",
                table: "ChituPlans",
                columns: new[] { "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChituInstalments_PaymentId",
                table: "ChituInstalments",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_ChituPlanId_DrawMonthIndex",
                table: "ChituDraws",
                columns: new[] { "ChituPlanId", "DrawMonthIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_WinningSlotId",
                table: "ChituDraws",
                column: "WinningSlotId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChituInstalments_Payments_PaymentId",
                table: "ChituInstalments",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChituPlans_Branches_BranchId",
                table: "ChituPlans",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChituInstalments_Payments_PaymentId",
                table: "ChituInstalments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChituPlans_Branches_BranchId",
                table: "ChituPlans");

            migrationBuilder.DropIndex(
                name: "IX_ChituPlans_BranchId_Code",
                table: "ChituPlans");

            migrationBuilder.DropIndex(
                name: "IX_ChituInstalments_PaymentId",
                table: "ChituInstalments");

            migrationBuilder.DropIndex(
                name: "IX_ChituDraws_ChituPlanId_DrawMonthIndex",
                table: "ChituDraws");

            migrationBuilder.DropIndex(
                name: "IX_ChituDraws_WinningSlotId",
                table: "ChituDraws");

            migrationBuilder.DropColumn(
                name: "AdvanceExcessAmount",
                table: "WinnerBenefits");

            migrationBuilder.DropColumn(
                name: "RedemptionType",
                table: "WinnerBenefits");

            migrationBuilder.DropColumn(
                name: "PrepaidExcessAmount",
                table: "ChituSlots");

            migrationBuilder.DropColumn(
                name: "SlotDisplayCode",
                table: "ChituSlots");

            migrationBuilder.DropColumn(
                name: "WinningRoundIndex",
                table: "ChituSlots");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "ChituPlans");

            migrationBuilder.DropColumn(
                name: "RedemptionType",
                table: "ChituPlans");

            migrationBuilder.DropColumn(
                name: "RecordedAtUtc",
                table: "ChituDraws");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "ChituDraws");

            migrationBuilder.CreateIndex(
                name: "IX_ChituPlans_BranchId",
                table: "ChituPlans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_ChituPlanId",
                table: "ChituDraws",
                column: "ChituPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_WinningSlotId",
                table: "ChituDraws",
                column: "WinningSlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChituPlans_Branches_BranchId",
                table: "ChituPlans",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
