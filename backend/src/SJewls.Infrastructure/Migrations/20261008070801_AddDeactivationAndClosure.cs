using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeactivationAndClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAtUtc",
                table: "StaffMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByStaffId",
                table: "StaffMembers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "StaffMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAtUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByStaffId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_DeactivatedByStaffId",
                table: "Customers",
                column: "DeactivatedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_StaffMembers_DeactivatedByStaffId",
                table: "Customers",
                column: "DeactivatedByStaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_StaffMembers_DeactivatedByStaffId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_DeactivatedByStaffId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "DeactivatedByStaffId",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeactivatedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeactivatedByStaffId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                table: "Customers");
        }
    }
}
