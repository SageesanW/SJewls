using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerJewelleryPlanFeaturesAndQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JewelleryEnrolments_Customers_CustomerId",
                table: "JewelleryEnrolments");

            migrationBuilder.DropIndex(
                name: "IX_JewelleryEnrolments_CustomerId",
                table: "JewelleryEnrolments");

            migrationBuilder.AddColumn<decimal>(
                name: "PremiumPercentage",
                table: "JewelleryPlans",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CategoryNameSnapshot",
                table: "JewelleryEnrolments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "JewelleryEnrolments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "JewelleryEnrolments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrlSnapshot",
                table: "JewelleryEnrolments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanNameSnapshot",
                table: "JewelleryEnrolments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PremiumPercentage",
                table: "JewelleryEnrolments",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            // Safe backfill for existing rows
            migrationBuilder.Sql("UPDATE \"JewelleryPlans\" SET \"PremiumPercentage\" = 5.0 WHERE \"PremiumPercentage\" = 0;");
            migrationBuilder.Sql(@"
                UPDATE ""JewelleryEnrolments"" e
                SET ""PremiumPercentage"" = 5.0,
                    ""Currency"" = 'LKR',
                    ""PlanNameSnapshot"" = COALESCE(p.""Name"", ''),
                    ""CategoryNameSnapshot"" = COALESCE(c.""Name"", ''),
                    ""ImageUrlSnapshot"" = p.""ImageUrl""
                FROM ""JewelleryPlans"" p
                LEFT JOIN ""JewelleryCategories"" c ON p.""CategoryId"" = c.""Id""
                WHERE e.""JewelleryPlanId"" = p.""Id""
                  AND (e.""PlanNameSnapshot"" = '' OR e.""PremiumPercentage"" = 0);
            ");

            migrationBuilder.CreateTable(
                name: "JewelleryPaymentQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    JewelleryEnrolmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputMode = table.Column<int>(type: "integer", nullable: false),
                    EnteredValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    RatePerGram = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RateSource = table.Column<string>(type: "text", nullable: false),
                    RateAsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PremiumPercentage = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BaseGoldGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PremiumGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ChargeableGoldGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    TotalGoldCredit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    PremiumAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AmountToPay = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RemainingGramsAfterPayment = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JewelleryPaymentQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JewelleryPaymentQuotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JewelleryPaymentQuotes_JewelleryEnrolments_JewelleryEnrolme~",
                        column: x => x.JewelleryEnrolmentId,
                        principalTable: "JewelleryEnrolments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JewelleryPaymentQuotes_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryEnrolments_CustomerId_IdempotencyKey",
                table: "JewelleryEnrolments",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPaymentQuotes_CustomerId",
                table: "JewelleryPaymentQuotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPaymentQuotes_ExpiresAtUtc",
                table: "JewelleryPaymentQuotes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPaymentQuotes_JewelleryEnrolmentId_Status",
                table: "JewelleryPaymentQuotes",
                columns: new[] { "JewelleryEnrolmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPaymentQuotes_PaymentId",
                table: "JewelleryPaymentQuotes",
                column: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_JewelleryEnrolments_Customers_CustomerId",
                table: "JewelleryEnrolments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JewelleryEnrolments_Customers_CustomerId",
                table: "JewelleryEnrolments");

            migrationBuilder.DropTable(
                name: "JewelleryPaymentQuotes");

            migrationBuilder.DropIndex(
                name: "IX_JewelleryEnrolments_CustomerId_IdempotencyKey",
                table: "JewelleryEnrolments");

            migrationBuilder.DropColumn(
                name: "PremiumPercentage",
                table: "JewelleryPlans");

            migrationBuilder.DropColumn(
                name: "CategoryNameSnapshot",
                table: "JewelleryEnrolments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "JewelleryEnrolments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "JewelleryEnrolments");

            migrationBuilder.DropColumn(
                name: "ImageUrlSnapshot",
                table: "JewelleryEnrolments");

            migrationBuilder.DropColumn(
                name: "PlanNameSnapshot",
                table: "JewelleryEnrolments");

            migrationBuilder.DropColumn(
                name: "PremiumPercentage",
                table: "JewelleryEnrolments");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryEnrolments_CustomerId",
                table: "JewelleryEnrolments",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_JewelleryEnrolments_Customers_CustomerId",
                table: "JewelleryEnrolments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
