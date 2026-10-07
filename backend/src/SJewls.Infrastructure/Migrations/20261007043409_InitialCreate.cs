using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorType = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    TargetEntity = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<string>(type: "text", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Timezone = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtpChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactValue = table.Column<string>(type: "text", nullable: false),
                    ContactType = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    IsConsumed = table.Column<bool>(type: "boolean", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Karat = table.Column<int>(type: "integer", nullable: false),
                    AppliedRatePerGram = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GoldGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentQuotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RoleType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChituPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MonthlyInstalmentAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ChituBenefitValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalSlotCapacity = table.Column<int>(type: "integer", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    DueDayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChituPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChituPlans_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    PrimaryBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MergedIntoCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Branches_PrimaryBranchId",
                        column: x => x.PrimaryBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JewelleryPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    TargetProductGoldWeightGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Karat = table.Column<int>(type: "integer", nullable: false),
                    TargetMakingCharges = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TargetVat = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalTargetAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AllowedDurationsMonths = table.Column<int[]>(type: "integer[]", nullable: false),
                    CancellationFeeType = table.Column<int>(type: "integer", nullable: false),
                    CancellationFeeValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JewelleryPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JewelleryPlans_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoldRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Karat = table.Column<int>(type: "integer", nullable: false),
                    RatePerGram = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoldRates_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoldRates_StaffMembers_RecordedByStaffId",
                        column: x => x.RecordedByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffBranches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffBranches_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffRoles_StaffMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChituSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureRemarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChituSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChituSlots_ChituPlans_ChituPlanId",
                        column: x => x.ChituPlanId,
                        principalTable: "ChituPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChituSlots_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderReference = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_StaffMembers_RecordedByStaffId",
                        column: x => x.RecordedByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhysicalClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HandedOverByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalClaims_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalClaims_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalClaims_StaffMembers_HandedOverByStaffId",
                        column: x => x.HandedOverByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JewelleryEnrolments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JewelleryPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedDurationMonths = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetProductWeightGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalTargetCurrency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalSavedGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalContributedCurrency = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JewelleryEnrolments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JewelleryEnrolments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JewelleryEnrolments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JewelleryEnrolments_JewelleryPlans_JewelleryPlanId",
                        column: x => x.JewelleryPlanId,
                        principalTable: "JewelleryPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChituDraws",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DrawMonthIndex = table.Column<int>(type: "integer", nullable: false),
                    DrawDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WinningSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChituDraws", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChituDraws_ChituPlans_ChituPlanId",
                        column: x => x.ChituPlanId,
                        principalTable: "ChituPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChituDraws_ChituSlots_WinningSlotId",
                        column: x => x.WinningSlotId,
                        principalTable: "ChituSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChituDraws_StaffMembers_RecordedByStaffId",
                        column: x => x.RecordedByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChituInstalments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthIndex = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChituInstalments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChituInstalments_ChituSlots_ChituSlotId",
                        column: x => x.ChituSlotId,
                        principalTable: "ChituSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationType = table.Column<string>(type: "text", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetRefundAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Refunds_StaffMembers_ApprovedByStaffId",
                        column: x => x.ApprovedByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClosureRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JewelleryEnrolmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedRefundAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CancellationFeeCharged = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DecidedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRemarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosureRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClosureRequests_JewelleryEnrolments_JewelleryEnrolmentId",
                        column: x => x.JewelleryEnrolmentId,
                        principalTable: "JewelleryEnrolments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClosureRequests_StaffMembers_DecidedByStaffId",
                        column: x => x.DecidedByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExtensionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JewelleryEnrolmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedExtensionMonths = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRemarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtensionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtensionRequests_JewelleryEnrolments_JewelleryEnrolmentId",
                        column: x => x.JewelleryEnrolmentId,
                        principalTable: "JewelleryEnrolments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExtensionRequests_StaffMembers_DecidedByStaffId",
                        column: x => x.DecidedByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JewelleryContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JewelleryEnrolmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GoldGrams = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AppliedRatePerGram = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Karat = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JewelleryContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JewelleryContributions_JewelleryEnrolments_JewelleryEnrolme~",
                        column: x => x.JewelleryEnrolmentId,
                        principalTable: "JewelleryEnrolments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JewelleryContributions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WinnerBenefits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituDrawId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChituBenefitValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HandedOverByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimRemarks = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WinnerBenefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WinnerBenefits_ChituDraws_ChituDrawId",
                        column: x => x.ChituDrawId,
                        principalTable: "ChituDraws",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WinnerBenefits_ChituSlots_ChituSlotId",
                        column: x => x.ChituSlotId,
                        principalTable: "ChituSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WinnerBenefits_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WinnerBenefits_StaffMembers_HandedOverByStaffId",
                        column: x => x.HandedOverByStaffId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_ChituPlanId",
                table: "ChituDraws",
                column: "ChituPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_RecordedByStaffId",
                table: "ChituDraws",
                column: "RecordedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituDraws_WinningSlotId",
                table: "ChituDraws",
                column: "WinningSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituInstalments_ChituSlotId_MonthIndex",
                table: "ChituInstalments",
                columns: new[] { "ChituSlotId", "MonthIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChituPlans_BranchId",
                table: "ChituPlans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ChituSlots_ChituPlanId_SlotNumber",
                table: "ChituSlots",
                columns: new[] { "ChituPlanId", "SlotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChituSlots_CustomerId",
                table: "ChituSlots",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureRequests_DecidedByStaffId",
                table: "ClosureRequests",
                column: "DecidedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureRequests_JewelleryEnrolmentId",
                table: "ClosureRequests",
                column: "JewelleryEnrolmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_CustomerId",
                table: "CustomerContacts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_Type_Value_IsVerified",
                table: "CustomerContacts",
                columns: new[] { "Type", "Value", "IsVerified" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PrimaryBranchId",
                table: "Customers",
                column: "PrimaryBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtensionRequests_DecidedByStaffId",
                table: "ExtensionRequests",
                column: "DecidedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtensionRequests_JewelleryEnrolmentId",
                table: "ExtensionRequests",
                column: "JewelleryEnrolmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldRates_BranchId",
                table: "GoldRates",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_GoldRates_RecordedByStaffId",
                table: "GoldRates",
                column: "RecordedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryContributions_JewelleryEnrolmentId",
                table: "JewelleryContributions",
                column: "JewelleryEnrolmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryContributions_PaymentId",
                table: "JewelleryContributions",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryEnrolments_BranchId",
                table: "JewelleryEnrolments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryEnrolments_CustomerId",
                table: "JewelleryEnrolments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryEnrolments_JewelleryPlanId",
                table: "JewelleryEnrolments",
                column: "JewelleryPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPlans_BranchId",
                table: "JewelleryPlans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_JewelleryPlans_Code",
                table: "JewelleryPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CustomerId",
                table: "Notifications",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BranchId",
                table: "Payments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CustomerId",
                table: "Payments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderReference",
                table: "Payments",
                column: "ProviderReference");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RecordedByStaffId",
                table: "Payments",
                column: "RecordedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalClaims_BranchId",
                table: "PhysicalClaims",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalClaims_CustomerId",
                table: "PhysicalClaims",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalClaims_HandedOverByStaffId",
                table: "PhysicalClaims",
                column: "HandedOverByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ApprovedByStaffId",
                table: "Refunds",
                column: "ApprovedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffBranches_BranchId",
                table: "StaffBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffBranches_StaffId",
                table: "StaffBranches",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_Email",
                table: "StaffMembers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_RoleId",
                table: "StaffRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_StaffId",
                table: "StaffRoles",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_WinnerBenefits_ChituDrawId",
                table: "WinnerBenefits",
                column: "ChituDrawId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WinnerBenefits_ChituSlotId",
                table: "WinnerBenefits",
                column: "ChituSlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WinnerBenefits_CustomerId",
                table: "WinnerBenefits",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_WinnerBenefits_HandedOverByStaffId",
                table: "WinnerBenefits",
                column: "HandedOverByStaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ChituInstalments");

            migrationBuilder.DropTable(
                name: "ClosureRequests");

            migrationBuilder.DropTable(
                name: "CustomerContacts");

            migrationBuilder.DropTable(
                name: "ExtensionRequests");

            migrationBuilder.DropTable(
                name: "GoldRates");

            migrationBuilder.DropTable(
                name: "JewelleryContributions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OtpChallenges");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "PaymentQuotes");

            migrationBuilder.DropTable(
                name: "PhysicalClaims");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "StaffBranches");

            migrationBuilder.DropTable(
                name: "StaffRoles");

            migrationBuilder.DropTable(
                name: "WinnerBenefits");

            migrationBuilder.DropTable(
                name: "JewelleryEnrolments");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "ChituDraws");

            migrationBuilder.DropTable(
                name: "JewelleryPlans");

            migrationBuilder.DropTable(
                name: "ChituSlots");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "ChituPlans");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
