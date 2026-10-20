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

    [Fact]
    public async Task GetJewelleryPaymentsPaged_SeparatesConfirmedAndPending_WithStableInstalmentNumbering()
    {
        var db = CreateInMemoryDbContext(nameof(GetJewelleryPaymentsPaged_SeparatesConfirmedAndPending_WithStableInstalmentNumbering));
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

        // Record first cash payment (Instalment 1)
        var p1 = await service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyAmount",
            EnteredAmount = 50000m,
            ConfirmedReceived = true
        }, null);

        var c1 = await db.JewelleryContributions.FirstAsync(c => c.PaymentId == p1.PaymentId);
        c1.ConfirmedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var pay1 = await db.Payments.FirstAsync(p => p.Id == p1.PaymentId);
        pay1.CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        pay1.CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        // Record second cash payment (Instalment 2)
        var p2 = await service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyAmount",
            EnteredAmount = 25000m,
            ConfirmedReceived = true
        }, null);

        // Add a genuine pending payment intent
        var pendingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            BranchId = branch.Id,
            Amount = 15000m,
            Currency = "LKR",
            Status = PaymentStatus.Pending,
            Method = PaymentMethod.MockOnline,
            Module = BusinessModule.Jewellery,
            ProviderReference = "PAYHERE-PENDING-001",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Payments.Add(pendingPayment);

        var pendingAlloc = new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            PaymentId = pendingPayment.Id,
            AllocationType = "JewelleryContribution",
            TargetEntityId = enrolment.Id,
            AllocatedAmount = 15000m
        };
        db.PaymentAllocations.Add(pendingAlloc);
        await db.SaveChangesAsync();

        // Query payments paged
        var response = await service.GetJewelleryPaymentsPagedAsync(staff.Id, new AdminJewelleryPaymentsQuery());

        Assert.Equal(3, response.TotalCount);
        Assert.Equal(75000m, response.TotalConfirmedAmount);
        Assert.Equal(15000m, response.TotalPendingAmount);
        Assert.Equal(1, response.TotalPendingCount);
        Assert.True(response.TotalConfirmedGrams > 0m);

        // Verify instalment numbering:
        // p2 is newest -> Payment 2
        // p1 is oldest -> Payment 1
        // pendingPayment -> Pending ("—")
        var p2Item = response.Items.First(x => x.PaymentReference == p2.PaymentReference);
        var p1Item = response.Items.First(x => x.PaymentReference == p1.PaymentReference);
        var pendingItem = response.Items.First(x => x.PaymentReference == "PAYHERE-PENDING-001");

        Assert.Equal(2, p2Item.InstalmentNumber);
        Assert.Equal("Payment 2", p2Item.InstalmentLabel);
        Assert.Equal("Successful", p2Item.Status);

        Assert.Equal(1, p1Item.InstalmentNumber);
        Assert.Equal("Payment 1", p1Item.InstalmentLabel);
        Assert.Equal("Successful", p1Item.Status);

        Assert.Null(pendingItem.InstalmentNumber);
        Assert.True(pendingItem.InstalmentLabel == "Pending" || pendingItem.InstalmentLabel == "—");
        Assert.Equal("Pending", pendingItem.Status);
        Assert.Equal(0m, pendingItem.GoldCredit); // Pending does not credit gold
        Assert.Null(pendingItem.PaidAtUtc);
    }

    [Fact]
    public async Task GetJewelleryPaymentsPaged_InstalmentNumbering_RemainsStableAcrossFiltering()
    {
        var db = CreateInMemoryDbContext(nameof(GetJewelleryPaymentsPaged_InstalmentNumbering_RemainsStableAcrossFiltering));
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

        // Payment 1: record and shift timestamp to 5 days ago
        var p1 = await service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyAmount",
            EnteredAmount = 10000m,
            ConfirmedReceived = true
        }, null);

        var c1 = await db.JewelleryContributions.FirstAsync(c => c.PaymentId == p1.PaymentId);
        c1.ConfirmedAtUtc = DateTimeOffset.UtcNow.AddDays(-5);
        var pay1 = await db.Payments.FirstAsync(p => p.Id == p1.PaymentId);
        pay1.CompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-5);
        pay1.CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5);

        // Payment 2: record today
        var p2 = await service.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyAmount",
            EnteredAmount = 15000m,
            ConfirmedReceived = true
        }, null);

        await db.SaveChangesAsync();

        // Now filter specifically for recent payment (excluding Payment 1 which was 5 days ago)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await service.GetJewelleryPaymentsPagedAsync(staff.Id, new AdminJewelleryPaymentsQuery
        {
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        // The returned item must STILL be Payment 2, NOT renumbered to Payment 1!
        Assert.Single(response.Items);
        var item = response.Items[0];
        Assert.Equal(p2.PaymentReference, item.PaymentReference);
        Assert.Equal(2, item.InstalmentNumber);
        Assert.Equal("Payment 2", item.InstalmentLabel);
    }

    [Fact]
    public async Task GetIncompleteEnrolmentsPaged_IdentifiesExpiredEnrolmentsBelowTarget()
    {
        var db = CreateInMemoryDbContext(nameof(GetIncompleteEnrolmentsPaged_IdentifiesExpiredEnrolmentsBelowTarget));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        // 1. Incomplete enrolment: Target 10g, Saved 4g, TargetEndDate passed 10 days ago
        var expiredIncomplete = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            PlanNameSnapshot = plan.Name,
            CategoryNameSnapshot = "Bridal Collection",
            BranchId = branch.Id,
            Status = JewelleryEnrolmentStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-7)),
            SelectedDurationMonths = 6,
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            TargetProductWeightGrams = 10.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 100000m,
            Currency = "LKR",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMonths(-7)
        };
        db.JewelleryEnrolments.Add(expiredIncomplete);

        // 2. Active enrolment: TargetEndDate not passed yet
        var activeOnSchedule = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            PlanNameSnapshot = plan.Name,
            CategoryNameSnapshot = "Bridal Collection",
            BranchId = branch.Id,
            Status = JewelleryEnrolmentStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SelectedDurationMonths = 6,
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            TargetProductWeightGrams = 10.0m,
            TotalSavedGrams = 2.0m,
            TotalContributedCurrency = 50000m,
            Currency = "LKR",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.JewelleryEnrolments.Add(activeOnSchedule);

        // 3. Completed enrolment: Expired but target reached and status Completed
        var completed = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            PlanNameSnapshot = plan.Name,
            CategoryNameSnapshot = "Bridal Collection",
            BranchId = branch.Id,
            Status = JewelleryEnrolmentStatus.Completed,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-7)),
            SelectedDurationMonths = 6,
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            TargetProductWeightGrams = 10.0m,
            TotalSavedGrams = 10.0m,
            TotalContributedCurrency = 250000m,
            Currency = "LKR",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMonths(-7)
        };
        db.JewelleryEnrolments.Add(completed);

        await db.SaveChangesAsync();

        var incompleteResponse = await service.GetIncompleteEnrolmentsPagedAsync(staff.Id, new AdminIncompleteEnrolmentsQuery());

        Assert.Single(incompleteResponse.Items);
        var inc = incompleteResponse.Items[0];
        Assert.Equal(expiredIncomplete.Id, inc.EnrolmentId);
        Assert.Equal(10.0m, inc.TargetGrams);
        Assert.Equal(4.0m, inc.NetSavedGrams);
        Assert.Equal(6.0m, inc.RemainingGrams);
        Assert.Equal(40.0m, inc.ProgressPercentage);
        Assert.True(inc.CanSendReminder);
        Assert.Equal("PlanIncomplete", inc.ReminderReason);
    }

    [Fact]
    public async Task ReminderEligibility_ActivePlan_RespectsThreeCalendarMonths_AndMonthEndBoundaries()
    {
        var db = CreateInMemoryDbContext(nameof(ReminderEligibility_ActivePlan_RespectsThreeCalendarMonths_AndMonthEndBoundaries));
        var (branch, staff, customer, plan) = await SetupBranchStaffCustomerAndPlanAsync(db);

        var goldRateMock = new MockGoldRateService { MockRate = 25000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var service = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        // Enrolment A: Joined 4 calendar months ago, no payments made
        var enrNoPayment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            PlanNameSnapshot = plan.Name,
            CategoryNameSnapshot = "Bridal Collection",
            BranchId = branch.Id,
            Status = JewelleryEnrolmentStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-4)),
            SelectedDurationMonths = 12,
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(8)),
            TargetProductWeightGrams = 10.0m,
            TotalSavedGrams = 0m,
            TotalContributedCurrency = 0m,
            Currency = "LKR",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMonths(-4)
        };
        db.JewelleryEnrolments.Add(enrNoPayment);

        // Enrolment B: Joined 4 months ago, but paid 1 month ago
        var enrRecentPayment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            PlanNameSnapshot = plan.Name,
            CategoryNameSnapshot = "Bridal Collection",
            BranchId = branch.Id,
            Status = JewelleryEnrolmentStatus.Active,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-4)),
            SelectedDurationMonths = 12,
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(8)),
            TargetProductWeightGrams = 10.0m,
            TotalSavedGrams = 1.0m,
            TotalContributedCurrency = 25000m,
            Currency = "LKR",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMonths(-4)
        };
        db.JewelleryEnrolments.Add(enrRecentPayment);

        var paymentRecent = new Payment
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CustomerId = customer.Id,
            Module = BusinessModule.Jewellery,
            Amount = 25000m,
            Currency = "LKR",
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "REC-B1",
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMonths(-1),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMonths(-1)
        };
        db.Payments.Add(paymentRecent);

        var cRecent = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrRecentPayment.Id,
            CurrencyAmount = 25000m,
            GoldGrams = 1.0m,
            AppliedRatePerGram = 25000m,
            Karat = 24,
            PaymentId = paymentRecent.Id,
            ConfirmedAtUtc = DateTimeOffset.UtcNow.AddMonths(-1)
        };
        db.JewelleryContributions.Add(cRecent);

        await db.SaveChangesAsync();

        var ctxNoPay = await service.GetReminderContextAsync(staff.Id, enrNoPayment.Id);
        Assert.True(ctxNoPay.CanSendReminder);
        Assert.Equal("NoPaymentForThreeMonths", ctxNoPay.ReminderReason);
        Assert.Null(ctxNoPay.LastSuccessfulPaymentAt);

        var ctxRecent = await service.GetReminderContextAsync(staff.Id, enrRecentPayment.Id);
        Assert.False(ctxRecent.CanSendReminder); // Paid 1 month ago, 3 calendar months have not elapsed!
        Assert.NotNull(ctxRecent.LastSuccessfulPaymentAt);
    }

    [Fact]
    public async Task ReminderHandoffAndDispatch_ValidatesAuthorization_AndReportsChannelCapabilities()
    {
        var db = CreateInMemoryDbContext(nameof(ReminderHandoffAndDispatch_ValidatesAuthorization_AndReportsChannelCapabilities));
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

        // 1. Fetch authorized reminder context
        var context = await service.GetReminderContextAsync(staff.Id, enrolment.Id);
        Assert.Equal(enrolment.Id, context.EnrolmentId);
        Assert.Equal(customer.FullName, context.CustomerName);
        Assert.Equal("Bridal Gold Saver", context.PlanName);

        // Check channel definitions
        Assert.True(context.InApp.Available);
        Assert.True(context.Email.Available);
        Assert.True(context.Sms.Available);
        Assert.False(context.Push.Available);
        Assert.Contains("not currently configured", context.Push.Description);

        // 2. Dispatch reminder using In-App and Email
        var sendResult = await service.SendReminderAsync(staff.Id, enrolment.Id, new AdminSendReminderRequest
        {
            Channels = new List<string> { "InApp", "Email" },
            Title = "Monthly Contribution Reminder",
            Message = "Please visit our branch or pay online to stay on track for your Bridal Gold Saver plan."
        });

        Assert.True(sendResult.Success);
        Assert.Contains("InApp", sendResult.DeliveredChannels);
        Assert.Contains("Email", sendResult.DeliveredChannels);

        // Verify In-App notification was created in database
        var createdNotification = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer.Id && n.Title == "Monthly Contribution Reminder");
        Assert.NotNull(createdNotification);
        Assert.Equal("reminder", createdNotification.Type);
    }
}


