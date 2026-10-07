-- ==============================================================================
-- SJewls Demo Seed Script - Jaffna Active Branch
-- ==============================================================================

-- 1. Insert Jaffna Branch
INSERT INTO "Branches" ("Id", "Code", "Name", "Address", "City", "Country", "Currency", "Timezone", "IsActive", "CreatedAtUtc")
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'JAF-01',
    'SJewls Jaffna Main Branch',
    '124 Hospital Road',
    'Jaffna',
    'Sri Lanka',
    'LKR',
    'Asia/Colombo',
    true,
    NOW()
) ON CONFLICT ("Code") DO NOTHING;

-- 2. Insert Roles
INSERT INTO "Roles" ("Id", "Name", "RoleType", "Description", "CreatedAtUtc")
VALUES 
    ('b0000000-0000-0000-0000-000000000001', 'Super Admin', 1, 'Full system access across all branches', NOW()),
    ('b0000000-0000-0000-0000-000000000002', 'Branch Admin', 2, 'Branch manager with local administrative rights', NOW()),
    ('b0000000-0000-0000-0000-000000000003', 'Staff', 3, 'In-store cashier and operations staff', NOW())
ON CONFLICT ("Id") DO NOTHING;

-- 3. Insert Default Super Admin Staff (Password: Admin@123)
-- Hash placeholder for BCrypt/PBKDF2; in practice verified via API auth
INSERT INTO "StaffMembers" ("Id", "FullName", "Email", "PasswordHash", "IsActive", "CreatedAtUtc")
VALUES (
    'c0000000-0000-0000-0000-000000000001',
    'SJewls Head Office Admin',
    'admin@sjewls.lk',
    'AQAAAAIAAYagAAAAENK+n3pW9vW4fF+uU5L18gT9/wD5xM6kS1zP1p5g6o9WkQ==',
    true,
    NOW()
) ON CONFLICT ("Email") DO NOTHING;

-- Assign SuperAdmin Role & Jaffna Branch
INSERT INTO "StaffRoles" ("Id", "StaffId", "RoleId", "CreatedAtUtc")
VALUES (
    gen_random_uuid(),
    'c0000000-0000-0000-0000-000000000001',
    'b0000000-0000-0000-0000-000000000001',
    NOW()
) ON CONFLICT DO NOTHING;

INSERT INTO "StaffBranches" ("Id", "StaffId", "BranchId", "CreatedAtUtc")
VALUES (
    gen_random_uuid(),
    'c0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    NOW()
) ON CONFLICT DO NOTHING;

-- 4. Insert Initial Gold Rates (22K and 24K in LKR per gram)
INSERT INTO "GoldRates" ("Id", "BranchId", "Karat", "RatePerGram", "EffectiveFromUtc", "RecordedByStaffId", "CreatedAtUtc")
VALUES 
    (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000001', 22, 26500.0000, NOW(), 'c0000000-0000-0000-0000-000000000001', NOW()),
    (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000001', 24, 28800.0000, NOW(), 'c0000000-0000-0000-0000-000000000001', NOW())
ON CONFLICT DO NOTHING;

-- 5. Insert Sample Chitu Plan
INSERT INTO "ChituPlans" (
    "Id", "BranchId", "Code", "Name", "Description", 
    "MonthlyInstalmentAmount", "ChituBenefitValue", "TotalSlotCapacity", 
    "DurationMonths", "DueDayOfMonth", "StartDate", "Status", "CreatedAtUtc"
)
VALUES (
    'd0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    'CHITU-10K-2026',
    'Swarna Mithra 10K Monthly Chitu',
    'Save 10,000 LKR monthly for 10 months. Monthly physical lucky draw with winning slots receiving 100,000 LKR in gold jewellery immediately!',
    10000.0000,
    100000.0000,
    100,
    10,
    10,
    CURRENT_DATE,
    2, -- Active
    NOW()
) ON CONFLICT DO NOTHING;

-- 6. Insert Sample Jewellery Plans
INSERT INTO "JewelleryPlans" (
    "Id", "BranchId", "Code", "Name", "Description", "ImageUrl",
    "TargetProductGoldWeightGrams", "Karat", "TargetMakingCharges", "TargetVat", "TotalTargetAmount",
    "AllowedDurationsMonths", "CancellationFeeType", "CancellationFeeValue", "IsActive", "CreatedAtUtc"
)
VALUES (
    'e0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    'JEW-THALI-8G',
    'Traditional 22K Gold Thali Pendant (8 Grams)',
    'Handcrafted traditional Jaffna Thali pendant in authentic 22K gold. Flexible monthly gold savings over 6, 8, or 12 months with locked gold rates.',
    'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?q=80&w=800',
    8.0000,
    22,
    15000.0000,
    0.0000,
    227000.0000,
    ARRAY[6, 8, 12]::integer[],
    2, -- Percentage
    5.0000,
    true,
    NOW()
) ON CONFLICT DO NOTHING;
