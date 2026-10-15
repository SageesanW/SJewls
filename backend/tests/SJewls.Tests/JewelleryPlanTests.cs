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
        Assert.StartsWith("ENR-", item.EnrolmentReference);
    }

    [Fact]
    public async Task PlanTracking_LifetimeCountsVsTotalCollected_EnrolmentDoesNotIncreaseTotalCollected()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Tracking Metrics Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/tracking.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = new DateOnly(2025, 1, 1),
            TargetProductGoldWeightGrams = 20.0m
        }, superAdminId, "127.0.0.1");

        // Initial: 0 enrolments, 0 customers, 0 collected
        var initialDto = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.NotNull(initialDto);
        Assert.Equal(0, initialDto.LifetimeTotalEnrolmentsCount);
        Assert.Equal(0, initialDto.LifetimeUniqueCustomersCount);
        Assert.Equal(0m, initialDto.LifetimeTotalCollectedLkr);

        // Customer joins without making any payment
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Kamal Perera",
            PhoneNumber = "+94771234567",
            PrimaryBranchId = branch1Id,
            IsActive = true
        };
        db.Customers.Add(customer);

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = customer.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 1, 1),
            TargetEndDate = new DateOnly(2026, 1, 1),
            TargetProductWeightGrams = 20m,
            TotalSavedGrams = 0m,
            TotalContributedCurrency = 0m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        // Enrolment creation alone increases counts, but NOT total collected
        var afterEnrolDto = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.NotNull(afterEnrolDto);
        Assert.Equal(1, afterEnrolDto.LifetimeTotalEnrolmentsCount);
        Assert.Equal(1, afterEnrolDto.LifetimeUniqueCustomersCount);
        Assert.Equal(0m, afterEnrolDto.LifetimeTotalCollectedLkr);

        // Add a pending payment and a quote (MUST BE EXCLUDED)
        var pendingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 50000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Pending,
            ProviderReference = "TXN-PENDING"
        };
        db.Payments.Add(pendingPayment);

        var pendingContrib = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = pendingPayment.Id,
            CurrencyAmount = 50000m,
            GoldGrams = 1.5m,
            AppliedRatePerGram = 33000m,
            Karat = 22,
            ConfirmedAtUtc = DateTime.UtcNow
        };
        db.JewelleryContributions.Add(pendingContrib);
        await db.SaveChangesAsync();

        var afterPendingDto = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.Equal(0m, afterPendingDto!.LifetimeTotalCollectedLkr);

        // Add a confirmed successful payment
        var successPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 150000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "TXN-SUCCESS"
        };
        db.Payments.Add(successPayment);

        var successContrib = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = successPayment.Id,
            CurrencyAmount = 150000m,
            GoldGrams = 4.5m,
            AppliedRatePerGram = 33000m,
            Karat = 22,
            ConfirmedAtUtc = DateTime.UtcNow
        };
        db.JewelleryContributions.Add(successContrib);
        await db.SaveChangesAsync();

        var afterSuccessDto = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.Equal(150000m, afterSuccessDto!.LifetimeTotalCollectedLkr);
    }

    [Fact]
    public async Task PlanTracking_DateFiltering_JoiningDateAndPaymentConfirmationDateSemantics()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Date Filtered Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/datefilter.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = new DateOnly(2025, 1, 1),
            TargetProductGoldWeightGrams = 20.0m
        }, superAdminId, "127.0.0.1");

        // Customer 1 joined early in Jan 2025, but pays in March 2025
        var cust1 = new Customer { Id = Guid.NewGuid(), FullName = "Cust 1", PrimaryBranchId = branch1Id, IsActive = true };
        var enr1 = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = cust1.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 1, 10),
            TargetEndDate = new DateOnly(2026, 1, 10),
            TargetProductWeightGrams = 20m,
            Status = JewelleryEnrolmentStatus.Active
        };
        var pay1 = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = cust1.Id,
            BranchId = branch1Id,
            Amount = 100000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "TXN-1"
        };
        var contrib1 = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enr1.Id,
            PaymentId = pay1.Id,
            CurrencyAmount = 100000m,
            GoldGrams = 3m,
            ConfirmedAtUtc = new DateTime(2025, 3, 15, 10, 0, 0, DateTimeKind.Utc)
        };
        enr1.Contributions.Add(contrib1);

        // Customer 2 joined in March 2025 and also pays in March 2025
        var cust2 = new Customer { Id = Guid.NewGuid(), FullName = "Cust 2", PrimaryBranchId = branch1Id, IsActive = true };
        var enr2 = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = cust2.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 3, 5),
            TargetEndDate = new DateOnly(2026, 3, 5),
            TargetProductWeightGrams = 20m,
            Status = JewelleryEnrolmentStatus.Active
        };
        var pay2 = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = cust2.Id,
            BranchId = branch1Id,
            Amount = 200000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "TXN-2"
        };
        var contrib2 = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enr2.Id,
            PaymentId = pay2.Id,
            CurrencyAmount = 200000m,
            GoldGrams = 6m,
            ConfirmedAtUtc = new DateTime(2025, 3, 20, 14, 0, 0, DateTimeKind.Utc)
        };
        enr2.Contributions.Add(contrib2);

        db.Customers.AddRange(cust1, cust2);
        db.Payments.AddRange(pay1, pay2);
        db.JewelleryEnrolments.AddRange(enr1, enr2);
        await db.SaveChangesAsync();

        // Query with date range: 2025-03-01 to 2025-03-31
        var pagedResult = await service.GetPlansAsync(
            superAdminId, null, null, null, null, 1, 10,
            fromDate: new DateOnly(2025, 3, 1),
            toDate: new DateOnly(2025, 3, 31));

        var planDto = pagedResult.Items.First(p => p.Id == plan.Id);

        // Lifetime figures: both customers
        Assert.Equal(2, planDto.LifetimeTotalEnrolmentsCount);
        Assert.Equal(2, planDto.LifetimeUniqueCustomersCount);
        Assert.Equal(300000m, planDto.LifetimeTotalCollectedLkr);

        // Filtered enrolment counts (filtered by joining date: only cust2 joined in March):
        Assert.Equal(1, planDto.FilteredTotalEnrolmentsCount);
        Assert.Equal(1, planDto.FilteredUniqueCustomersCount);

        // Filtered collections (filtered by payment date: BOTH paid in March, including cust1 who joined earlier!):
        Assert.Equal(300000m, planDto.FilteredTotalCollectedLkr);
    }

    [Fact]
    public async Task PlanCustomers_MultipleEnrolmentsForSameCustomer_SeparateRowsAndMetrics()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Multi Enrolment Test Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/multi.png",
            AllowedDurationsMonths = new List<int> { 6, 12 },
            StartDate = new DateOnly(2025, 1, 1),
            TargetProductGoldWeightGrams = 20.0m
        }, superAdminId, "127.0.0.1");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Repeat Customer",
            PhoneNumber = "+94770001122",
            PrimaryBranchId = branch1Id,
            IsActive = true
        };
        db.Customers.Add(customer);

        // Enrolment 1: 12 months, active
        var enr1 = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = customer.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 1, 1),
            TargetEndDate = new DateOnly(2026, 1, 1),
            TargetProductWeightGrams = 20.0m,
            TotalSavedGrams = 10.0m,
            TotalContributedCurrency = 300000m,
            Status = JewelleryEnrolmentStatus.Active
        };

        // Enrolment 2: 6 months, completed (historical)
        var enr2 = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = customer.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 6,
            StartDate = new DateOnly(2024, 1, 1),
            TargetEndDate = new DateOnly(2024, 7, 1),
            TargetProductWeightGrams = 10.0m,
            TotalSavedGrams = 10.0m,
            TotalContributedCurrency = 280000m,
            Status = JewelleryEnrolmentStatus.Completed
        };

        db.JewelleryEnrolments.AddRange(enr1, enr2);
        await db.SaveChangesAsync();

        var planCustomers = await service.GetPlanCustomersAsync(plan.Id, superAdminId, null, null, 1, 10);

        // Must return two separate rows for the same customer
        Assert.Equal(2, planCustomers.Items.Count);
        var row1 = planCustomers.Items.First(x => x.EnrolmentId == enr1.Id);
        var row2 = planCustomers.Items.First(x => x.EnrolmentId == enr2.Id);

        Assert.Equal(enr1.Id, row1.EnrolmentId);
        Assert.Equal(12, row1.SelectedDurationMonths);
        Assert.Equal("Active", row1.EnrolmentStatus);
        Assert.Equal(20.0m, row1.EnrolmentTargetGrams);
        Assert.StartsWith("ENR-", row1.EnrolmentReference);

        Assert.Equal(enr2.Id, row2.EnrolmentId);
        Assert.Equal(6, row2.SelectedDurationMonths);
        Assert.Equal("Completed", row2.EnrolmentStatus);
        Assert.Equal(10.0m, row2.EnrolmentTargetGrams);
        Assert.StartsWith("ENR-", row2.EnrolmentReference);

        // Verify lifetime counts on the plan preserve historical enrolments
        var planDetails = await service.GetPlanByIdAsync(plan.Id, superAdminId);
        Assert.Equal(1, planDetails!.LifetimeUniqueCustomersCount);
        Assert.Equal(2, planDetails.LifetimeTotalEnrolmentsCount);
    }

    [Fact]
    public async Task GetPlanCustomers_ConfirmedContributions_AppearUnderCorrectEnrolment_WithPaymentReferenceAndMethod()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Diamond Necklace Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/diamond.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 25.0m
        }, superAdminId, "127.0.0.1");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Priya Ramanathan",
            PhoneNumber = "+94771234567",
            Email = "priya@example.com",
            PrimaryBranchId = branch1Id,
            IsActive = true
        };
        db.Customers.Add(customer);

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = customer.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 1, 1),
            TargetEndDate = new DateOnly(2026, 1, 1),
            TargetProductWeightGrams = 25.0m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);

        // Add 2 confirmed payments with payment references and methods
        var payment1 = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 140000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "PAY-REF-001",
            CompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-5)
        };
        var contrib1 = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = payment1.Id,
            CurrencyAmount = 140000m,
            GoldGrams = 5.0m,
            AppliedRatePerGram = 28000m,
            Karat = 24,
            ConfirmedAtUtc = DateTimeOffset.UtcNow.AddDays(-5)
        };

        var payment2 = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 280000m,
            Currency = "LKR",
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "PAY-REF-002",
            CompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var contrib2 = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = payment2.Id,
            CurrencyAmount = 280000m,
            GoldGrams = 10.0m,
            AppliedRatePerGram = 28000m,
            Karat = 24,
            ConfirmedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.Payments.AddRange(payment1, payment2);
        db.JewelleryContributions.AddRange(contrib1, contrib2);
        await db.SaveChangesAsync();

        var result = await service.GetPlanCustomersAsync(plan.Id, superAdminId, null, null, 1, 10);
        Assert.Single(result.Items);
        var item = result.Items[0];

        Assert.Equal(enrolment.Id, item.EnrolmentId);
        Assert.Equal("Priya Ramanathan", item.CustomerName);
        Assert.Equal("+94771234567", item.PhoneNumber);
        Assert.Equal("priya@example.com", item.Email);
        Assert.Equal(25.0m, item.EnrolmentTargetGrams);
        Assert.Equal(420000m, item.TotalConfirmedMoneyPaid); // 140,000 + 280,000
        Assert.Equal(15.0m, item.NetAccumulatedGrams); // 5.0 + 10.0
        Assert.Equal(10.0m, item.RemainingGrams); // 25.0 - 15.0
        Assert.Equal(60.0m, item.IndividualProgressPercentage); // 15.0 / 25.0 * 100

        // Contributions history verification
        Assert.Equal(2, item.Contributions.Count);
        var c1 = item.Contributions.First(c => c.PaymentReference == "PAY-REF-001");
        Assert.Equal(140000m, c1.CurrencyAmount);
        Assert.Equal(5.0m, c1.GoldGrams);
        Assert.Equal("MockOnline", c1.PaymentMethod);
        Assert.Equal("Succeeded", c1.Status);

        var c2 = item.Contributions.First(c => c.PaymentReference == "PAY-REF-002");
        Assert.Equal(280000m, c2.CurrencyAmount);
        Assert.Equal(10.0m, c2.GoldGrams);
        Assert.Equal("Cash", c2.PaymentMethod);
        Assert.Equal("Succeeded", c2.Status);

        // Plan overall aggregates
        Assert.Equal(420000m, result.TotalCollectedCurrency);
        Assert.Equal(60.0m, result.OverallProgressPercentage);
    }

    [Fact]
    public async Task GetPlanCustomers_UnconfirmedAndPendingPayments_DoNotIncreaseTotalsOrProgress()
    {
        var (db, superAdminId, _, branch1Id, _, cat1Id) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        var plan = await service.CreatePlanAsync(new CreateJewelleryPlanRequest
        {
            BranchId = branch1Id,
            CategoryId = cat1Id,
            Name = "Gold Bangle Plan",
            ImageUrl = "https://storage.sjewls.lk/plans/bangle.png",
            AllowedDurationsMonths = new List<int> { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetProductGoldWeightGrams = 20.0m
        }, superAdminId, "127.0.0.1");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Nimal Perera",
            PhoneNumber = "+94781234567",
            PrimaryBranchId = branch1Id,
            IsActive = true
        };
        db.Customers.Add(customer);

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = plan.Id,
            CustomerId = customer.Id,
            BranchId = branch1Id,
            SelectedDurationMonths = 12,
            StartDate = new DateOnly(2025, 1, 1),
            TargetEndDate = new DateOnly(2026, 1, 1),
            TargetProductWeightGrams = 20.0m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);

        // 1 Confirmed Succeeded Payment (5g, 150,000)
        var succPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 150000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "CONFIRMED-001"
        };
        var succContrib = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = succPayment.Id,
            CurrencyAmount = 150000m,
            GoldGrams = 5.0m,
            AppliedRatePerGram = 30000m,
            Karat = 24,
            ConfirmedAtUtc = DateTimeOffset.UtcNow
        };

        // 1 Pending Payment (3g, 90,000) - MUST BE EXCLUDED from progress and collected total
        var pendingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 90000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Pending,
            ProviderReference = "PENDING-001"
        };
        var pendingContrib = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = pendingPayment.Id,
            CurrencyAmount = 90000m,
            GoldGrams = 3.0m,
            AppliedRatePerGram = 30000m,
            Karat = 24,
            ConfirmedAtUtc = DateTimeOffset.UtcNow
        };

        // 1 Failed Payment (2g, 60,000) - MUST BE EXCLUDED from progress and collected total
        var failedPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch1Id,
            Amount = 60000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Failed,
            ProviderReference = "FAILED-001"
        };
        var failedContrib = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            PaymentId = failedPayment.Id,
            CurrencyAmount = 60000m,
            GoldGrams = 2.0m,
            AppliedRatePerGram = 30000m,
            Karat = 24,
            ConfirmedAtUtc = DateTimeOffset.UtcNow
        };

        db.Payments.AddRange(succPayment, pendingPayment, failedPayment);
        db.JewelleryContributions.AddRange(succContrib, pendingContrib, failedContrib);
        await db.SaveChangesAsync();

        var result = await service.GetPlanCustomersAsync(plan.Id, superAdminId, null, null, 1, 10);
        Assert.Single(result.Items);
        var item = result.Items[0];

        // TotalConfirmedMoneyPaid and NetAccumulatedGrams only include Succeeded
        Assert.Equal(150000m, item.TotalConfirmedMoneyPaid);
        Assert.Equal(5.0m, item.NetAccumulatedGrams);
        Assert.Equal(15.0m, item.RemainingGrams);
        Assert.Equal(25.0m, item.IndividualProgressPercentage); // 5 / 20 * 100

        // Plan overall aggregates also strictly exclude pending and failed
        Assert.Equal(150000m, result.TotalCollectedCurrency);
        Assert.Equal(25.0m, result.OverallProgressPercentage);

        // However, all 3 contributions are listed in history for staff audit with their actual statuses
        Assert.Equal(3, item.Contributions.Count);
        Assert.Contains(item.Contributions, c => c.Status == "Pending" && c.PaymentReference == "PENDING-001");
        Assert.Contains(item.Contributions, c => c.Status == "Failed" && c.PaymentReference == "FAILED-001");
        Assert.Contains(item.Contributions, c => c.Status == "Succeeded" && c.PaymentReference == "CONFIRMED-001");
    }

    [Fact]
    public async Task GetPlanCustomers_BranchStaff_CannotAccessOtherBranchPlan()
    {
        var (db, _, branchStaffId, branch1Id, branch2Id, _) = SetupTestDatabase();
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new JewelleryPlanService(db, auditService, NullLogger<JewelleryPlanService>.Instance);

        // Create category and plan in Branch 2
        var cat2 = new JewelleryPlanCategory
        {
            Id = Guid.NewGuid(),
            BranchId = branch2Id,
            Name = "Colombo Rings",
            NormalizedName = "COLOMBO RINGS",
            IsActive = true
        };
        db.JewelleryCategories.Add(cat2);

        var planBranch2 = new JewelleryPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch2Id,
            CategoryId = cat2.Id,
            Name = "Colombo Exclusive Plan",
            Code = "COL-EXCL",
            TargetProductGoldWeightGrams = 30.0m,
            IsActive = true,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        db.JewelleryPlans.Add(planBranch2);
        await db.SaveChangesAsync();

        // Branch 1 staff attempts to query plan from Branch 2
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetPlanCustomersAsync(planBranch2.Id, branchStaffId, null, null, 1, 10));
    }
}
