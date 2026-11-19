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

public class PlanAlertAndNotificationTests
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

        public Task<CustomerGoldRateResponse> GetCustomerRateAsync(Guid? branchId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GoldRateOverviewDto> GetOverviewAsync(Guid? branchId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GoldRateOverviewDto> SetOverrideAsync(SetGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GoldRateOverviewDto> ToggleOverrideAsync(ToggleGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GoldRateHistoryDto>> GetHistoryAsync(Guid? branchId, int limit = 50, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private (Staff Staff, Role Role, StaffBranch StaffBranch, StaffRole StaffRole) CreateSuperAdminStaff(Guid branchId)
    {
        var staffId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = "SuperAdmin" };
        var staff = new Staff
        {
            Id = staffId,
            FullName = "Super Admin Staff",
            Email = $"admin_{staffId:N}@sjewls.lk",
            IsActive = true
        };
        var staffRole = new StaffRole { StaffId = staff.Id, RoleId = role.Id, Role = role };
        staff.Roles.Add(staffRole);
        var staffBranch = new StaffBranch { StaffId = staff.Id, BranchId = branchId };
        staff.AssignedBranches.Add(staffBranch);
        return (staff, role, staffBranch, staffRole);
    }

    [Fact]
    public async Task ChituWinner_ConfirmsRaffleWinner_CreatesNotificationAndAlert_Atomically()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(ChituWinner_ConfirmsRaffleWinner_CreatesNotificationAndAlert_Atomically));
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Main Branch", Code = "MB01", Address = "123 Main St", City = "Jaffna" };
        var (staff, role, staffBranch, staffRole) = CreateSuperAdminStaff(branch.Id);
        var customer1 = new Customer { Id = Guid.NewGuid(), FullName = "Winning Customer", PrimaryBranchId = branch.Id, IsActive = true };
        var customer2 = new Customer { Id = Guid.NewGuid(), FullName = "Other Customer", PrimaryBranchId = branch.Id, IsActive = true };

        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Branch = branch,
            Code = "CP-2026",
            Name = "Festival Gold Chitu",
            Description = "10-month plan",
            RedemptionType = ChituRedemptionType.Gold,
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 100000m,
            DurationMonths = 10,
            TotalSlotCapacity = 10,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 10, 31),
            Status = PlanStatus.Active
        };

        var slot1 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer1.Id,
            Customer = customer1,
            SlotNumber = 1,
            SlotDisplayCode = "SLOT-001",
            Status = ChituSlotStatus.Active
        };

        var slot2 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer2.Id,
            Customer = customer2,
            SlotNumber = 2,
            SlotDisplayCode = "SLOT-002",
            Status = ChituSlotStatus.Active
        };

        // Populate paid instalments for round 1
        slot1.Instalments.Add(new ChituInstalment
        {
            Id = Guid.NewGuid(),
            ChituSlotId = slot1.Id,
            MonthIndex = 1,
            DueDate = new DateOnly(2026, 1, 10),
            AmountDue = 10000m,
            Status = InstalmentStatus.Paid
        });

        slot2.Instalments.Add(new ChituInstalment
        {
            Id = Guid.NewGuid(),
            ChituSlotId = slot2.Id,
            MonthIndex = 1,
            DueDate = new DateOnly(2026, 1, 10),
            AmountDue = 10000m,
            Status = InstalmentStatus.Paid
        });

        db.Branches.Add(branch);
        db.StaffMembers.Add(staff);
        db.StaffBranches.Add(staffBranch);
        db.Customers.AddRange(customer1, customer2);
        db.ChituPlans.Add(plan);
        db.ChituSlots.AddRange(slot1, slot2);
        await db.SaveChangesAsync();

        var auditLog = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var chituService = new ChituService(db, auditLog, NullLogger<ChituService>.Instance);

        // Act: Confirm raffle winner for Round 1
        var result = await chituService.RecordRaffleDrawAsync(staff.Id, plan.Id, 1, new RecordChituRaffleRequest
        {
            WinningSlotId = slot1.Id,
            Remarks = "First round draw"
        });

        // Assert Draw Result
        Assert.NotNull(result);
        Assert.Equal(slot1.Id, result.WinningSlotId);
        Assert.Equal(customer1.Id, result.CustomerId);

        // Assert Inbox Notification
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer1.Id);
        Assert.NotNull(notif);
        Assert.Equal("Chitu Raffle Winner!", notif.Title);
        Assert.Contains("slot 1 in Festival Gold Chitu has won Round 1 for January 2026", notif.Message);
        Assert.Contains("Visit Main Branch to collect your gold prize", notif.Message);
        Assert.False(notif.IsRead);
        Assert.Equal(plan.Id, notif.ReferenceId);
        Assert.Equal("ChituInvestment", notif.ReferenceType);

        // Assert Other customer received no notification
        var otherNotif = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer2.Id);
        Assert.Null(otherNotif);

        // Assert Plan Alert
        var alert = await db.PlanAlerts.FirstOrDefaultAsync(a => a.CustomerId == customer1.Id);
        Assert.NotNull(alert);
        Assert.Equal("ChituWinner", alert.EventType);
        Assert.Equal(BusinessModule.Chitu, alert.Module);
        Assert.Equal(plan.Id, alert.PlanId);
        Assert.Equal("Festival Gold Chitu", alert.PlanNameSnapshot);
        Assert.Equal(slot1.Id, alert.ChituSlotId);
        Assert.Equal(1, alert.ChituSlotNumber);
        Assert.Equal("SLOT-001", alert.ChituSlotDisplayCode);
        Assert.Equal(1, alert.ChituRoundIndex);
        Assert.Equal("January 2026", alert.ChituRoundScheduledMonthYear);
        Assert.Equal(ChituRedemptionType.Gold, alert.RedemptionType);
        Assert.Equal(100000m, alert.PrizeAmount);
        Assert.Equal(branch.Id, alert.BranchId);
        Assert.Equal("Main Branch", alert.BranchName);
        Assert.False(alert.IsAcknowledged);
        Assert.Null(alert.AcknowledgedAtUtc);
        Assert.Equal("chitu-investments/" + plan.Id, alert.NavigationTarget);
        Assert.Equal("Visit Main Branch to collect your gold prize.", alert.CollectionInstruction);

        // Assert atomic cross-link
        Assert.Equal(alert.Id, notif.PlanAlertId);
        Assert.Equal(notif.Id, alert.NotificationId);
    }

    [Fact]
    public async Task ChituWinner_SeparateAlertsForSeparateWinningEvents()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(ChituWinner_SeparateAlertsForSeparateWinningEvents));
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Colombo Branch", Code = "CB01", Address = "Galle Rd", City = "Colombo" };
        var (staff, role, staffBranch, staffRole) = CreateSuperAdminStaff(branch.Id);
        var customer = new Customer { Id = Guid.NewGuid(), FullName = "Multi-Slot Customer", PrimaryBranchId = branch.Id, IsActive = true };

        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Branch = branch,
            Code = "CP-MULTI",
            Name = "Mega Chitu Plan",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 5000m,
            DurationMonths = 12,
            TotalSlotCapacity = 10,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            Status = PlanStatus.Active
        };

        var slotA = new ChituSlot { Id = Guid.NewGuid(), ChituPlanId = plan.Id, CustomerId = customer.Id, Customer = customer, SlotNumber = 3, SlotDisplayCode = "SLOT-003", Status = ChituSlotStatus.Active };
        var slotB = new ChituSlot { Id = Guid.NewGuid(), ChituPlanId = plan.Id, CustomerId = customer.Id, Customer = customer, SlotNumber = 7, SlotDisplayCode = "SLOT-007", Status = ChituSlotStatus.Active };

        for (int m = 1; m <= 2; m++)
        {
            slotA.Instalments.Add(new ChituInstalment { Id = Guid.NewGuid(), ChituSlotId = slotA.Id, MonthIndex = m, DueDate = new DateOnly(2026, m, 10), AmountDue = 5000m, Status = InstalmentStatus.Paid });
            slotB.Instalments.Add(new ChituInstalment { Id = Guid.NewGuid(), ChituSlotId = slotB.Id, MonthIndex = m, DueDate = new DateOnly(2026, m, 10), AmountDue = 5000m, Status = InstalmentStatus.Paid });
        }

        db.Branches.Add(branch);
        db.StaffMembers.Add(staff);
        db.Customers.Add(customer);
        db.ChituPlans.Add(plan);
        db.ChituSlots.AddRange(slotA, slotB);
        await db.SaveChangesAsync();

        var auditLog = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var chituService = new ChituService(db, auditLog, NullLogger<ChituService>.Instance);

        // Act 1: Slot A wins Round 1
        await chituService.RecordRaffleDrawAsync(staff.Id, plan.Id, 1, new RecordChituRaffleRequest { WinningSlotId = slotA.Id });

        // Act 2: Slot B wins Round 2
        await chituService.RecordRaffleDrawAsync(staff.Id, plan.Id, 2, new RecordChituRaffleRequest { WinningSlotId = slotB.Id });

        // Assert: 2 distinct alerts exist for the same customer
        var alerts = await db.PlanAlerts.Where(a => a.CustomerId == customer.Id).OrderBy(a => a.ChituRoundIndex).ToListAsync();
        Assert.Equal(2, alerts.Count);
        Assert.Equal(1, alerts[0].ChituRoundIndex);
        Assert.Equal(3, alerts[0].ChituSlotNumber);
        Assert.Equal(2, alerts[1].ChituRoundIndex);
        Assert.Equal(7, alerts[1].ChituSlotNumber);
    }

    [Fact]
    public async Task JewelleryCompletion_VerifiedOnlinePayment_CreatesNotificationAndAlert_Atomically()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(JewelleryCompletion_VerifiedOnlinePayment_CreatesNotificationAndAlert_Atomically));
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Kandy Branch", Code = "KB01", Address = "Temple St", City = "Kandy" };
        var customer = new Customer { Id = Guid.NewGuid(), FullName = "Jewellery Saver", PrimaryBranchId = branch.Id, IsActive = true };

        var category = new JewelleryPlanCategory { Id = Guid.NewGuid(), BranchId = branch.Id, Name = "Bangles", NormalizedName = "BANGLES" };
        var plan = new JewelleryPlan { Id = Guid.NewGuid(), BranchId = branch.Id, CategoryId = category.Id, Code = "JP-BANGLE", Name = "Gold Bangle Plan" };

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            JewelleryPlanId = plan.Id,
            JewelleryPlan = plan,
            BranchId = branch.Id,
            Branch = branch,
            PlanNameSnapshot = "Gold Bangle Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };

        var quote = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            JewelleryEnrolment = enrolment,
            CustomerId = customer.Id,
            Customer = customer,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryCategories.Add(category);
        db.JewelleryPlans.Add(plan);
        db.JewelleryEnrolments.Add(enrolment);
        db.JewelleryPaymentQuotes.Add(quote);
        await db.SaveChangesAsync();

        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var finalizationService = new JewelleryPaymentFinalizationService(db, notifyService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        // Act: Payment completes the remaining 1.0g to reach 5.0g target
        var result = await finalizationService.FinalizePaymentAsync(new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = "ONLINE-TXN-12345",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "MockGateway"
        });

        // Assert
        Assert.True(result.Success);
        Assert.True(result.PlanCompleted);
        Assert.Equal(5.0m, result.NewTotalSavedGrams);

        // Verify enrolment status
        var updatedEnrolment = await db.JewelleryEnrolments.FindAsync(enrolment.Id);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, updatedEnrolment!.Status);

        // Verify inbox notification
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer.Id && n.Type == "completion");
        Assert.NotNull(notif);
        Assert.Equal("Jewellery Plan Completed!", notif.Title);
        Assert.Contains("completed Gold Bangle Plan and reached your target of 5 g", notif.Message);
        Assert.Contains("Visit Kandy Branch to collect your jewellery", notif.Message);
        Assert.Equal(enrolment.Id, notif.ReferenceId);
        Assert.Equal("JewelleryEnrolment", notif.ReferenceType);

        // Verify plan alert
        var alert = await db.PlanAlerts.FirstOrDefaultAsync(a => a.JewelleryEnrolmentId == enrolment.Id);
        Assert.NotNull(alert);
        Assert.Equal("JewelleryCompletion", alert.EventType);
        Assert.Equal(BusinessModule.Jewellery, alert.Module);
        Assert.Equal("Jewellery Plan Completed!", alert.Title);
        Assert.Equal(5.0m, alert.TargetGrams);
        Assert.Equal(branch.Id, alert.BranchId);
        Assert.Equal("Kandy Branch", alert.BranchName);
        Assert.False(alert.IsAcknowledged);
        Assert.Equal("jewellery-enrolments/" + enrolment.Id, alert.NavigationTarget);
        Assert.Equal("Visit Kandy Branch to collect your jewellery.", alert.CollectionInstruction);

        // Physical collection must remain unclaimed
        var claim = await db.PhysicalClaims.FirstOrDefaultAsync(c => c.ReferenceId == enrolment.Id);
        Assert.Null(claim);
    }

    [Fact]
    public async Task JewelleryCompletion_InStoreCashPayment_CreatesNotificationAndAlert_Atomically()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(JewelleryCompletion_InStoreCashPayment_CreatesNotificationAndAlert_Atomically));
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Main Branch", Code = "MB01", Address = "Main Rd", City = "Jaffna" };
        var (staff, role, staffBranch, staffRole) = CreateSuperAdminStaff(branch.Id);
        var customer = new Customer { Id = Guid.NewGuid(), FullName = "Cash Customer", PrimaryBranchId = branch.Id, IsActive = true };

        var category = new JewelleryPlanCategory { Id = Guid.NewGuid(), BranchId = branch.Id, Name = "Rings", NormalizedName = "RINGS" };
        var plan = new JewelleryPlan { Id = Guid.NewGuid(), BranchId = branch.Id, CategoryId = category.Id, Code = "JP-RING", Name = "Gold Ring Plan" };

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Customer = customer,
            JewelleryPlanId = plan.Id,
            JewelleryPlan = plan,
            BranchId = branch.Id,
            Branch = branch,
            PlanNameSnapshot = "Gold Ring Plan",
            TargetProductWeightGrams = 2.0m,
            TotalSavedGrams = 1.0m,
            TotalContributedCurrency = 30000m,
            PremiumPercentage = 0m,
            Status = JewelleryEnrolmentStatus.Active
        };

        db.Branches.Add(branch);
        db.StaffMembers.Add(staff);
        db.Customers.Add(customer);
        db.JewelleryCategories.Add(category);
        db.JewelleryPlans.Add(plan);
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var goldRateMock = new MockGoldRateService { MockRate = 30000m };
        var notifyService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var auditService = new AuditLogService(db, NullLogger<AuditLogService>.Instance);
        var adminService = new AdminJewelleryEnrolmentService(db, goldRateMock, notifyService, auditService, NullLogger<AdminJewelleryEnrolmentService>.Instance);

        // Act: Cash payment for remaining 1.0g
        var result = await adminService.RecordPaymentAsync(staff.Id, enrolment.Id, new AdminRecordPaymentRequest
        {
            Mode = "BuyGram",
            RequestedGrams = 1.0m,
            ConfirmedReceived = true
        }, null);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.PlanCompleted);
        Assert.Equal(2.0m, result.NewTotalSavedGrams);

        // Verify alert and notification
        var alert = await db.PlanAlerts.FirstOrDefaultAsync(a => a.JewelleryEnrolmentId == enrolment.Id);
        Assert.NotNull(alert);
        Assert.Equal("JewelleryCompletion", alert.EventType);
        Assert.Equal(2.0m, alert.TargetGrams);

        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer.Id && n.Type == "completion");
        Assert.NotNull(notif);
        Assert.Equal(alert.Id, notif.PlanAlertId);
    }

    [Fact]
    public async Task PlanAlert_Acknowledge_Idempotent_StopsPopup_PreservesPermanentBanner()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(PlanAlert_Acknowledge_Idempotent_StopsPopup_PreservesPermanentBanner));
        var customerId = Guid.NewGuid();
        var alert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            EventType = "ChituWinner",
            Module = BusinessModule.Chitu,
            Title = "You Won!",
            Message = "Winner message",
            PlanId = Guid.NewGuid(),
            PlanNameSnapshot = "Chitu Plan 1",
            ReferenceId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            BranchName = "Branch A",
            IsAcknowledged = false,
            AcknowledgedAtUtc = null,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        db.PlanAlerts.Add(alert);
        await db.SaveChangesAsync();

        var alertService = new PlanAlertService(db, NullLogger<PlanAlertService>.Instance);

        // Initial state before acknowledgement
        var initialDto = alertService.MapToDto(alert);
        Assert.False(initialDto.IsAcknowledged);
        Assert.True(initialDto.ShouldShowPopup);

        // Act 1: Acknowledge (dismiss popup on mobile)
        var ackResult1 = await alertService.AcknowledgeAlertAsync(customerId, alert.Id);

        // Assert 1: Acknowledged state recorded
        Assert.True(ackResult1.IsAcknowledged);
        Assert.False(ackResult1.ShouldShowPopup);
        Assert.NotNull(ackResult1.AcknowledgedAtUtc);
        var firstAckTime = ackResult1.AcknowledgedAtUtc;

        // Act 2: Repeat acknowledgement (idempotency check)
        var ackResult2 = await alertService.AcknowledgeAlertAsync(customerId, alert.Id);

        // Assert 2: Same timestamp preserved, returns success
        Assert.True(ackResult2.IsAcknowledged);
        Assert.False(ackResult2.ShouldShowPopup);
        Assert.Equal(firstAckTime, ackResult2.AcknowledgedAtUtc);

        // Assert 3: Alert is STILL returned in investment list (permanent banner)
        var allAlerts = await alertService.GetCustomerPlanAlertsAsync(customerId, alert.PlanId);
        Assert.Single(allAlerts);
        Assert.Equal(alert.Id, allAlerts[0].AlertId);
        Assert.False(allAlerts[0].ShouldShowPopup); // No popup
        Assert.True(allAlerts[0].IsAcknowledged); // Permanent banner remains!
    }

    [Fact]
    public async Task InboxReadState_And_AlertAcknowledgementState_AreIndependent()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(InboxReadState_And_AlertAcknowledgementState_AreIndependent));
        var customerId = Guid.NewGuid();

        var notif = new Notification
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Title = "Draw Winner",
            Message = "You won",
            Type = "chitu_winner",
            IsRead = false
        };

        var alert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            EventType = "ChituWinner",
            Module = BusinessModule.Chitu,
            Title = "Draw Winner",
            Message = "You won",
            PlanId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            NotificationId = notif.Id,
            IsAcknowledged = false
        };

        notif.PlanAlertId = alert.Id;
        db.Notifications.Add(notif);
        db.PlanAlerts.Add(alert);
        await db.SaveChangesAsync();

        var notifService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var alertService = new PlanAlertService(db, NullLogger<PlanAlertService>.Instance);

        // Act 1: Mark notification as read
        await notifService.MarkAsReadAsync(customerId, notif.Id);

        // Assert 1: Notification is read, but alert is STILL NOT acknowledged (popup remains eligible)
        var reloadedNotif = await db.Notifications.FindAsync(notif.Id);
        Assert.True(reloadedNotif!.IsRead);

        var reloadedAlert = await db.PlanAlerts.FindAsync(alert.Id);
        Assert.False(reloadedAlert!.IsAcknowledged);
        Assert.Null(reloadedAlert.AcknowledgedAtUtc);

        // Reset notification to unread
        reloadedNotif.IsRead = false;
        await db.SaveChangesAsync();

        // Act 2: Acknowledge alert
        await alertService.AcknowledgeAlertAsync(customerId, alert.Id);

        // Assert 2: Alert is acknowledged, but notification is STILL UNREAD
        var reloadedAlert2 = await db.PlanAlerts.FindAsync(alert.Id);
        Assert.True(reloadedAlert2!.IsAcknowledged);

        var reloadedNotif2 = await db.Notifications.FindAsync(notif.Id);
        Assert.False(reloadedNotif2!.IsRead);
    }

    [Fact]
    public async Task CrossCustomerAccess_CannotAcknowledgeOthersAlert_ThrowsUnauthorized()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(CrossCustomerAccess_CannotAcknowledgeOthersAlert_ThrowsUnauthorized));
        var ownerCustomerId = Guid.NewGuid();
        var attackerCustomerId = Guid.NewGuid();

        var alert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = ownerCustomerId,
            EventType = "ChituWinner",
            Title = "Winner",
            Message = "You won",
            PlanId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            IsAcknowledged = false
        };
        db.PlanAlerts.Add(alert);
        await db.SaveChangesAsync();

        var alertService = new PlanAlertService(db, NullLogger<PlanAlertService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            alertService.AcknowledgeAlertAsync(attackerCustomerId, alert.Id));
    }

    [Fact]
    public async Task PlanAlert_Acknowledge_ConcurrentRequests_SucceedsWithoutConflict()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(PlanAlert_Acknowledge_ConcurrentRequests_SucceedsWithoutConflict));
        var customerId = Guid.NewGuid();
        var alert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            EventType = "ChituWinner",
            Title = "Winner",
            Message = "You won",
            PlanId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            IsAcknowledged = false
        };
        db.PlanAlerts.Add(alert);
        await db.SaveChangesAsync();

        var alertService = new PlanAlertService(db, NullLogger<PlanAlertService>.Instance);

        // Act: Run 10 concurrent acknowledgement calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => alertService.AcknowledgeAlertAsync(customerId, alert.Id))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: All 10 succeed, alert is acknowledged, and ShouldShowPopup is false
        Assert.All(results, r =>
        {
            Assert.True(r.IsAcknowledged);
            Assert.False(r.ShouldShowPopup);
            Assert.NotNull(r.AcknowledgedAtUtc);
        });
    }

    [Fact]
    public async Task PlanAlert_InactiveCustomer_ThrowsInvalidOperationException()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(PlanAlert_InactiveCustomer_ThrowsInvalidOperationException));
        var customerId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = customerId,
            FullName = "Inactive Customer",
            IsActive = false
        };
        var alert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            EventType = "JewelleryCompletion",
            Title = "Completed",
            Message = "Completed target",
            PlanId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            IsAcknowledged = false
        };
        db.Customers.Add(customer);
        db.PlanAlerts.Add(alert);
        await db.SaveChangesAsync();

        var alertService = new PlanAlertService(db, NullLogger<PlanAlertService>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            alertService.AcknowledgeAlertAsync(customerId, alert.Id));
        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlanAlert_MultipleEnrolmentsInSamePlan_AlertAssignedOnlyToCompletedEnrolment()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(PlanAlert_MultipleEnrolmentsInSamePlan_AlertAssignedOnlyToCompletedEnrolment));
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Kandy Branch", Code = "KB01" };
        var customer = new Customer { Id = Guid.NewGuid(), FullName = "Repeat Customer", PrimaryBranchId = branch.Id, IsActive = true };
        var plan = new JewelleryPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Name = "Gold Chain 10g",
            TargetProductGoldWeightGrams = 10m,
            IsActive = true
        };

        var enrolment1 = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            TargetProductWeightGrams = 10m,
            TotalSavedGrams = 10m,
            Status = JewelleryEnrolmentStatus.Completed,
            TargetEndDate = new DateOnly(2027, 1, 1)
        };

        var enrolment2 = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            TargetProductWeightGrams = 10m,
            TotalSavedGrams = 2m,
            Status = JewelleryEnrolmentStatus.Active,
            TargetEndDate = new DateOnly(2027, 1, 1)
        };

        // Alert created specifically for enrolment 1
        var alert1 = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryEnrolmentId = enrolment1.Id,
            PlanId = plan.Id,
            ReferenceId = enrolment1.Id,
            BranchId = branch.Id,
            BranchName = branch.Name,
            EventType = "JewelleryCompletion",
            Module = BusinessModule.Jewellery,
            Title = "Jewellery Plan Completed!",
            Message = "You completed enrolment 1",
            TargetGrams = 10m,
            IsAcknowledged = false
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryPlans.Add(plan);
        db.JewelleryEnrolments.AddRange(enrolment1, enrolment2);
        db.PlanAlerts.Add(alert1);
        await db.SaveChangesAsync();

        var jewelleryService = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        // Act 1: Get Enrolment 1 Details
        var details1 = await jewelleryService.GetEnrolmentDetailsAsync(customer.Id, enrolment1.Id);

        // Assert 1: Enrolment 1 has completion alert
        Assert.NotNull(details1.CompletionAlert);
        Assert.Equal(alert1.Id, details1.CompletionAlert.AlertId);
        Assert.True(details1.CompletionAlert.ShouldShowPopup);
        Assert.NotEmpty(details1.Alerts);

        // Act 2: Get Enrolment 2 Details
        var details2 = await jewelleryService.GetEnrolmentDetailsAsync(customer.Id, enrolment2.Id);

        // Assert 2: Enrolment 2 does NOT have completion alert
        Assert.Null(details2.CompletionAlert);
        Assert.Empty(details2.Alerts);

        // Act 3: Get List of Enrolments (My Investments)
        var paged = await jewelleryService.GetEnrolmentsAsync(customer.Id, 1, 10);

        // Assert 3: List correctly reflects enrolment 1 has alert and enrolment 2 does not
        var paged1 = paged.Items.First(i => i.EnrolmentId == enrolment1.Id);
        var paged2 = paged.Items.First(i => i.EnrolmentId == enrolment2.Id);
        Assert.NotNull(paged1.CompletionAlert);
        Assert.Null(paged2.CompletionAlert);
    }

    [Fact]
    public async Task PlanAlert_BannerPersistence_RemainsPresentInListAndDetailsAfterAcknowledgement()
    {
        // Arrange
        var db = CreateInMemoryDbContext(nameof(PlanAlert_BannerPersistence_RemainsPresentInListAndDetailsAfterAcknowledgement));
        var branch = new Branch { Id = Guid.NewGuid(), Name = "Galle Branch", Code = "GB01", Address = "Main St", City = "Galle" };
        var customer = new Customer { Id = Guid.NewGuid(), FullName = "Persistent Customer", PrimaryBranchId = branch.Id, IsActive = true };
        var plan = new JewelleryPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Name = "Gold Bangle 8g",
            TargetProductGoldWeightGrams = 8m,
            IsActive = true
        };

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            TargetProductWeightGrams = 8m,
            TotalSavedGrams = 8m,
            Status = JewelleryEnrolmentStatus.Completed,
            TargetEndDate = new DateOnly(2027, 1, 1)
        };

        var alert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            JewelleryEnrolmentId = enrolment.Id,
            PlanId = plan.Id,
            ReferenceId = enrolment.Id,
            BranchId = branch.Id,
            BranchName = branch.Name,
            BranchAddress = branch.Address,
            EventType = "JewelleryCompletion",
            Module = BusinessModule.Jewellery,
            Title = "Jewellery Plan Completed!",
            Message = "You completed enrolment",
            TargetGrams = 8m,
            IsAcknowledged = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryPlans.Add(plan);
        db.JewelleryEnrolments.Add(enrolment);
        db.PlanAlerts.Add(alert);
        await db.SaveChangesAsync();

        var alertService = new PlanAlertService(db, NullLogger<PlanAlertService>.Instance);
        var jewelleryService = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        // Stage 1: Before acknowledgement
        var beforeDetails = await jewelleryService.GetEnrolmentDetailsAsync(customer.Id, enrolment.Id);
        Assert.NotNull(beforeDetails.CompletionAlert);
        Assert.False(beforeDetails.CompletionAlert.IsAcknowledged);
        Assert.True(beforeDetails.CompletionAlert.ShouldShowPopup); // Popup triggers!

        var beforeList = await jewelleryService.GetEnrolmentsAsync(customer.Id, 1, 10);
        Assert.NotNull(beforeList.Items[0].CompletionAlert);
        Assert.True(beforeList.Items[0].CompletionAlert!.ShouldShowPopup);

        // Stage 2: Acknowledge alert
        var ack = await alertService.AcknowledgeAlertAsync(customer.Id, alert.Id);
        Assert.True(ack.IsAcknowledged);
        Assert.False(ack.ShouldShowPopup);

        // Stage 3: After acknowledgement -> Alert is STILL RETURNED!
        var afterDetails = await jewelleryService.GetEnrolmentDetailsAsync(customer.Id, enrolment.Id);
        Assert.NotNull(afterDetails.CompletionAlert); // Still present!
        Assert.True(afterDetails.CompletionAlert.IsAcknowledged); // Marked acknowledged
        Assert.False(afterDetails.CompletionAlert.ShouldShowPopup); // Popup suppressed!
        Assert.NotEmpty(afterDetails.Alerts); // Permanent banner remains!
        Assert.Equal("Galle Branch", afterDetails.CompletionAlert.BranchName);

        var afterList = await jewelleryService.GetEnrolmentsAsync(customer.Id, 1, 10);
        Assert.NotNull(afterList.Items[0].CompletionAlert); // Still present in list!
        Assert.True(afterList.Items[0].CompletionAlert!.IsAcknowledged);
        Assert.False(afterList.Items[0].CompletionAlert!.ShouldShowPopup);
    }
}
