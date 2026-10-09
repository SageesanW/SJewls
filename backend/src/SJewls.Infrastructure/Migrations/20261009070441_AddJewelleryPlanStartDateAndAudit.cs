using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJewelleryPlanStartDateAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JewelleryPlans_BranchId",
                table: "JewelleryPlans");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAtUtc",
                table: "JewelleryPlans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByStaffId",
                table: "JewelleryPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReopenedAtUtc",
                table: "JewelleryPlans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReopenedByStaffId",
                table: "JewelleryPlans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "JewelleryPlans",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPlans_BranchId_IsActive_StartDate",
                table: "JewelleryPlans",
                columns: new[] { "BranchId", "IsActive", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPlans_DeactivatedByStaffId",
                table: "JewelleryPlans",
                column: "DeactivatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPlans_ReopenedByStaffId",
                table: "JewelleryPlans",
                column: "ReopenedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_JewelleryPlans_StaffMembers_DeactivatedByStaffId",
                table: "JewelleryPlans",
                column: "DeactivatedByStaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JewelleryPlans_StaffMembers_ReopenedByStaffId",
                table: "JewelleryPlans",
                column: "ReopenedByStaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JewelleryPlans_StaffMembers_DeactivatedByStaffId",
                table: "JewelleryPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_JewelleryPlans_StaffMembers_ReopenedByStaffId",
                table: "JewelleryPlans");

            migrationBuilder.DropIndex(
                name: "IX_JewelleryPlans_BranchId_IsActive_StartDate",
                table: "JewelleryPlans");

            migrationBuilder.DropIndex(
                name: "IX_JewelleryPlans_DeactivatedByStaffId",
                table: "JewelleryPlans");

            migrationBuilder.DropIndex(
                name: "IX_JewelleryPlans_ReopenedByStaffId",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "DeactivatedByStaffId",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "ReopenedAtUtc",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "ReopenedByStaffId",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "JewelleryPlans");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPlans_BranchId",
                table: "JewelleryPlans",
                column: "BranchId");
        }
    }
}
