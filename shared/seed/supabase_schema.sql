CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "ActorType" text NOT NULL,
        "ActorId" uuid,
        "Action" text NOT NULL,
        "TargetEntity" text NOT NULL,
        "TargetId" text NOT NULL,
        "BranchId" uuid,
        "BeforeJson" text,
        "AfterJson" text,
        "IpAddress" text,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "Branches" (
        "Id" uuid NOT NULL,
        "Code" text NOT NULL,
        "Name" text NOT NULL,
        "Address" text NOT NULL,
        "City" text NOT NULL,
        "Country" text NOT NULL,
        "Currency" text NOT NULL,
        "Timezone" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_Branches" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "OtpChallenges" (
        "Id" uuid NOT NULL,
        "ContactValue" text NOT NULL,
        "ContactType" integer NOT NULL,
        "Code" text NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "AttemptCount" integer NOT NULL,
        "IsConsumed" boolean NOT NULL,
        "CustomerId" uuid,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_OtpChallenges" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "PaymentQuotes" (
        "Id" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "Karat" integer NOT NULL,
        "AppliedRatePerGram" numeric(18,4) NOT NULL,
        "CurrencyAmount" numeric(18,4) NOT NULL,
        "GoldGrams" numeric(18,4) NOT NULL,
        "ExpiresAtUtc" timestamp with time zone NOT NULL,
        "IsUsed" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_PaymentQuotes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "Roles" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "RoleType" integer NOT NULL,
        "Description" text NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "StaffMembers" (
        "Id" uuid NOT NULL,
        "FullName" text NOT NULL,
        "Email" text NOT NULL,
        "PasswordHash" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_StaffMembers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "ChituPlans" (
        "Id" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "Code" text NOT NULL,
        "Name" text NOT NULL,
        "Description" text NOT NULL,
        "MonthlyInstalmentAmount" numeric(18,4) NOT NULL,
        "ChituBenefitValue" numeric(18,4) NOT NULL,
        "TotalSlotCapacity" integer NOT NULL,
        "DurationMonths" integer NOT NULL,
        "DueDayOfMonth" integer NOT NULL,
        "StartDate" date NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ChituPlans" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ChituPlans_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "Customers" (
        "Id" uuid NOT NULL,
        "FullName" text NOT NULL,
        "PrimaryBranchId" uuid NOT NULL,
        "IsActive" boolean NOT NULL,
        "MergedIntoCustomerId" uuid,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_Customers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Customers_Branches_PrimaryBranchId" FOREIGN KEY ("PrimaryBranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "JewelleryPlans" (
        "Id" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "Code" text NOT NULL,
        "Name" text NOT NULL,
        "Description" text NOT NULL,
        "ImageUrl" text NOT NULL,
        "TargetProductGoldWeightGrams" numeric(18,4) NOT NULL,
        "Karat" integer NOT NULL,
        "TargetMakingCharges" numeric(18,4) NOT NULL,
        "TargetVat" numeric(18,4) NOT NULL,
        "TotalTargetAmount" numeric(18,4) NOT NULL,
        "AllowedDurationsMonths" integer[] NOT NULL,
        "CancellationFeeType" integer NOT NULL,
        "CancellationFeeValue" numeric(18,4) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_JewelleryPlans" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_JewelleryPlans_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "GoldRates" (
        "Id" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "Karat" integer NOT NULL,
        "RatePerGram" numeric(18,4) NOT NULL,
        "EffectiveFromUtc" timestamp with time zone NOT NULL,
        "RecordedByStaffId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_GoldRates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GoldRates_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_GoldRates_StaffMembers_RecordedByStaffId" FOREIGN KEY ("RecordedByStaffId") REFERENCES "StaffMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "StaffBranches" (
        "Id" uuid NOT NULL,
        "StaffId" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_StaffBranches" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StaffBranches_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StaffBranches_StaffMembers_StaffId" FOREIGN KEY ("StaffId") REFERENCES "StaffMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "StaffRoles" (
        "Id" uuid NOT NULL,
        "StaffId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_StaffRoles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StaffRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StaffRoles_StaffMembers_StaffId" FOREIGN KEY ("StaffId") REFERENCES "StaffMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "ChituSlots" (
        "Id" uuid NOT NULL,
        "ChituPlanId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "SlotNumber" integer NOT NULL,
        "Status" integer NOT NULL,
        "ReservedUntilUtc" timestamp with time zone,
        "ActivatedAtUtc" timestamp with time zone,
        "ClosedAtUtc" timestamp with time zone,
        "ClosureRemarks" text,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ChituSlots" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ChituSlots_ChituPlans_ChituPlanId" FOREIGN KEY ("ChituPlanId") REFERENCES "ChituPlans" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ChituSlots_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "CustomerContacts" (
        "Id" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "Type" integer NOT NULL,
        "Value" text NOT NULL,
        "IsVerified" boolean NOT NULL,
        "VerifiedAtUtc" timestamp with time zone,
        "IsPrimary" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_CustomerContacts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CustomerContacts_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "Notifications" (
        "Id" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "Title" text NOT NULL,
        "Message" text NOT NULL,
        "Type" text NOT NULL,
        "IsRead" boolean NOT NULL,
        "ReadAtUtc" timestamp with time zone,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Notifications_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "Payments" (
        "Id" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "Module" integer NOT NULL,
        "Amount" numeric(18,4) NOT NULL,
        "Currency" text NOT NULL,
        "Method" integer NOT NULL,
        "Status" integer NOT NULL,
        "ProviderReference" text NOT NULL,
        "IdempotencyKey" text NOT NULL,
        "RecordedByStaffId" uuid,
        "CompletedAtUtc" timestamp with time zone,
        "Remarks" text,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_Payments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Payments_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Payments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Payments_StaffMembers_RecordedByStaffId" FOREIGN KEY ("RecordedByStaffId") REFERENCES "StaffMembers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "PhysicalClaims" (
        "Id" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "Module" integer NOT NULL,
        "ReferenceId" uuid NOT NULL,
        "ClaimedAtUtc" timestamp with time zone NOT NULL,
        "HandedOverByStaffId" uuid NOT NULL,
        "Remarks" text NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_PhysicalClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PhysicalClaims_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PhysicalClaims_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PhysicalClaims_StaffMembers_HandedOverByStaffId" FOREIGN KEY ("HandedOverByStaffId") REFERENCES "StaffMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "JewelleryEnrolments" (
        "Id" uuid NOT NULL,
        "JewelleryPlanId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "BranchId" uuid NOT NULL,
        "SelectedDurationMonths" integer NOT NULL,
        "StartDate" date NOT NULL,
        "TargetEndDate" date NOT NULL,
        "TargetProductWeightGrams" numeric(18,4) NOT NULL,
        "TotalTargetCurrency" numeric(18,4) NOT NULL,
        "TotalSavedGrams" numeric(18,4) NOT NULL,
        "TotalContributedCurrency" numeric(18,4) NOT NULL,
        "Status" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_JewelleryEnrolments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_JewelleryEnrolments_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_JewelleryEnrolments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_JewelleryEnrolments_JewelleryPlans_JewelleryPlanId" FOREIGN KEY ("JewelleryPlanId") REFERENCES "JewelleryPlans" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "ChituDraws" (
        "Id" uuid NOT NULL,
        "ChituPlanId" uuid NOT NULL,
        "DrawMonthIndex" integer NOT NULL,
        "DrawDate" date NOT NULL,
        "WinningSlotId" uuid NOT NULL,
        "RecordedByStaffId" uuid NOT NULL,
        "Remarks" text NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ChituDraws" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ChituDraws_ChituPlans_ChituPlanId" FOREIGN KEY ("ChituPlanId") REFERENCES "ChituPlans" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ChituDraws_ChituSlots_WinningSlotId" FOREIGN KEY ("WinningSlotId") REFERENCES "ChituSlots" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ChituDraws_StaffMembers_RecordedByStaffId" FOREIGN KEY ("RecordedByStaffId") REFERENCES "StaffMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "ChituInstalments" (
        "Id" uuid NOT NULL,
        "ChituSlotId" uuid NOT NULL,
        "MonthIndex" integer NOT NULL,
        "DueDate" date NOT NULL,
        "AmountDue" numeric(18,4) NOT NULL,
        "Status" integer NOT NULL,
        "PaidAtUtc" timestamp with time zone,
        "PaymentId" uuid,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ChituInstalments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ChituInstalments_ChituSlots_ChituSlotId" FOREIGN KEY ("ChituSlotId") REFERENCES "ChituSlots" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "PaymentAllocations" (
        "Id" uuid NOT NULL,
        "PaymentId" uuid NOT NULL,
        "AllocationType" text NOT NULL,
        "TargetEntityId" uuid NOT NULL,
        "AllocatedAmount" numeric(18,4) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_PaymentAllocations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PaymentAllocations_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "Refunds" (
        "Id" uuid NOT NULL,
        "PaymentId" uuid NOT NULL,
        "GrossAmount" numeric(18,4) NOT NULL,
        "FeeAmount" numeric(18,4) NOT NULL,
        "NetRefundAmount" numeric(18,4) NOT NULL,
        "Reason" text NOT NULL,
        "Status" integer NOT NULL,
        "ApprovedByStaffId" uuid,
        "ProcessedAtUtc" timestamp with time zone,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_Refunds" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Refunds_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Refunds_StaffMembers_ApprovedByStaffId" FOREIGN KEY ("ApprovedByStaffId") REFERENCES "StaffMembers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "ClosureRequests" (
        "Id" uuid NOT NULL,
        "JewelleryEnrolmentId" uuid NOT NULL,
        "Reason" text NOT NULL,
        "Status" integer NOT NULL,
        "ApprovedRefundAmount" numeric(18,4) NOT NULL,
        "CancellationFeeCharged" numeric(18,4) NOT NULL,
        "DecidedByStaffId" uuid,
        "DecidedAtUtc" timestamp with time zone,
        "DecisionRemarks" text,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ClosureRequests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ClosureRequests_JewelleryEnrolments_JewelleryEnrolmentId" FOREIGN KEY ("JewelleryEnrolmentId") REFERENCES "JewelleryEnrolments" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ClosureRequests_StaffMembers_DecidedByStaffId" FOREIGN KEY ("DecidedByStaffId") REFERENCES "StaffMembers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "ExtensionRequests" (
        "Id" uuid NOT NULL,
        "JewelleryEnrolmentId" uuid NOT NULL,
        "RequestedExtensionMonths" integer NOT NULL,
        "Reason" text NOT NULL,
        "Status" integer NOT NULL,
        "DecidedByStaffId" uuid,
        "DecidedAtUtc" timestamp with time zone,
        "DecisionRemarks" text,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ExtensionRequests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExtensionRequests_JewelleryEnrolments_JewelleryEnrolmentId" FOREIGN KEY ("JewelleryEnrolmentId") REFERENCES "JewelleryEnrolments" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ExtensionRequests_StaffMembers_DecidedByStaffId" FOREIGN KEY ("DecidedByStaffId") REFERENCES "StaffMembers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "JewelleryContributions" (
        "Id" uuid NOT NULL,
        "JewelleryEnrolmentId" uuid NOT NULL,
        "CurrencyAmount" numeric(18,4) NOT NULL,
        "GoldGrams" numeric(18,4) NOT NULL,
        "AppliedRatePerGram" numeric(18,4) NOT NULL,
        "Karat" integer NOT NULL,
        "PaymentId" uuid NOT NULL,
        "ConfirmedAtUtc" timestamp with time zone NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_JewelleryContributions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_JewelleryContributions_JewelleryEnrolments_JewelleryEnrolme~" FOREIGN KEY ("JewelleryEnrolmentId") REFERENCES "JewelleryEnrolments" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_JewelleryContributions_Payments_PaymentId" FOREIGN KEY ("PaymentId") REFERENCES "Payments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE TABLE "WinnerBenefits" (
        "Id" uuid NOT NULL,
        "ChituDrawId" uuid NOT NULL,
        "ChituSlotId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "ChituBenefitValue" numeric(18,4) NOT NULL,
        "Status" integer NOT NULL,
        "ClaimedAtUtc" timestamp with time zone,
        "HandedOverByStaffId" uuid,
        "ClaimRemarks" text,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_WinnerBenefits" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WinnerBenefits_ChituDraws_ChituDrawId" FOREIGN KEY ("ChituDrawId") REFERENCES "ChituDraws" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_WinnerBenefits_ChituSlots_ChituSlotId" FOREIGN KEY ("ChituSlotId") REFERENCES "ChituSlots" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_WinnerBenefits_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_WinnerBenefits_StaffMembers_HandedOverByStaffId" FOREIGN KEY ("HandedOverByStaffId") REFERENCES "StaffMembers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Branches_Code" ON "Branches" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ChituDraws_ChituPlanId" ON "ChituDraws" ("ChituPlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ChituDraws_RecordedByStaffId" ON "ChituDraws" ("RecordedByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ChituDraws_WinningSlotId" ON "ChituDraws" ("WinningSlotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_ChituInstalments_ChituSlotId_MonthIndex" ON "ChituInstalments" ("ChituSlotId", "MonthIndex");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ChituPlans_BranchId" ON "ChituPlans" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_ChituSlots_ChituPlanId_SlotNumber" ON "ChituSlots" ("ChituPlanId", "SlotNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ChituSlots_CustomerId" ON "ChituSlots" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ClosureRequests_DecidedByStaffId" ON "ClosureRequests" ("DecidedByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ClosureRequests_JewelleryEnrolmentId" ON "ClosureRequests" ("JewelleryEnrolmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_CustomerContacts_CustomerId" ON "CustomerContacts" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_CustomerContacts_Type_Value_IsVerified" ON "CustomerContacts" ("Type", "Value", "IsVerified");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Customers_PrimaryBranchId" ON "Customers" ("PrimaryBranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ExtensionRequests_DecidedByStaffId" ON "ExtensionRequests" ("DecidedByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_ExtensionRequests_JewelleryEnrolmentId" ON "ExtensionRequests" ("JewelleryEnrolmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_GoldRates_BranchId" ON "GoldRates" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_GoldRates_RecordedByStaffId" ON "GoldRates" ("RecordedByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_JewelleryContributions_JewelleryEnrolmentId" ON "JewelleryContributions" ("JewelleryEnrolmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_JewelleryContributions_PaymentId" ON "JewelleryContributions" ("PaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_JewelleryEnrolments_BranchId" ON "JewelleryEnrolments" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_JewelleryEnrolments_CustomerId" ON "JewelleryEnrolments" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_JewelleryEnrolments_JewelleryPlanId" ON "JewelleryEnrolments" ("JewelleryPlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_JewelleryPlans_BranchId" ON "JewelleryPlans" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_JewelleryPlans_Code" ON "JewelleryPlans" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Notifications_CustomerId" ON "Notifications" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_PaymentAllocations_PaymentId" ON "PaymentAllocations" ("PaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Payments_BranchId" ON "Payments" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Payments_CustomerId" ON "Payments" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Payments_IdempotencyKey" ON "Payments" ("IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Payments_ProviderReference" ON "Payments" ("ProviderReference");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Payments_RecordedByStaffId" ON "Payments" ("RecordedByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_PhysicalClaims_BranchId" ON "PhysicalClaims" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_PhysicalClaims_CustomerId" ON "PhysicalClaims" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_PhysicalClaims_HandedOverByStaffId" ON "PhysicalClaims" ("HandedOverByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Refunds_ApprovedByStaffId" ON "Refunds" ("ApprovedByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_Refunds_PaymentId" ON "Refunds" ("PaymentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_StaffBranches_BranchId" ON "StaffBranches" ("BranchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_StaffBranches_StaffId" ON "StaffBranches" ("StaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_StaffMembers_Email" ON "StaffMembers" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_StaffRoles_RoleId" ON "StaffRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_StaffRoles_StaffId" ON "StaffRoles" ("StaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_WinnerBenefits_ChituDrawId" ON "WinnerBenefits" ("ChituDrawId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_WinnerBenefits_ChituSlotId" ON "WinnerBenefits" ("ChituSlotId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_WinnerBenefits_CustomerId" ON "WinnerBenefits" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    CREATE INDEX "IX_WinnerBenefits_HandedOverByStaffId" ON "WinnerBenefits" ("HandedOverByStaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007043409_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261007043409_InitialCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

