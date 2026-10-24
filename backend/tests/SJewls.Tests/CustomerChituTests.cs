using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class CustomerChituTests
{
    private SJewlsDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SJewlsDbContext(options);
    }

    private class MockNotificationService : SJewls.Application.Interfaces.ICustomerNotificationService
    {
        public List<(Guid CustomerId, string Title, string Message, string Type)> SentNotifications = new();

        public Task<CustomerNotificationsPagedResponse> GetNotificationsAsync(Guid customerId, int page, int pageSize, CancellationToken ct = default)
        {
            return Task.FromResult(new CustomerNotificationsPagedResponse());
        }

        public Task<bool> MarkAsReadAsync(Guid customerId, Guid notificationId, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<int> MarkAllAsReadAsync(Guid customerId, CancellationToken ct = default)
        {
            return Task.FromResult(1);
        }

        public Task CreateNotificationAsync(Guid customerId, string title, string message, string type, CancellationToken ct = default)
        {
            SentNotifications.Add((customerId, title, message, type));
            return Task.CompletedTask;
        }
    }

    private class MockAuditLogService : SJewls.Application.Interfaces.IAuditLogService
    {
        public Task LogActionAsync(string actorType, Guid? actorId, string action, string targetEntity, string targetId, Guid? branchId, object? before, object? after, string? ipAddress)
        {
            return Task.CompletedTask;
        }
    }

    private async Task<(Branch jaffnaBranch, Branch colomboBranch, Customer customer, Customer otherCustomer)> SetupTestDataAsync(SJewlsDbContext db)
    {
        var jaffna = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Jaffna Heritage Branch",
            Code = "JAF-01",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        var colombo = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Colombo Main Branch",
            Code = "COL-01",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        db.Branches.AddRange(jaffna, colombo);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Sivaram Tharmalingam",
            PhoneNumber = "+94771234567",
            Nic = "199212345678",
            PrimaryBranchId = jaffna.Id,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var otherCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Kavitha Rajaratnam",
            PhoneNumber = "+94779876543",
            Nic = "199587654321",
            PrimaryBranchId = jaffna.Id,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.Customers.AddRange(customer, otherCustomer);
        await db.SaveChangesAsync();

        return (jaffna, colombo, customer, otherCustomer);
    }

    private CustomerChituService CreateService(
        SJewlsDbContext db,
        MockNotificationService notifService,
        bool enableSimulation = true)
    {
        var options = Options.Create(new ChituPaymentOptions
        {
            Provider = "Simulated",
            EnableSimulation = enableSimulation
        });

        return new CustomerChituService(
            db,
            notifService,
            new MockAuditLogService(),
            options,
            NullLogger<CustomerChituService>.Instance);
    }

    #region 1. Discovery Tests

    [Fact]
    public async Task GetAvailablePlans_ReturnsOnlyActivePlansInCustomerBranch_ExcludesEndedFullOrOtherBranchPlans()
    {
        var db = CreateInMemoryDbContext(nameof(GetAvailablePlans_ReturnsOnlyActivePlansInCustomerBranch_ExcludesEndedFullOrOtherBranchPlans));
        var (jaffna, colombo, customer, _) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Plan 1: Active upcoming in Jaffna (Available)
        var upcomingPlan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-UPCOMING",
            Name = "Upcoming Gold Saver",
            MonthlyInstalmentAmount = 5000m,
            ChituBenefitValue = 60000m,
            TotalSlotCapacity = 20,
            DurationMonths = 12,
            StartDate = today.AddMonths(1),
            EndDate = today.AddMonths(12),
            Status = PlanStatus.Active
        };

        // Plan 2: Active running in Jaffna with capacity (Available - Late join)
        var runningPlan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-RUNNING",
            Name = "Running Cash Harvest",
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 120000m,
            TotalSlotCapacity = 30,
            DurationMonths = 12,
            StartDate = today.AddMonths(-2),
            EndDate = today.AddMonths(9),
            Status = PlanStatus.Active
        };

        // Plan 3: Inactive plan in Jaffna (Excluded)
        var inactivePlan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-INACTIVE",
            Name = "Inactive Plan",
            MonthlyInstalmentAmount = 5000m,
            ChituBenefitValue = 50000m,
            TotalSlotCapacity = 10,
            DurationMonths = 10,
            StartDate = today.AddMonths(1),
            EndDate = today.AddMonths(10),
            Status = PlanStatus.Draft
        };

        // Plan 4: Full plan in Jaffna (Excluded)
        var fullPlan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-FULL",
            Name = "Full Plan",
            MonthlyInstalmentAmount = 5000m,
            ChituBenefitValue = 50000m,
            TotalSlotCapacity = 1,
            DurationMonths = 10,
            StartDate = today.AddMonths(1),
            EndDate = today.AddMonths(10),
            Status = PlanStatus.Active
        };
        fullPlan.Slots.Add(new ChituSlot { SlotNumber = 1, SlotDisplayCode = "CHT-FULL-001", Status = ChituSlotStatus.Active, CustomerId = customer.Id });

        // Plan 5: Colombo branch plan (Excluded from Jaffna customer)
        var colomboPlan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = colombo.Id,
            Code = "CHT-COLOMBO",
            Name = "Colombo Plan",
            MonthlyInstalmentAmount = 5000m,
            ChituBenefitValue = 50000m,
            TotalSlotCapacity = 20,
            DurationMonths = 10,
            StartDate = today.AddMonths(1),
            EndDate = today.AddMonths(10),
            Status = PlanStatus.Active
        };

        db.ChituPlans.AddRange(upcomingPlan, runningPlan, inactivePlan, fullPlan, colomboPlan);
        await db.SaveChangesAsync();

        var result = await service.GetAvailablePlansAsync(customer.Id);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, p => p.PlanId == upcomingPlan.Id);
        Assert.Contains(result.Items, p => p.PlanId == runningPlan.Id);
        Assert.DoesNotContain(result.Items, p => p.PlanId == inactivePlan.Id);
        Assert.DoesNotContain(result.Items, p => p.PlanId == fullPlan.Id);
        Assert.DoesNotContain(result.Items, p => p.PlanId == colomboPlan.Id);

        // Verify currency and winning entitlement: MonthlySlotPrice * DurationMonths
        var upcomingDto = result.Items.First(p => p.PlanId == upcomingPlan.Id);
        Assert.Equal("LKR", upcomingDto.Currency);
        Assert.Equal(60000m, upcomingDto.WinningEntitlementPerSlot);
        Assert.Equal(20, upcomingDto.AvailableSlotsCount);
    }

    #endregion

    #region 2. Join Preview Tests

    [Fact]
    public async Task PreviewJoin_UpcomingPlan_RequiresOnlyRound1Payment()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewJoin_UpcomingPlan_RequiresOnlyRound1Payment));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-PRE01",
            Name = "Pre-Enrolment Plan",
            MonthlyInstalmentAmount = 5000m,
            ChituBenefitValue = 60000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(14), // In future
            EndDate = today.AddDays(14).AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var preview = await service.PreviewJoinAsync(customer.Id, plan.Id, new CustomerChituJoinPreviewRequest { Quantity = 3 });

        Assert.Equal(3, preview.RequestedQuantity);
        Assert.Equal(5000m, preview.MonthlySlotPrice);
        Assert.Equal(15000m, preview.TotalOrdinaryMonthlyPayment); // 3 * 5000
        Assert.Equal(1, preview.RequiredInitialInstalmentsPerSlot); // Round 1 only
        Assert.Equal(5000m, preview.RequiredInitialAmountPerSlot);
        Assert.Equal(15000m, preview.TotalInitialAmountPayable); // 3 * 5000 * 1
        Assert.StartsWith("JOIN-PREV-", preview.PreviewReference);
    }

    [Fact]
    public async Task PreviewJoin_LateJoiningRunningPlan_RequiresCatchupRounds()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewJoin_LateJoiningRunningPlan_RequiresCatchupRounds));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Start date 2 months ago -> current round is 3
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-LATE01",
            Name = "Late Join Running Plan",
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 120000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddMonths(-2),
            EndDate = today.AddMonths(9),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var preview = await service.PreviewJoinAsync(customer.Id, plan.Id, new CustomerChituJoinPreviewRequest { Quantity = 2 });

        Assert.Equal(3, preview.CurrentRoundIndex);
        Assert.Equal(3, preview.RequiredInitialInstalmentsPerSlot); // Rounds 1, 2, 3
        Assert.Equal(30000m, preview.RequiredInitialAmountPerSlot); // 3 * 10000
        Assert.Equal(60000m, preview.TotalInitialAmountPayable); // 2 slots * 30000 = 60000
        Assert.Equal(20000m, preview.TotalOrdinaryMonthlyPayment); // Normal monthly commitment = 2 * 10000
    }

    [Fact]
    public async Task PreviewJoin_ExceedingCapacity_ThrowsInvalidOperationException()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewJoin_ExceedingCapacity_ThrowsInvalidOperationException));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-CAP01",
            Name = "Small Capacity Plan",
            MonthlyInstalmentAmount = 5000m,
            TotalSlotCapacity = 2,
            DurationMonths = 12,
            StartDate = today.AddDays(7),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewJoinAsync(customer.Id, plan.Id, new CustomerChituJoinPreviewRequest { Quantity = 3 }));

        Assert.Contains("Capacity exceeded", ex.Message);
    }

    #endregion

    #region 3. Confirm & Join Tests

    [Fact]
    public async Task ConfirmAndJoin_SimulationDisabled_ThrowsPaymentProviderNotConfiguredException()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmAndJoin_SimulationDisabled_ThrowsPaymentProviderNotConfiguredException));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService, enableSimulation: false);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-SIMOFF",
            Name = "No Sim Plan",
            MonthlyInstalmentAmount = 5000m,
            TotalSlotCapacity = 20,
            DurationMonths = 12,
            StartDate = today.AddDays(7),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PaymentProviderNotConfiguredException>(() =>
            service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
            {
                Quantity = 1,
                IdempotencyKey = "key-sim-off"
            }));

        Assert.Equal(PaymentProviderNotConfiguredException.ErrorCode, ex.Code);
    }

    [Fact]
    public async Task ConfirmAndJoin_SimulatedSuccess_AllocatesSequentialSlotsAndPostsInstalments()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmAndJoin_SimulatedSuccess_AllocatesSequentialSlotsAndPostsInstalments));
        var (jaffna, _, customer, otherCustomer) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService, enableSimulation: true);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-GOLD01",
            Name = "Gold Sovereign Plan",
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 120000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(10), // Round 1
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        // Existing slot 1 already assigned to someone else
        var existingSlot = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = otherCustomer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-GOLD01-001",
            Status = ChituSlotStatus.Active
        };
        plan.Slots.Add(existingSlot);
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var response = await service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
        {
            Quantity = 2,
            IdempotencyKey = "join-tx-001"
        });

        Assert.NotNull(response);
        Assert.Equal(2, response.SlotsCount);
        Assert.Equal(20000m, response.TotalAmountPaid); // 2 slots * 10000 * 1 round
        Assert.True(response.IsSimulated);
        Assert.Equal(2, response.AssignedSlots.Count);

        // Verify sequential numbering: 2 and 3 (after existing 1)
        Assert.Equal(2, response.AssignedSlots[0].SlotNumber);
        Assert.Equal("CHT-GOLD01-002", response.AssignedSlots[0].SlotDisplayCode);
        Assert.Equal(3, response.AssignedSlots[1].SlotNumber);
        Assert.Equal("CHT-GOLD01-003", response.AssignedSlots[1].SlotDisplayCode);

        // Verify Payment in database
        var payment = await db.Payments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == response.PaymentId);
        Assert.NotNull(payment);
        Assert.Equal(20000m, payment.Amount);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.StartsWith("SIM-CHITU-", payment.ProviderReference);
        Assert.Equal(2, payment.Allocations.Count); // 2 instalment allocations

        // Verify customer received notification
        Assert.Single(notifService.SentNotifications);
        Assert.Equal("chitu_join", notifService.SentNotifications[0].Type);
    }

    [Fact]
    public async Task ConfirmAndJoin_IdempotentReplay_ReturnsExistingResponseWithoutDuplicatingSlots()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmAndJoin_IdempotentReplay_ReturnsExistingResponseWithoutDuplicatingSlots));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-IDEM01",
            Name = "Idempotency Plan",
            MonthlyInstalmentAmount = 5000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var joinRequest = new CustomerChituJoinRequest
        {
            Quantity = 2,
            IdempotencyKey = "stable-idempotency-key-01"
        };

        var firstResp = await service.ConfirmAndJoinAsync(customer.Id, plan.Id, joinRequest);
        var secondResp = await service.ConfirmAndJoinAsync(customer.Id, plan.Id, joinRequest);

        Assert.Equal(firstResp.PaymentId, secondResp.PaymentId);
        Assert.Equal(firstResp.TotalAmountPaid, secondResp.TotalAmountPaid);
        Assert.Equal(firstResp.AssignedSlots.Count, secondResp.AssignedSlots.Count);

        // Verify database slot count is still only 2
        var totalSlotsInDb = await db.ChituSlots.CountAsync(s => s.ChituPlanId == plan.Id);
        Assert.Equal(2, totalSlotsInDb);

        // Verify payments count is 1
        var totalPaymentsInDb = await db.Payments.CountAsync(p => p.CustomerId == customer.Id);
        Assert.Equal(1, totalPaymentsInDb);
    }

    [Fact]
    public async Task ConfirmAndJoin_AdditionalSlotsPurchase_SucceedsWithNewIdempotencyKey()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmAndJoin_AdditionalSlotsPurchase_SucceedsWithNewIdempotencyKey));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-MULTI01",
            Name = "Multiple Slots Plan",
            MonthlyInstalmentAmount = 5000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        // First purchase: 2 slots
        var resp1 = await service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
        {
            Quantity = 2,
            IdempotencyKey = "tx-purchase-1"
        });

        // Second intentional purchase: 1 slot with new key
        var resp2 = await service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
        {
            Quantity = 1,
            IdempotencyKey = "tx-purchase-2"
        });

        Assert.NotEqual(resp1.PaymentId, resp2.PaymentId);
        Assert.Equal(2, resp1.SlotsCount);
        Assert.Equal(1, resp2.SlotsCount);
        Assert.Equal(3, resp2.AssignedSlots[0].SlotNumber); // Next sequential slot

        var totalOwned = await db.ChituSlots.CountAsync(s => s.ChituPlanId == plan.Id && s.CustomerId == customer.Id);
        Assert.Equal(3, totalOwned);
    }

    #endregion

    #region 4. My Plans (Portfolio) Tests

    [Fact]
    public async Task GetInvestments_AccuratelyDistinguishesMonthlyCommitmentFromArrears()
    {
        var db = CreateInMemoryDbContext(nameof(GetInvestments_AccuratelyDistinguishesMonthlyCommitmentFromArrears));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Start date 2 months ago -> current round is 3
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-PORT01",
            Name = "Portfolio Test Plan",
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 120000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddMonths(-2),
            EndDate = today.AddMonths(9),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        // Slot 1: Fully paid through round 3 (Up to date: 0 arrears, owes 10000 next month)
        var slot1 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-PORT01-001",
            Status = ChituSlotStatus.Active
        };
        for (int m = 1; m <= 12; m++)
        {
            var isPaid = m <= 3;
            slot1.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 10000m,
                Status = isPaid ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }

        // Slot 2: Paid only round 1 (2 arrears rounds: round 2 and round 3 unpaid = 20,000 arrears)
        var slot2 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 2,
            SlotDisplayCode = "CHT-PORT01-002",
            Status = ChituSlotStatus.Active
        };
        for (int m = 1; m <= 12; m++)
        {
            var isPaid = m == 1;
            slot2.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 10000m,
                Status = isPaid ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }

        // Slot 3: Winning slot in round 2 (No longer incurs monthly commitments)
        var slot3 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 3,
            SlotDisplayCode = "CHT-PORT01-003",
            Status = ChituSlotStatus.Won,
            WinningRoundIndex = 2
        };
        for (int m = 1; m <= 12; m++)
        {
            var isPaid = m <= 2;
            slot3.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 10000m,
                Status = isPaid ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }

        db.ChituSlots.AddRange(slot1, slot2, slot3);
        await db.SaveChangesAsync();

        var investments = await service.GetInvestmentsAsync(customer.Id);

        Assert.Single(investments.Items);
        var planSummary = investments.Items[0];
        Assert.Equal(3, planSummary.TotalOwnedSlotsCount);
        Assert.Equal(2, planSummary.ActiveSlotsCount);
        Assert.Equal(1, planSummary.WinningSlotsCount);

        // Normal monthly commitment should only be for Slot 1 and Slot 2 (2 * 10000 = 20000). Winning slot 3 owes 0!
        Assert.Equal(20000m, planSummary.NormalMonthlyCommitment);

        // Actual outstanding due (arrears through round 3): Slot 1 owes 0, Slot 2 owes 20000 (months 2 and 3)
        Assert.Equal(20000m, planSummary.ActualOutstandingDueThroughCurrentRound);
    }

    #endregion

    #region 5. Existing Slot Payment Tests

    [Fact]
    public async Task PreviewAndConfirmPayment_DifferentInstalmentsPerSlot_OldestUnpaidAllocated()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewAndConfirmPayment_DifferentInstalmentsPerSlot_OldestUnpaidAllocated));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var notifService = new MockNotificationService();
        var service = CreateService(db, notifService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-PAY01",
            Name = "Payment Allocation Plan",
            MonthlyInstalmentAmount = 1000m,
            ChituBenefitValue = 12000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddMonths(-3), // Round 4
            EndDate = today.AddMonths(8),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        // Slot A: Paid Round 1. Overdue rounds 2, 3, 4. (Customer will pay 2 instalments -> covering 2 and 3)
        var slotA = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-PAY01-001",
            Status = ChituSlotStatus.Active
        };
        for (int m = 1; m <= 12; m++)
        {
            slotA.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 1000m,
                Status = m == 1 ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }

        // Slot B: Paid Rounds 1 and 2. Overdue rounds 3, 4. (Customer will pay 1 instalment -> covering round 3)
        var slotB = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 2,
            SlotDisplayCode = "CHT-PAY01-002",
            Status = ChituSlotStatus.Active
        };
        for (int m = 1; m <= 12; m++)
        {
            slotB.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 1000m,
                Status = m <= 2 ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }

        db.ChituSlots.AddRange(slotA, slotB);
        await db.SaveChangesAsync();

        // 1. Preview Payment: Slot A (2 instalments = 2000), Slot B (1 instalment = 1000) -> Total 3000
        var preview = await service.PreviewExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentPreviewRequest
        {
            SlotSelections = new List<CustomerSlotPaymentSelectionDto>
            {
                new() { SlotId = slotA.Id, InstalmentsToPayCount = 2 },
                new() { SlotId = slotB.Id, InstalmentsToPayCount = 1 }
            }
        });

        Assert.Equal(3000m, preview.TotalPayableAmount);
        var previewA = preview.Slots.First(s => s.SlotId == slotA.Id);
        Assert.Equal(new[] { 2, 3 }, previewA.ClearedRounds);
        Assert.Equal(1000m, previewA.RemainingArrearsAfterPayment); // Round 4 still overdue!
        Assert.False(previewA.ResultingRaffleEligible); // Not eligible because round 4 is still unpaid!

        var previewB = preview.Slots.First(s => s.SlotId == slotB.Id);
        Assert.Equal(new[] { 3 }, previewB.ClearedRounds);
        Assert.Equal(1000m, previewB.RemainingArrearsAfterPayment); // Round 4 still overdue!

        // 2. Confirm Payment
        var confirmResp = await service.ConfirmExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentConfirmRequest
        {
            SlotSelections = new List<CustomerSlotPaymentSelectionDto>
            {
                new() { SlotId = slotA.Id, InstalmentsToPayCount = 2 },
                new() { SlotId = slotB.Id, InstalmentsToPayCount = 1 }
            },
            IdempotencyKey = "pay-key-123"
        });

        Assert.Equal(3000m, confirmResp.TotalAmount);
        Assert.True(confirmResp.IsSimulated);

        // Verify instalments in db:
        // Slot A now has rounds 1, 2, 3 Paid; round 4 is Pending.
        var updatedSlotA = await db.ChituSlots.Include(s => s.Instalments).FirstAsync(s => s.Id == slotA.Id);
        Assert.Equal(InstalmentStatus.Paid, updatedSlotA.Instalments.First(i => i.MonthIndex == 2).Status);
        Assert.Equal(InstalmentStatus.Paid, updatedSlotA.Instalments.First(i => i.MonthIndex == 3).Status);
        Assert.Equal(InstalmentStatus.Pending, updatedSlotA.Instalments.First(i => i.MonthIndex == 4).Status);

        // Verify single financial receipt was stored
        var payment = await db.Payments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == confirmResp.PaymentId);
        Assert.NotNull(payment);
        Assert.Equal(3000m, payment.Amount);
        Assert.Equal(3, payment.Allocations.Count); // 2 for Slot A + 1 for Slot B
    }

    [Fact]
    public async Task ConfirmPayment_SlotAlreadyWon_RejectedForPayment()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmPayment_SlotAlreadyWon_RejectedForPayment));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-WON01",
            Name = "Winner Test Plan",
            MonthlyInstalmentAmount = 1000m,
            TotalSlotCapacity = 20,
            DurationMonths = 12,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(10)),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        var winningSlot = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-WON01-001",
            Status = ChituSlotStatus.Won,
            WinningRoundIndex = 1
        };
        db.ChituSlots.Add(winningSlot);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentPreviewRequest
            {
                SlotSelections = new List<CustomerSlotPaymentSelectionDto>
                {
                    new() { SlotId = winningSlot.Id, InstalmentsToPayCount = 1 }
                }
            }));

        Assert.Contains("Winning or closed slots cannot receive payments", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_AttemptToPayOtherCustomerSlot_ThrowsKeyNotFoundOrUnauthorized()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmPayment_AttemptToPayOtherCustomerSlot_ThrowsKeyNotFoundOrUnauthorized));
        var (jaffna, _, customer, otherCustomer) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-SEC01",
            Name = "Security Plan",
            MonthlyInstalmentAmount = 1000m,
            TotalSlotCapacity = 20,
            DurationMonths = 12,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(11)),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        // Slot belongs to otherCustomer
        var otherSlot = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = otherCustomer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-SEC01-001",
            Status = ChituSlotStatus.Active
        };
        db.ChituSlots.Add(otherSlot);
        await db.SaveChangesAsync();

        // Customer attempts to pay otherSlot
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.PreviewExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentPreviewRequest
            {
                SlotSelections = new List<CustomerSlotPaymentSelectionDto>
                {
                    new() { SlotId = otherSlot.Id, InstalmentsToPayCount = 1 }
                }
            }));
    }

    [Fact]
    public async Task ConfirmJoin_MismatchedExpectedAmount_ThrowsChituPreviewExpiredOrChangedException()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmJoin_MismatchedExpectedAmount_ThrowsChituPreviewExpiredOrChangedException));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-DRIFT",
            Name = "Drift Test Plan",
            MonthlyInstalmentAmount = 5000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(5),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        // Customer expects initial amount to be 4000, but required amount is 5000
        var ex = await Assert.ThrowsAsync<ChituPreviewExpiredOrChangedException>(() =>
            service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
            {
                Quantity = 1,
                IdempotencyKey = "key-drift",
                ExpectedTotalInitialAmount = 4000m
            }));

        Assert.Equal(ChituPreviewExpiredOrChangedException.ErrorCode, ex.Code);
        Assert.Equal(4000m, ex.ExpectedAmount);
        Assert.Equal(5000m, ex.ActualAmount);
    }

    [Fact]
    public async Task PayingOneMonthFromThreeOverdue_LeavesTwoOverdueAndIneligibleForRaffle()
    {
        var db = CreateInMemoryDbContext(nameof(PayingOneMonthFromThreeOverdue_LeavesTwoOverdueAndIneligibleForRaffle));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Start date 3 months ago -> Current round is 4
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-ARREARS",
            Name = "Arrears Test Plan",
            MonthlyInstalmentAmount = 2000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddMonths(-3),
            EndDate = today.AddMonths(8),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        // Slot has paid Round 1. Rounds 2, 3, 4 are overdue (3 overdue months)
        var slot = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-ARR-001",
            Status = ChituSlotStatus.Active
        };
        for (int m = 1; m <= 12; m++)
        {
            slot.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 2000m,
                Status = m == 1 ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }
        db.ChituSlots.Add(slot);
        await db.SaveChangesAsync();

        // Customer chooses to pay only 1 full instalment out of 3 overdue months
        var preview = await service.PreviewExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentPreviewRequest
        {
            SlotSelections = new List<CustomerSlotPaymentSelectionDto>
            {
                new() { SlotId = slot.Id, InstalmentsToPayCount = 1 }
            }
        });

        Assert.Equal(2000m, preview.TotalPayableAmount);
        var slotPreview = preview.Slots[0];
        Assert.Equal(new[] { 2 }, slotPreview.ClearedRounds); // Oldest unpaid (round 2) cleared
        Assert.Equal(4000m, slotPreview.RemainingArrearsAfterPayment); // Rounds 3 and 4 remain in arrears!
        Assert.False(slotPreview.ResultingRaffleEligible); // Slot is NOT raffle eligible because arrears remain through round 4

        // Post payment
        var confirm = await service.ConfirmExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentConfirmRequest
        {
            SlotSelections = new List<CustomerSlotPaymentSelectionDto>
            {
                new() { SlotId = slot.Id, InstalmentsToPayCount = 1 }
            },
            IdempotencyKey = "pay-one-of-three"
        });

        Assert.Equal(2000m, confirm.TotalAmount);

        // Verify slot status in portfolio
        var slotDetail = await service.GetSlotDetailsAsync(customer.Id, slot.Id);
        Assert.Equal(2, slotDetail.PaidThroughRound); // Paid through round 2
        Assert.Equal(2, slotDetail.ArrearsMonths); // Rounds 3 and 4 in arrears
        Assert.Equal(4000m, slotDetail.ArrearsAmount);
        Assert.False(slotDetail.IsRaffleEligible);
        Assert.Contains("round 3 is unpaid", slotDetail.RaffleEligibilityReason);
    }

    [Fact]
    public async Task AdvancePayments_AllowedUpToDuration_ExceedingThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext(nameof(AdvancePayments_AllowedUpToDuration_ExceedingThrowsArgumentException));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-ADV01",
            Name = "Advance Payments Plan",
            MonthlyInstalmentAmount = 1000m,
            TotalSlotCapacity = 50,
            DurationMonths = 6, // 6 months total
            StartDate = today,
            EndDate = today.AddMonths(5),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        // Slot has paid Round 1. Remaining unpaid rounds = 5 (Rounds 2, 3, 4, 5, 6)
        var slot = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-ADV-001",
            Status = ChituSlotStatus.Active
        };
        for (int m = 1; m <= 6; m++)
        {
            slot.Instalments.Add(new ChituInstalment
            {
                MonthIndex = m,
                DueDate = plan.StartDate.AddMonths(m - 1),
                AmountDue = 1000m,
                Status = m == 1 ? InstalmentStatus.Paid : InstalmentStatus.Pending
            });
        }
        db.ChituSlots.Add(slot);
        await db.SaveChangesAsync();

        // 1. Advance pay all remaining 5 months -> Allowed
        var preview = await service.PreviewExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentPreviewRequest
        {
            SlotSelections = new List<CustomerSlotPaymentSelectionDto>
            {
                new() { SlotId = slot.Id, InstalmentsToPayCount = 5 }
            }
        });

        Assert.Equal(5000m, preview.TotalPayableAmount);
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, preview.Slots[0].ClearedRounds);
        Assert.True(preview.Slots[0].ResultingRaffleEligible);

        // 2. Attempt to pay 6 instalments when only 5 remain unpaid -> Throws ArgumentException
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentPreviewRequest
            {
                SlotSelections = new List<CustomerSlotPaymentSelectionDto>
                {
                    new() { SlotId = slot.Id, InstalmentsToPayCount = 6 }
                }
            }));

        Assert.Contains("only has 5 remaining unpaid instalment(s)", ex.Message);
    }

    [Fact]
    public async Task PreviewJoinAsync_ReturnsPlanCode()
    {
        var db = CreateInMemoryDbContext(nameof(PreviewJoinAsync_ReturnsPlanCode));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-PREV-CODE",
            Name = "Preview Plan",
            MonthlyInstalmentAmount = 1000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(7),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var preview = await service.PreviewJoinAsync(customer.Id, plan.Id, new CustomerChituJoinPreviewRequest { Quantity = 2 });

        Assert.Equal("CHT-PREV-CODE", preview.PlanCode);
        Assert.Equal(2000m, preview.TotalInitialAmountPayable);
    }

    [Fact]
    public async Task ConfirmAndJoin_IdempotencyKeyMismatchPayload_ThrowsChituIdempotencyConflictException()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmAndJoin_IdempotencyKeyMismatchPayload_ThrowsChituIdempotencyConflictException));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-IDEMP-MISMATCH",
            Name = "Idempotency Mismatch Test Plan",
            MonthlyInstalmentAmount = 1000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddDays(7),
            EndDate = today.AddMonths(11),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);
        await db.SaveChangesAsync();

        var idempKey = "cust-idemp-dup-key";

        // First join succeeds
        var resp1 = await service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
        {
            Quantity = 1,
            IdempotencyKey = idempKey,
            ExpectedTotalInitialAmount = 1000m
        });
        Assert.Single(resp1.AssignedSlots);

        // Second join with same key but DIFFERENT quantity throws ChituIdempotencyConflictException
        var ex = await Assert.ThrowsAsync<ChituIdempotencyConflictException>(() =>
            service.ConfirmAndJoinAsync(customer.Id, plan.Id, new CustomerChituJoinRequest
            {
                Quantity = 2,
                IdempotencyKey = idempKey,
                ExpectedTotalInitialAmount = 2000m
            }));

        Assert.Equal("IdempotencyConflict", ex.Code);
    }

    [Fact]
    public async Task ConfirmExistingSlotPayment_SimulationDisabled_ThrowsPaymentProviderNotConfiguredException()
    {
        var db = CreateInMemoryDbContext(nameof(ConfirmExistingSlotPayment_SimulationDisabled_ThrowsPaymentProviderNotConfiguredException));
        var (jaffna, _, customer, _) = await SetupTestDataAsync(db);
        var service = CreateService(db, new MockNotificationService(), enableSimulation: false);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = jaffna.Id,
            Code = "CHT-SIM-OFF-PAY",
            Name = "Sim Off Payment Plan",
            MonthlyInstalmentAmount = 1000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = today.AddMonths(-1),
            EndDate = today.AddMonths(10),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(plan);

        var slot = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = plan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-SIM-001",
            Status = ChituSlotStatus.Active
        };
        slot.Instalments.Add(new ChituInstalment
        {
            MonthIndex = 1,
            DueDate = plan.StartDate,
            AmountDue = 1000m,
            Status = InstalmentStatus.Pending
        });
        db.ChituSlots.Add(slot);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PaymentProviderNotConfiguredException>(() =>
            service.ConfirmExistingSlotPaymentAsync(customer.Id, plan.Id, new CustomerChituPaymentConfirmRequest
            {
                SlotSelections = new List<CustomerSlotPaymentSelectionDto>
                {
                    new() { SlotId = slot.Id, InstalmentsToPayCount = 1 }
                },
                IdempotencyKey = "key-no-sim"
            }));

        Assert.Equal(PaymentProviderNotConfiguredException.ErrorCode, ex.Code);
    }

    #endregion
}
