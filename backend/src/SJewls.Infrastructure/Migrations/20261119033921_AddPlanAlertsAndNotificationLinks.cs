using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanAlertsAndNotificationLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlanAlertId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlanAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanNameSnapshot = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituDrawId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChituSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChituSlotNumber = table.Column<int>(type: "integer", nullable: true),
                    ChituSlotDisplayCode = table.Column<string>(type: "text", nullable: true),
                    ChituRoundIndex = table.Column<int>(type: "integer", nullable: true),
                    ChituRoundScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ChituRoundScheduledMonthYear = table.Column<string>(type: "text", nullable: true),
                    RedemptionType = table.Column<int>(type: "integer", nullable: true),
                    PrizeAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    JewelleryEnrolmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchName = table.Column<string>(type: "text", nullable: false),
                    BranchAddress = table.Column<string>(type: "text", nullable: true),
                    BranchPhone = table.Column<string>(type: "text", nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NavigationTarget = table.Column<string>(type: "text", nullable: false),
                    CollectionInstruction = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_ChituDraws_ChituDrawId",
                        column: x => x.ChituDrawId,
                        principalTable: "ChituDraws",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_ChituSlots_ChituSlotId",
                        column: x => x.ChituSlotId,
                        principalTable: "ChituSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_JewelleryEnrolments_JewelleryEnrolmentId",
                        column: x => x.JewelleryEnrolmentId,
                        principalTable: "JewelleryEnrolments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlanAlerts_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_BranchId",
                table: "PlanAlerts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_ChituDrawId",
                table: "PlanAlerts",
                column: "ChituDrawId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_ChituSlotId",
                table: "PlanAlerts",
                column: "ChituSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_ChituSlotId_ChituRoundIndex",
                table: "PlanAlerts",
                columns: new[] { "ChituSlotId", "ChituRoundIndex" },
                unique: true,
                filter: "\"ChituSlotId\" IS NOT NULL AND \"ChituRoundIndex\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_CustomerId",
                table: "PlanAlerts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_CustomerId_PlanId",
                table: "PlanAlerts",
                columns: new[] { "CustomerId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_JewelleryEnrolmentId",
                table: "PlanAlerts",
                column: "JewelleryEnrolmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_JewelleryEnrolmentId_EventType",
                table: "PlanAlerts",
                columns: new[] { "JewelleryEnrolmentId", "EventType" },
                unique: true,
                filter: "\"JewelleryEnrolmentId\" IS NOT NULL AND \"EventType\" = 'JewelleryCompletion'");

            migrationBuilder.CreateIndex(
                name: "IX_PlanAlerts_NotificationId",
                table: "PlanAlerts",
                column: "NotificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanAlerts");

            migrationBuilder.DropColumn(
                name: "PlanAlertId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "Notifications");
        }
    }
}
