using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAuthAndRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerContacts_Type_Value_IsVerified",
                table: "CustomerContacts");

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "OtpChallenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "OtpChallenges",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResendCooldownUntilUtc",
                table: "OtpChallenges",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "Customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProfileComplete",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Nic",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "text", nullable: true),
                    CreatedByIp = table.Column<string>(type: "text", nullable: true),
                    RevokedByIp = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationToken = table.Column<string>(type: "text", nullable: false),
                    ContactValue = table.Column<string>(type: "text", nullable: false),
                    ContactType = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_ContactValue_Purpose_IsConsumed",
                table: "OtpChallenges",
                columns: new[] { "ContactValue", "Purpose", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Nic",
                table: "Customers",
                column: "Nic",
                unique: true,
                filter: "\"Nic\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_Type_Value",
                table: "CustomerContacts",
                columns: new[] { "Type", "Value" },
                unique: true,
                filter: "\"IsVerified\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_CustomerId",
                table: "RefreshTokens",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationSessions_RegistrationToken",
                table: "RegistrationSessions",
                column: "RegistrationToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RegistrationSessions");

            migrationBuilder.DropIndex(
                name: "IX_OtpChallenges_ContactValue_Purpose_IsConsumed",
                table: "OtpChallenges");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Nic",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerContacts_Type_Value",
                table: "CustomerContacts");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "OtpChallenges");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpChallenges");

            migrationBuilder.DropColumn(
                name: "ResendCooldownUntilUtc",
                table: "OtpChallenges");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsProfileComplete",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Nic",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_Type_Value_IsVerified",
                table: "CustomerContacts",
                columns: new[] { "Type", "Value", "IsVerified" });
        }
    }
}
