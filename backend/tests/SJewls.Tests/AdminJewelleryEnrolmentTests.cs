using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class AdminJewelleryEnrolmentTests
{
    private SJewlsDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SJewlsDbContext(options);
    }

    private class MockGoldRateService : IGoldRateService
    {
        public decimal MockRate { get; set; } = 30000m;

        public Task<ApplicableGoldRateDto> GetApplicable24kRateAsync(Guid branchId, CancellationToken ct = default)
        {
            return Task.FromResult(new ApplicableGoldRateDto
            {
                RatePerGram = MockRate,
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                SourceTimestampUtc = DateTimeOffset.UtcNow,
                Source = "GoldAPI.io (Mock)",
                Currency = "LKR",
                IsFresh = true
            });
        }
    }

    private async Task<(Branch branch, Staff staff, Customer customer, JewelleryPlan plan)> SetupBranchStaffCustomerAndPlanAsync(SJewlsDbContext db)
    {
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Colombo Main Branch",
            Code = "COL-01",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        db.Branches.Add(branch);

        var superAdminRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "Super Admin",
            Description = "Full Access"
        };
        db.Roles.Add(superAdminRole);

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Username = "superadmin",
            Email = "admin@sjewls.lk",
            FullName = "Super Admin",
            PasswordHash = "hash",
            IsActive = true
        };
        db.StaffMembers.Add(staff);

        var staffRole = new StaffRole
        {
            StaffId = staff.Id,
            RoleId = superAdminRole.Id,
            Staff = staff,
            Role = superAdminRole
        };
        db.StaffRoles.Add(staffRole);
        staff.Roles.Add(staffRole);

        var staffBranch = new StaffBranch
        {
            StaffId = staff.Id,
            BranchId = branch.Id,
            Staff = staff,
            Branch = branch
        };
        db.StaffBranches.Add(staffBranch);
        staff.AssignedBranches.Add(staffBranch);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Nimali Fernando",
            PhoneNumber = "+94771234567",
            Email = "nimali@example.com",
            Nic = "199012345678",
            PrimaryBranchId = branch.Id,
            PrimaryBranch = branch,
            IsActive = true,
            IsPhoneVerified = false,
            IsEmailVerified = false
        };
        db.Customers.Add(customer);

        var category = new JewelleryPlanCategory
        {
            Id = Guid.NewGuid(),
            Name = "Bridal Collection",
            BranchId = branch.Id,
            Branch = branch,
            IsActive = true
        };
        db.JewelleryCategories.Add(category);

        var plan = new JewelleryPlan
        {
            Id = Guid.NewGuid(),
            Code = "BRD-01",
            Name = "Bridal Gold Saver",
            CategoryId = category.Id,
            Category = category,
            BranchId = branch.Id,
            Branch = branch,
            TargetProductGoldWeightGrams = 10.0m,
            AllowedDurationsMonths = new[] { 6, 12, 24 },
            PremiumPercentage = 5.0m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            IsActive = true
        };
        db.JewelleryPlans.Add(plan);

        await db.SaveChangesAsync();

        return (branch, staff, customer, plan);
    }

    [Fact]
    public async Task PreviewAssignment_ValidCustomerAndPlan_ReturnsEligibleAndRepeatCounts()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewAssignment_ValidCustomerAndPlan_ReturnsEligibleAndRepeatCounts));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService();
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var preview = await service.PreviewAssignmentAsync(staff.Id, new AdminEnrolmentPreviewRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id
        });

        Assert.True(preview.IsEligible);
        Assert.Null(preview.IneligibilityReason);
        Assert.Equal(10.0m, preview.TargetProductGoldWeightGrams);
        Assert.Equal(5.0m, preview.PremiumPercentage);
        Assert.Contains(6, preview.AllowedDurationsMonths);
        Assert.Contains(12, preview.AllowedDurationsMonths);
        Assert.False(preview.RequiresRepeatConfirmation);
        Assert.Equal(0, preview.TotalExistingEnrolments);
    }

    [Fact]
    public async Task AssignPlan_ValidInputs_CreatesActiveEnrolment_WithPlanTargetSnapshotAndNotification()
    {
        var db = CreateInMemoryDbContext(nameof(AssignPlan_ValidInputs_CreatesActiveEnrolment_WithPlanTargetSnapshotAndNotification));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService();
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var result = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12,
            IdempotencyKey = "ENR-TEST-001"
        }, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal(10.0m, result.TargetWeightGrams);
        Assert.Equal(12, result.SelectedDurationMonths);
        Assert.Equal(0m, result.TotalSavedGrams);
        Assert.Equal(10.0m, result.RemainingGrams);
        Assert.Equal(0m, result.ProgressPercentage);
        Assert.Equal("Active", result.Status);

        // Verify notification persisted
        var notifications = await db.Notifications.Where(n => n.CustomerId == customer.Id).ToListAsync();
        Assert.Single(notifications);
        Assert.Equal("Jewellery Plan Activated", notifications[0].Title);

        // Verify audit log persisted
        var auditLogs = await db.AuditLogs.Where(a => a.TargetEntity == "JewelleryEnrolment").ToListAsync();
        Assert.Single(auditLogs);
        Assert.Equal("AssignJewelleryPlan", auditLogs[0].Action);

        // Verify customer contact verification was NOT modified
        var reloadedCustomer = await db.Customers.FindAsync(customer.Id);
        Assert.False(reloadedCustomer!.IsPhoneVerified);
        Assert.False(reloadedCustomer.IsEmailVerified);
    }

    [Fact]
    public async Task AssignPlan_RepeatEnrolmentWithoutConfirmation_ThrowsRepeatEnrolmentConfirmationRequiredException()
    {
        var db = CreateInMemoryDbContext(nameof(AssignPlan_RepeatEnrolmentWithoutConfirmation_ThrowsRepeatEnrolmentConfirmationRequiredException));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService();
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        // First assignment succeeds
        await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 6,
            ConfirmRepeatEnrolment = false
        }, null);

        // Second assignment without confirmation must throw RepeatEnrolmentConfirmationRequiredException
        var ex = await Assert.ThrowsAsync<RepeatEnrolmentConfirmationRequiredException>(() =>
            service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
            {
                CustomerId = customer.Id,
                PlanId = plan.Id,
                SelectedDurationMonths = 12,
                ConfirmRepeatEnrolment = false
            }, null));

        Assert.Equal(1, ex.TotalEnrolmentCount);
        Assert.Equal(1, ex.ActiveEnrolmentCount);
    }

    [Fact]
    public async Task AssignPlan_RepeatEnrolmentWithConfirmation_CreatesSeparateEnrolmentRow()
    {
        var db = CreateInMemoryDbContext(nameof(AssignPlan_RepeatEnrolmentWithConfirmation_CreatesSeparateEnrolmentRow));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService();
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var enr1 = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 6
        }, null);

        var enr2 = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12,
            ConfirmRepeatEnrolment = true
        }, null);

        Assert.NotEqual(enr1.Id, enr2.Id);
        var totalInDb = await db.JewelleryEnrolments.CountAsync(e => e.CustomerId == customer.Id);
        Assert.Equal(2, totalInDb);
    }

    [Fact]
    public async Task PreviewPayment_BuyAmountAndBuyGram_CalculatesBreakdownWith5PercentPremium()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewPayment_BuyAmountAndBuyGram_CalculatesBreakdownWith5PercentPremium));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var enrolment = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        }, null);

        // Buy Gram test: 0.5g with 5% premium at rate 25,000 -> payable 0.525g * 25000 = 13,125 LKR, credited 0.5g
        var previewGram = await service.PreviewPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentPreviewRequest
        {
            Mode = "BuyGram",
            RequestedGrams = 0.5m
        });

        Assert.Equal(0.5m, previewGram.NetCreditedGrams);
        Assert.Equal(0.525m, previewGram.ChargeableGoldGrams);
        Assert.Equal(13125.00m, previewGram.AmountPayable);
        Assert.Equal(9.5m, previewGram.RemainingGramsAfterPayment);
        Assert.False(previewGram.WillReachCompletion);

        // Buy Amount test: 50,000 LKR at rate 25,000 -> base 2g, 5% premium = 0.1g, credit = 1.9g
        var previewAmount = await service.PreviewPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentPreviewRequest
        {
            Mode = "BuyAmount",
            EnteredAmount = 50000m
        });

        Assert.Equal(1.9m, previewAmount.NetCreditedGrams);
        Assert.Equal(50000.00m, previewAmount.AmountPayable);
        Assert.Equal(8.1m, previewAmount.RemainingGramsAfterPayment);

        // Initial preview without amount or grams: returns fresh live rate and enrolment context
        var initialPreview = await service.PreviewPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentPreviewRequest
        {
            Mode = "BuyAmount"
        });

        Assert.Equal(25000m, initialPreview.RatePerGram);
        Assert.Equal("GoldAPI.io (Mock)", initialPreview.RateSource);
        Assert.Equal(0m, initialPreview.NetCreditedGrams);
        Assert.Equal(0m, initialPreview.AmountPayable);
        Assert.Equal(10.0m, initialPreview.RemainingGramsBeforePayment);
        Assert.Equal(5.0m, initialPreview.PremiumPercentage);
        Assert.False(initialPreview.WillReachCompletion);
        Assert.False(initialPreview.WillExceedTarget);
    }

    [Fact]
    public async Task RecordPayment_BuyAmount_AtomicallyUpdatesBalances_AndCreatesCashPayment()
    {
        var db = CreateInMemoryDbContext(nameof(RecordPayment_BuyAmount_AtomicallyUpdatesBalances_AndCreatesCashPayment));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var enrolment = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        }, null);

        var paymentResult = await service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyAmount",
            EnteredAmount = 50000m,
            ConfirmedReceived = true,
            Notes = "Cash received counter 1"
        }, "127.0.0.1");

        Assert.True(paymentResult.Success);
        Assert.Equal(50000m, paymentResult.AmountPaid);
        Assert.Equal(1.9m, paymentResult.NetCreditedGrams);
        Assert.Equal(1.9m, paymentResult.NewTotalSavedGrams);
        Assert.Equal(8.1m, paymentResult.RemainingGrams);
        Assert.Equal(19.0m, paymentResult.ProgressPercentage);
        Assert.False(paymentResult.PlanCompleted);

        // Check Payment entity in db
        var paymentInDb = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentResult.PaymentId);
        Assert.NotNull(paymentInDb);
        Assert.Equal(PaymentMethod.Cash, paymentInDb.Method);
        Assert.Equal(PaymentStatus.Succeeded, paymentInDb.Status);
        Assert.Equal(staff.Id, paymentInDb.RecordedByStaffId);

        // Check contribution in db
        var contrib = await db.JewelleryContributions.FirstOrDefaultAsync(c => c.PaymentId == paymentResult.PaymentId);
        Assert.NotNull(contrib);
        Assert.Equal(1.9m, contrib.GoldGrams);
        Assert.Equal(50000m, contrib.CurrencyAmount);
        Assert.Equal(25000m, contrib.AppliedRatePerGram);
    }

    [Fact]
    public async Task RecordPayment_ExceedingRemainingTarget_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext(nameof(RecordPayment_ExceedingRemainingTarget_ThrowsArgumentException));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var enrolment = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        }, null);

        // Enrolment target is 10g. Attempting to buy 15g must fail.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
            {
                Mode = "BuyGram",
                RequestedGrams = 15.0m,
                ConfirmedReceived = true
            }, null));

        Assert.Contains("exceeds the remaining target", ex.Message);
    }

    [Fact]
    public async Task RecordPayment_ReachingTarget_MarksEnrolmentCompleted_AndFiresCompletionNotification()
    {
        var db = CreateInMemoryDbContext(nameof(RecordPayment_ReachingTarget_MarksEnrolmentCompleted_AndFiresCompletionNotification));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var enrolment = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        }, null);

        // Target is 10g. Record payment of exactly 10g via BuyGram
        var result = await service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyGram",
            RequestedGrams = 10.0m,
            ConfirmedReceived = true
        }, null);

        Assert.True(result.PlanCompleted);
        Assert.Equal(10.0m, result.NewTotalSavedGrams);
        Assert.Equal(0m, result.RemainingGrams);
        Assert.Equal(100.0m, result.ProgressPercentage);

        var dbEnrolment = await db.JewelleryEnrolments.FindAsync(enrolment.Id);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, dbEnrolment!.Status);

        // Verify completion notification
        var notifications = await db.Notifications.Where(n => n.CustomerId == customer.Id && n.Type == "completion").ToListAsync();
        Assert.Single(notifications);
    }

    [Fact]
    public async Task RecordPayment_NotConfirmedReceived_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext(nameof(RecordPayment_NotConfirmedReceived_ThrowsArgumentException));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        var enrolment = await service.AssignPlanAsync(staff.Id, new AdminAssignPlanRequest
        {
            CustomerId = customer.Id,
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        }, null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
            {
                Mode = "BuyAmount",
                EnteredAmount = 10000m,
                ConfirmedReceived = false // Staff didn't confirm
            }, null));

        Assert.Contains("confirm receipt", ex.Message);
    }
}
