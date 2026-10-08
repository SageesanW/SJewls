using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAuthAndManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetExpiresAtUtc",
                table: "StaffMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "StaffMembers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "StaffMembers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "StaffMembers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "StaffMembers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_Username",
                table: "StaffMembers",
                column: "Username",
                unique: true,
                filter: "\"Username\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffMembers_Username",
                table: "StaffMembers");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpiresAtUtc",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenHash",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "StaffMembers");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "StaffMembers");
        }
    }
}
