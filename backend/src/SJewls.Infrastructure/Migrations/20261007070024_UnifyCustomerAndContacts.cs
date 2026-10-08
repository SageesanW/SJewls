using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyCustomerAndContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerifiedAtUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PhoneVerifiedAtUtc",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Customers"" c
                SET ""IsPhoneVerified"" = COALESCE((SELECT ""IsVerified"" FROM ""CustomerContacts"" WHERE ""CustomerId"" = c.""Id"" AND ""Type"" = 1 LIMIT 1), false),
                    ""PhoneVerifiedAtUtc"" = (SELECT ""VerifiedAtUtc"" FROM ""CustomerContacts"" WHERE ""CustomerId"" = c.""Id"" AND ""Type"" = 1 LIMIT 1),
                    ""IsEmailVerified"" = COALESCE((SELECT ""IsVerified"" FROM ""CustomerContacts"" WHERE ""CustomerId"" = c.""Id"" AND ""Type"" = 2 LIMIT 1), false),
                    ""EmailVerifiedAtUtc"" = (SELECT ""VerifiedAtUtc"" FROM ""CustomerContacts"" WHERE ""CustomerId"" = c.""Id"" AND ""Type"" = 2 LIMIT 1);
            ");

            migrationBuilder.DropTable(
                name: "CustomerContacts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAtUtc",
                table: "Customers");

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_CustomerId",
                table: "CustomerContacts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_Type_Value",
                table: "CustomerContacts",
                columns: new[] { "Type", "Value" },
                unique: true,
                filter: "\"IsVerified\" = true");
        }
    }
}
