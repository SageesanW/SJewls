using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class JewelleryPlanTests
{
    private (SJewlsDbContext db, Guid superAdminId, Guid branchStaffId, Guid branch1Id, Guid branch2Id, Guid categoryBranch1Id) SetupTestDatabase()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new SJewlsDbContext(options);

        // Seed Branches
        var branch1 = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "JAF-01",
            Name = "Jaffna Branch",
            Currency = "LKR",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        var branch2 = new Branch
        {
            Id = Guid.NewGuid(),
            Code = "COL-01",
            Name = "Colombo Branch",
            Currency = "LKR",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        db.Branches.AddRange(branch1, branch2);

        // Seed Roles
        var superAdminRole = new Role { Id = Guid.NewGuid(), Name = "Super Admin" };
        var staffRole = new Role { Id = Guid.NewGuid(), Name = "Staff" };
        db.Roles.AddRange(superAdminRole, staffRole);

        // Seed Super Admin
        var superAdmin = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Super Administrator",
            Email = "superadmin@sjewls.lk",
            IsActive = true
        };
        db.StaffMembers.Add(superAdmin);
        db.StaffRoles.Add(new StaffRole { Id = Guid.NewGuid(), StaffId = superAdmin.Id, RoleId = superAdminRole.Id, Role = superAdminRole });
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = superAdmin.Id, BranchId = branch1.Id });
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = superAdmin.Id, BranchId = branch2.Id });

        // Seed Branch Staff (assigned only to branch1)
        var branchStaff = new Staff
        {
            Id = Guid.NewGuid(),
            FullName = "Jaffna Staff",
            Email = "staff@sjewls.lk",
            IsActive = true
        };
        db.StaffMembers.Add(branchStaff);
        db.StaffRoles.Add(new StaffRole { Id = Guid.NewGuid(), StaffId = branchStaff.Id, RoleId = staffRole.Id, Role = staffRole });
        db.StaffBranches.Add(new StaffBranch { Id = Guid.NewGuid(), StaffId = branchStaff.Id, BranchId = branch1.Id });

        // Seed Category in Branch 1
        var categoryBranch1 = new JewelleryPlanCategory
        {
            Id = Guid.NewGuid(),
            BranchId = branch1.Id,
            Name = "Bridal",
            NormalizedName = "BRIDAL",
            IsActive = true
        };
        db.JewelleryCategories.Add(categoryBranch1);

        // Seed 24K Gold Rate in Branch 1
        var goldRate1 = new GoldRate
        {
            Id = Guid.NewGuid(),
            BranchId = branch1.Id,
            Karat = 24,
            RatePerGram = 28500.00m,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            RecordedByStaffId = superAdmin.Id
        };
        db.GoldRates.Add(goldRate1);

        db.SaveChanges();

        return (db, superAdmin.Id, branchStaff.Id, branch1.Id, branch2.Id, categoryBranch1.Id);
    }

    [Fact]
    public async Task CreatePlan_ValidDefaultDurations_CreatesSuccessfully()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var request = new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "18 Sovereign Thali Kodi",
            Description = "Traditional wedding thali kodi collection.",
            ImageUrl = "https://storage.sjewls.lk/plans/thali.png",
            AllowedDurationsMonths = new List<int> { 6, 12, 18 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 40.0m
        };

        var created = await service.CreatePlanAsync(request, superAdminId, "127.0.0.1");

        Assert.NotNull(created);
        Assert.Equal("18 Sovereign Thali Kodi", created.Name);
        Assert.Equal(40.0m, created.TargetProductGoldWeightGrams);
        Assert.Equal(new[] { 6, 12, 18 }, created.AllowedDurationsMonths);
        Assert.Equal("Active", created.DisplayStatus);
        Assert.StartsWith("JP-JAF-01-", created.Code);
    }

    [Fact]
    public async Task CreatePlan_CustomDurations_DeduplicatesAndSorts()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var request = new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Custom Flexi Gold",
            ImageUrl = "https://storage.sjewls.lk/plans/flexi.png",
            AllowedDurationsMonths = new List<int> { 18, 9, 6, 18, 9 }, // duplicate 18, 9 and unordered
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 25.5m
        };

        var created = await service.CreatePlanAsync(request, superAdminId, "127.0.0.1");

        Assert.Equal(new[] { 6, 9, 18 }, created.AllowedDurationsMonths);
    }

    [Fact]
    public async Task CreatePlan_InvalidOrEmptyDurations_ThrowsArgumentException()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var reqEmpty = new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Invalid Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/invalid.png",
            AllowedDurationsMonths = new List<int>(),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 10.0m
        };

        var reqNegative = new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Invalid Plan 2",
            ImageUrl = "https://storage.sjewls.lk/plans/invalid.png",
            AllowedDurationsMonths = new List<int> { 0, -5 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 10.0m
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePlanAsync(reqEmpty, superAdminId, "127.0.0.1"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePlanAsync(reqNegative, superAdminId, "127.0.0.1"));
    }

    [Fact]
    public async Task CreatePlan_BranchStaffCannotCreateInAnotherBranch_ThrowsUnauthorized()
    {
        var (db, _, branchStaffId, _, branch2Id, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var request = new CreateJewelleryPlanRequest
        {
            BranchId = branch2Id, // Staff is only in branch 1
            CategoryId = cat1Id,
            Name = "Branch 2 Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/b2.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 15.0m
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreatePlanAsync(request, branchStaffId, "127.0.0.1"));
    }

    [Fact]
    public async Task CreatePlan_CategoryFromDifferentBranch_ThrowsArgumentException()
    {
        var (db, superAdminId, _, _, branch2Id, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        // cat1Id is in branch 1, but we are creating plan for branch 2
        var request = new CreateJewelleryPlanRequest
        {
            BranchId = branch2Id,
            CategoryId = cat1Id,
            Name = "Mismatched Category Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/mismatch.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 20.0m
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePlanAsync(request, superAdminId, "127.0.0.1"));
    }

    [Fact]
    public async Task ScheduledPlan_FutureStartDate_ShowsScheduledStatus()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var request = new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "New Year Special Gold",
            ImageUrl = "https://storage.sjewls.lk/plans/ny.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = futureDate,
            TargetProductGoldWeightGrams = 50.0m
        };

        var created = await service.CreatePlanAsync(request, superAdminId, "127.0.0.1");

        Assert.Equal("Scheduled", created.DisplayStatus);

        // Fetch plan and confirm
        var fetched = await service.GetPlanByIdAsync(created.Id, superAdminId);
        Assert.NotNull(fetched);
        Assert.Equal("Scheduled", fetched.DisplayStatus);
    }

    [Fact]
    public async Task DeactivateAndReopenPlan_TransitionsStatusCorrectly()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var request = new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Daily Wear Flexi",
            ImageUrl = "https://storage.sjewls.lk/plans/daily.png",
            AllowedDurationsMonths = new List<int> { 6, 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 10.0m
        };

        var created = await service.CreatePlanAsync(request, superAdminId, "127.0.0.1");
        Assert.Equal("Active", created.DisplayStatus);

        // Deactivate
        var deactivated = await service.UpdatePlanStatusAsync(created.Id, new UpdateJewelleryPlanStatusRequest
        {
            Action = "Deactivate",
            Reason = "Seasonal plan closed for new enrolments"
        }, superAdminId, "127.0.0.1");

        Assert.Equal("Deactivated", deactivated.DisplayStatus);
        Assert.False(deactivated.IsActive);
        Assert.NotNull(deactivated.DeactivatedAtUtc);

        // Reopen
        var reopened = await service.UpdatePlanStatusAsync(created.Id, new UpdateJewelleryPlanStatusRequest
        {
            Action = "Reopen",
            Reason = "Reopening for festive season"
        }, superAdminId, "127.0.0.1");

        Assert.Equal("Active", reopened.DisplayStatus);
        Assert.True(reopened.IsActive);
        Assert.NotNull(reopened.ReopenedAtUtc);
    }

    [Fact]
    public async Task UpdatePlan_PreservesExistingEnrolmentsTerms()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var created = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Classic Saver",
            ImageUrl = "https://storage.sjewls.lk/plans/classic.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            TargetProductGoldWeightGrams = 20.0m
        }, superAdminId, "127.0.0.1");

        // Customer enrols in the plan with target 20.0g and duration 12 months
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Kavitha Rajan",
            PhoneNumber = "+94771234567",
            Email = "kavitha@example.com",
            PrimaryBranchId = branch1Id,
            IsActive = true
        };
        db.Customers.Add(customer);

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = created.Id,
            CustomerId = customer.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 1, 1),
            TargetEndDate = new DateOnly(2026, 1, 1),
            TargetProductWeightGrams = 20.0m, // snapshot
            TotalSavedGrams = 5.0m,
            TotalContributedCurrency = 142500.00m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        // Admin updates the plan target to 30.0g and durations to 6 and 18 months
        var updated = await service.UpdatePlanAsync(created.Id, new UpdateJewelleryPlanRequest
        {
            CategoryId = cat1Id,
            Name = "Classic Saver Enhanced",
            ImageUrl = "https://storage.sjewls.lk/plans/classic-v2.png",
            AllowedDurationsMonths = new List<int> { 6, 18 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 30.0m
        }, superAdminId, "127.0.0.1");

        Assert.Equal(30.0m, updated.TargetProductGoldWeightGrams);
        Assert.Equal(new[] { 6, 18 }, updated.AllowedDurationsMonths);

        // Verify existing customer enrolment snapshot was preserved untouched
        var enrolmentInDb = await db.JewelleryEnrolments.FindAsync(enrolment.Id);
        Assert.NotNull(enrolmentInDb);
        Assert.Equal(20.0m, enrolmentInDb.TargetProductWeightGrams);
        Assert.Equal(12, enrolmentInDb.SelectedDurationMonths);
        Assert.Equal(new DateOnly(2025, 1, 1), enrolmentInDb.StartDate);
        Assert.Equal(new DateOnly(2026, 1, 1), enrolmentInDb.TargetEndDate);
    }

    [Fact]
    public async Task ProgressCalculation_ZeroAndMultipleEnrolments()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Bridal Gold Saver",
            ImageUrl = "https://storage.sjewls.lk/plans/bridal.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            TargetProductGoldWeightGrams = 50.0m
        }, superAdminId, "127.0.0.1");

        // 0 enrolments => 0% progress
        var planWithZero = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.NotNull(planWithZero);
        Assert.Equal(0m, planWithZero.OverallProgressPercentage);
        Assert.Equal(0, planWithZero.TotalEnrolments);

        // Add 2 customer enrolments:
        // Customer 1: target snapshot 50g, saved 15g
        // Customer 2: target snapshot 50g, saved 22g
        // Total saved = 37g, Total targets across enrolments = 100g => 37.0%
        var cust1 = new Customer { Id = Guid.NewGuid(), FullName = "C1", PrimaryBranchId = branch1Id, IsActive = true };
        var cust2 = new Customer { Id = Guid.NewGuid(), FullName = "C2", PrimaryBranchId = branch1Id, IsActive = true };
        db.Customers.AddRange(cust1, cust2);

        db.JewelleryEnrolments.AddRange(
            new JewelleryEnrolment
            {
                Id = Guid.NewGuid(),
                JewelleryPlanId = plan.Id,
                CustomerId = cust1.Id,
                BranchId = branch1Id,
                SelectedDurationMonths = 12,
                StartDate = new DateOnly(2025, 1, 1),
                TargetEndDate = new DateOnly(2026, 1, 1),
                TargetProductWeightGrams = 50.0m,
                TotalSavedGrams = 15.0m,
                Status = JewelleryEnrolmentStatus.Active
            },
            new JewelleryEnrolment
            {
                Id = Guid.NewGuid(),
                JewelleryPlanId = plan.Id,
                CustomerId = cust2.Id,
                BranchId = branch1Id,
                SelectedDurationMonths = 12,
                StartDate = new DateOnly(2025, 2, 1),
                TargetEndDate = new DateOnly(2026, 2, 1),
                TargetProductWeightGrams = 50.0m,
                TotalSavedGrams = 22.0m,
                Status = JewelleryEnrolmentStatus.Active
            }
        );
        await db.SaveChangesAsync();

        var planWithEnrolments = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.NotNull(planWithEnrolments);
        Assert.Equal(2, planWithEnrolments.TotalEnrolments);
        Assert.Equal(100.0m, planWithEnrolments.TotalTargetGramsAcrossEnrolments);
        Assert.Equal(37.0m, planWithEnrolments.TotalNetAccumulatedGrams);
        Assert.Equal(37.0m, planWithEnrolments.OverallProgressPercentage);
    }

    [Fact]
    public async Task FinancialQuote_ByGramsAndByMoney_CalculatesWithPrecision()
    {
        var (db, superAdminId, _, branch1Id, _, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        // Rate is 28,500 LKR / gram
        // 1. By Grams: 2.5000g => 2.5 * 28,500 = 71,250.00 LKR
        var quoteGrams = await service.CalculateFinancialQuoteAsync(new FinancialQuoteRequest
        {
            BranchId = branch1Id,
            Mode = "ByGrams",
            InputGrams = 2.5m
        }, superAdminId);

        Assert.Equal(28500m, quoteGrams.RatePerGram);
        Assert.Equal(71250.00m, quoteGrams.PayableMoney);
        Assert.Equal(2.5m, quoteGrams.CreditedGrams);

        // 2. By Money: 50,000 LKR => 50,000 / 28,500 = 1.754385... => floored to 1.7543g
        var quoteMoney = await service.CalculateFinancialQuoteAsync(new FinancialQuoteRequest
        {
            BranchId = branch1Id,
            Mode = "ByMoney",
            InputMoney = 50000.00m
        }, superAdminId);

        Assert.Equal(50000.00m, quoteMoney.PayableMoney);
        Assert.Equal(1.7543m, quoteMoney.CreditedGrams);
    }

    [Fact]
    public async Task FinancialQuote_WhenRemainingBalanceExceeded_FlagsWillExceedTarget()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Small Pendant",
            ImageUrl = "https://storage.sjewls.lk/plans/pendant.png",
            AllowedDurationsMonths = new List<int> { 6 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 5.0m
        }, superAdminId, "127.0.0.1");

        var cust = new Customer { Id = Guid.NewGuid(), FullName = "Niroshan", PrimaryBranchId = branch1Id, IsActive = true };
        db.Customers.Add(cust);

        // Customer has saved 4.5g out of 5.0g target => 0.5g remaining
        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = cust.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 6,
            StartDate = new DateOnly(2025, 1, 1),
            TargetEndDate = new DateOnly(2025, 7, 1),
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.5m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        // Customer asks to contribute 1.0g (exceeds 0.5g remaining)
        var quote = await service.CalculateFinancialQuoteAsync(new FinancialQuoteRequest
        {
            BranchId = branch1Id,
            EnrolmentId = enrolment.Id,
            Mode = "ByGrams",
            InputGrams = 1.0m
        }, superAdminId);

        Assert.True(quote.WillExceedTarget);
        Assert.Equal(0.5m, quote.RemainingTargetGrams);
    }

    [Fact]
    public async Task FinancialQuote_MissingGoldRate_ThrowsInvalidOperationException()
    {
        var (db, superAdminId, _, _, branch2Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        // Branch 2 has no 24K gold rate configured
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CalculateFinancialQuoteAsync(
            new FinancialQuoteRequest
            {
                BranchId = branch2Id,
                Mode = "ByGrams",
                InputGrams = 5.0m
            }, superAdminId));
    }

    [Fact]
    public async Task GetPlanCustomers_HonestEmptyStateAndDetails()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Empty Plan Customers Test",
            ImageUrl = "https://storage.sjewls.lk/plans/empty.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 20.0m
        }, superAdminId, "127.0.0.1");

        // When plan has no enrolments, returns honest empty state with 0 count
        var emptyCustomers = await service.GetPlanCustomersAsync(plan.Id, superAdminId, null, null, 1, 10);
        Assert.NotNull(emptyCustomers);
        Assert.Empty(emptyCustomers.Items);
        Assert.Equal(0, emptyCustomers.TotalCount);

        // Add an enrolled customer
        var cust = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Amara Silva",
            PhoneNumber = "+94719876543",
            Email = "amara@example.com",
            PrimaryBranchId = branch1Id,
            IsActive = true
        };
        db.Customers.Add(cust);

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = cust.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 3, 1),
            TargetEndDate = new DateOnly(2026, 3, 1),
            TargetProductWeightGrams = 20.0m,
            TotalSavedGrams = 8.0m,
            TotalContributedCurrency = 228000.00m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var populatedCustomers = await service.GetPlanCustomersAsync(plan.Id, superAdminId, null, null, 1, 10);
        Assert.Single(populatedCustomers.Items);
        var item = populatedCustomers.Items[0];
        Assert.Equal("Amara Silva", item.CustomerName);
        Assert.Equal("+94719876543", item.PhoneNumber);
        Assert.Equal("amara@example.com", item.Email);
        Assert.Equal(20.0m, item.EnrolmentTargetGrams);
        Assert.Equal(8.0m, item.NetAccumulatedGrams);
        Assert.Equal(12.0m, item.RemainingGrams);
        Assert.Equal(40.0m, item.IndividualProgressPercentage); // 8/20 * 100 = 40%
        Assert.Equal("Active", item.EnrolmentStatus);
    }
}
