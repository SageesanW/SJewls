using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class AdminChituTests
{
    private SJewlsDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SJewlsDbContext(options);
    }

    private class MockAuditLogService : SJewls.Application.Interfaces.IAuditLogService
    {
        public Task LogActionAsync(string actorType, Guid? actorId, string action, string targetEntity, string targetId, Guid? branchId, object? before, object? after, string? ipAddress)
        {
            return Task.CompletedTask;
        }
    }

    private async Task<(Branch branch, Staff staff, Customer customer)> SetupTestEnvironmentAsync(SJewlsDbContext db)
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
            FullName = "Chitu Manager",
            Username = "chitumanager",
            Email = "chitu.admin@sjewls.lk",
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

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            PrimaryBranchId = branch.Id,
            FullName = "Kamal Perera",
            PhoneNumber = "+94771234567",
            Email = "kamal@example.lk",
            IsActive = true
        };
        db.Customers.Add(customer);

        await db.SaveChangesAsync();
        return (branch, staff, customer);
    }

    [Fact]
    public async Task CreatePlan_GeneratesExactSchedule_AndClampsMonthEndWithoutDrift()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, _) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        // Jan 31st start (future date), 12 months duration
        var startDate = new DateOnly(2027, 1, 31);
        var req = new CreateChituPlanRequest
        {
            Name = "Gold Saver 2027",
            Code = "GS2027",
            RedemptionType = ChituRedemptionType.Gold,
            MonthlyInstalmentAmount = 10000m,
            DurationMonths = 12,
            StartDate = startDate,
            TotalSlotCapacity = 50,
            BranchId = branch.Id
        };

        var plan = await service.CreatePlanAsync(staff.Id, req);

        Assert.Equal("GS2027", plan.Code);
        Assert.Equal(ChituRedemptionType.Gold, plan.RedemptionType);
        Assert.Equal(new DateOnly(2027, 12, 31), plan.EndDate);

        // Fetch detail to view schedule rounds
        var detail = await service.GetPlanByIdAsync(staff.Id, plan.Id);
        Assert.Equal(12, detail.Schedule.Count);

        // Verify no month-end drift and correct month-end clamping
        Assert.Equal(new DateOnly(2027, 1, 31), detail.Schedule[0].ScheduledRaffleDate);
        // Feb 2027 has 28 days
        Assert.Equal(new DateOnly(2027, 2, 28), detail.Schedule[1].ScheduledRaffleDate);
        // Mar has 31 days
        Assert.Equal(new DateOnly(2027, 3, 31), detail.Schedule[2].ScheduledRaffleDate);
        // Apr has 30 days
        Assert.Equal(new DateOnly(2027, 4, 30), detail.Schedule[3].ScheduledRaffleDate);
        // Final round (Round 12) is Dec 31
        Assert.Equal(new DateOnly(2027, 12, 31), detail.Schedule[11].ScheduledRaffleDate);
    }

    [Fact]
    public async Task AssignSlots_EnforcesSequentialNumbering_AndInitialPayment()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Cash Quick Plan",
            Code = "CASH100",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 5000m,
            DurationMonths = 6,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalSlotCapacity = 5,
            BranchId = branch.Id
        });

        // Preview assignment of 2 slots
        var preview = await service.PreviewAssignmentAsync(staff.Id, new ChituSlotAssignmentPreviewRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 2,
            AdditionalPrepaidMonths = 0
        });

        Assert.Equal(2, preview.SlotsCount);
        Assert.Equal(2, preview.AssignedSlotNumbers.Count);
        Assert.Equal(1, preview.AssignedSlotNumbers[0]);
        Assert.Equal(2, preview.AssignedSlotNumbers[1]);
        Assert.Equal("CASH100-001", preview.AssignedSlotDisplayCodes[0]);
        Assert.Equal("CASH100-002", preview.AssignedSlotDisplayCodes[1]);
        Assert.Equal(10000m, preview.TotalPayableAmount); // 5000 * 2 for Round 1

        // Assign 2 slots with payment
        var assignResult = await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 2,
            PaymentMethod = PaymentMethod.Cash
        });

        Assert.Equal(2, assignResult.CreatedSlots.Count);
        Assert.Equal(10000m, assignResult.TotalAmountPaid);

        // Assign 1 more slot - should get slot number 3 (no recycling, sequential)
        var assignResult2 = await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 1,
            PaymentMethod = PaymentMethod.Cash
        });

        Assert.Single(assignResult2.CreatedSlots);
        Assert.Equal(3, assignResult2.CreatedSlots[0].SlotNumber);
        Assert.Equal("CASH100-003", assignResult2.CreatedSlots[0].SlotDisplayCode);
    }

    [Fact]
    public async Task AssignSlots_RejectsExceedingCapacity()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Limited Plan",
            Code = "LIM01",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 5000m,
            DurationMonths = 6,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            TotalSlotCapacity = 2,
            BranchId = branch.Id
        });

        // Assign 2 slots to fill capacity
        await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 2,
            PaymentMethod = PaymentMethod.Cash
        });

        // Attempting to assign 1 more slot exceeds capacity of 2
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 1,
            PaymentMethod = PaymentMethod.Cash
        }));
    }

    [Fact]
    public async Task Payment_EnforcesOldestUnpaidFirst_AndWholeMultiplesOnly()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Payment Test Plan",
            Code = "PAY01",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 2000m,
            DurationMonths = 5,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            TotalSlotCapacity = 10,
            BranchId = branch.Id
        });

        var assignResult = await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 1,
            PaymentMethod = PaymentMethod.Cash
        });

        var slotId = assignResult.CreatedSlots[0].Id;

        // Non-multiple amount should be rejected
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordPaymentAsync(staff.Id, new RecordChituPaymentRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            SlotAllocations = new List<ChituSlotAllocationInputDto>
            {
                new() { SlotId = slotId, Amount = 2500m } // Invalid, not a multiple of 2000
            }
        }));

        // Overpayment beyond remaining 4 rounds (8000 max) should be rejected
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordPaymentAsync(staff.Id, new RecordChituPaymentRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            SlotAllocations = new List<ChituSlotAllocationInputDto>
            {
                new() { SlotId = slotId, Amount = 10000m } // 5 months total, but round 1 is already paid!
            }
        }));

        // Valid payment of 2 months (4000m)
        var paymentResult = await service.RecordPaymentAsync(staff.Id, new RecordChituPaymentRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            PaymentMethod = PaymentMethod.Cash,
            SlotAllocations = new List<ChituSlotAllocationInputDto>
            {
                new() { SlotId = slotId, Amount = 4000m }
            }
        });

        Assert.Equal(4000m, paymentResult.TotalAmount);
        Assert.Single(paymentResult.SlotReceipts);
        // Rounds 2 and 3 should be cleared
        Assert.Equal(2, paymentResult.SlotReceipts[0].MonthsCovered);
        Assert.Equal(new List<int> { 2, 3 }, paymentResult.SlotReceipts[0].ClearedMonthIndices);
    }

    [Fact]
    public async Task PhysicalRaffle_RequiresChronologicalOrder_Eligibility_AndEndsWinnerObligations()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        // Create plan starting today
        var initialStartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Raffle Test Plan",
            Code = "RAF01",
            RedemptionType = ChituRedemptionType.Gold,
            MonthlyInstalmentAmount = 5000m,
            DurationMonths = 10,
            StartDate = initialStartDate,
            TotalSlotCapacity = 10,
            BranchId = branch.Id
        });

        // Assign Slot 1 with 2 additional prepaid months (3 months total: rounds 1, 2, 3) = 15000m
        var assignResult1 = await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 1,
            AdditionalPrepaidMonths = 2,
            PaymentMethod = PaymentMethod.Cash
        });
        var slot1Id = assignResult1.CreatedSlots[0].Id;

        // Create Customer 2 with Slot 2
        var customer2 = new Customer
        {
            Id = Guid.NewGuid(),
            PrimaryBranchId = branch.Id,
            FullName = "Nimal Silva",
            PhoneNumber = "+94777654321",
            IsActive = true
        };
        db.Customers.Add(customer2);
        await db.SaveChangesAsync();

        var assignResult2 = await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer2.Id,
            SlotsCount = 1,
            AdditionalPrepaidMonths = 0,
            PaymentMethod = PaymentMethod.Cash
        });
        var slot2Id = assignResult2.CreatedSlots[0].Id;

        // Now shift plan start date to 2 months ago to simulate elapsed rounds 1 and 2
        var elapsedStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2));
        var planDb = await db.ChituPlans.FirstAsync(p => p.Id == plan.Id);
        planDb.StartDate = elapsedStartDate;
        planDb.EndDate = elapsedStartDate.AddMonths(10 - 1);
        foreach (var inst in await db.ChituInstalments.ToListAsync())
        {
            inst.DueDate = elapsedStartDate.AddMonths(inst.MonthIndex - 1);
        }
        await db.SaveChangesAsync();

        // Check Round 1 eligible slots (both slot 1 and slot 2 covered Round 1)
        var eligibleForRound1 = await service.GetEligibleRaffleSlotsAsync(staff.Id, plan.Id, 1);
        Assert.Equal(2, eligibleForRound1.EligibleCount);

        // Attempting to record Round 2 before Round 1 should be rejected (chronological order)
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordRaffleDrawAsync(staff.Id, plan.Id, 2, new RecordChituRaffleRequest
        {
            WinningSlotId = slot1Id
        }));

        // Record Round 1 winner = Slot 1
        var raffle1Result = await service.RecordRaffleDrawAsync(staff.Id, plan.Id, 1, new RecordChituRaffleRequest
        {
            WinningSlotId = slot1Id,
            Remarks = "Physical raffle drawn at Colombo showroom"
        });

        Assert.Equal(1, raffle1Result.RoundIndex);
        Assert.Equal("RAF01-001", raffle1Result.WinningSlotDisplayCode);
        // Fixed entitlement: 5000 * 10 = 50000m
        Assert.Equal(50000m, raffle1Result.WinningEntitlementValue);
        // Slot 1 had prepaid rounds 2 and 3 (10000m). Post-winning prepaid excess is flagged!
        Assert.Equal(10000m, raffle1Result.AdvanceExcessPrepaidAmount);
        Assert.True(raffle1Result.AdvanceExcessSettlementDecisionRequired);

        // Check Slot 1 state: is won, excluded from future raffles, future payment obligations ended
        var slot1 = await db.ChituSlots.Include(s => s.Instalments).FirstAsync(s => s.Id == slot1Id);
        Assert.Equal(ChituSlotStatus.Won, slot1.Status);
        Assert.Equal(1, slot1.WinningRoundIndex);
        Assert.Equal(10000m, slot1.PrepaidExcessAmount);

        // In Round 2, Slot 1 is excluded from eligible slots
        var eligibleForRound2 = await service.GetEligibleRaffleSlotsAsync(staff.Id, plan.Id, 2);
        // Slot 2 only paid round 1 at assignment, so it has not paid round 2 yet!
        // Therefore eligible count should be 0 until slot 2 pays round 2
        Assert.Equal(0, eligibleForRound2.EligibleCount);

        // Pay round 2 for Slot 2
        await service.RecordPaymentAsync(staff.Id, new RecordChituPaymentRequest
        {
            PlanId = plan.Id,
            CustomerId = customer2.Id,
            PaymentMethod = PaymentMethod.Cash,
            SlotAllocations = new List<ChituSlotAllocationInputDto>
            {
                new() { SlotId = slot2Id, Amount = 5000m }
            }
        });

        // Now Slot 2 is eligible for Round 2
        var eligibleForRound2AfterPayment = await service.GetEligibleRaffleSlotsAsync(staff.Id, plan.Id, 2);
        Assert.Equal(1, eligibleForRound2AfterPayment.EligibleCount);
        Assert.Equal(slot2Id, eligibleForRound2AfterPayment.EligibleSlots[0].SlotId);

        // Verify search filtering by customer name and slot number
        var searchedByName = await service.GetEligibleRaffleSlotsAsync(staff.Id, plan.Id, 2, search: "Nimal");
        Assert.Equal(1, searchedByName.EligibleCount);
        Assert.Equal(slot2Id, searchedByName.EligibleSlots[0].SlotId);

        var searchedBySlotNumber = await service.GetEligibleRaffleSlotsAsync(staff.Id, plan.Id, 2, search: "2");
        Assert.Equal(1, searchedBySlotNumber.EligibleCount);

        var searchedByNonExistent = await service.GetEligibleRaffleSlotsAsync(staff.Id, plan.Id, 2, search: "NonExistentCustomer");
        Assert.Equal(0, searchedByNonExistent.EligibleCount);

        // Cannot draw round 1 again
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordRaffleDrawAsync(staff.Id, plan.Id, 1, new RecordChituRaffleRequest
        {
            WinningSlotId = slot2Id
        }));
    }

    [Fact]
    public async Task CreatePlan_RejectsDuplicateCaseNormalizedCodeInSameBranch()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, _) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Diamond Plan",
            Code = "DIA-01",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 10000m,
            DurationMonths = 12,
            StartDate = startDate,
            TotalSlotCapacity = 20,
            BranchId = branch.Id
        });

        // Attempting to create with same code in different case "dia-01" must be rejected
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Duplicate Diamond",
            Code = "dia-01 ",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 10000m,
            DurationMonths = 12,
            StartDate = startDate,
            TotalSlotCapacity = 20,
            BranchId = branch.Id
        }));
    }

    [Fact]
    public async Task RosterQuery_ReturnsAccuratePerSlotMetrics_AndFilters()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            Name = "Roster Metrics Plan",
            Code = "ROS01",
            RedemptionType = ChituRedemptionType.Cash,
            MonthlyInstalmentAmount = 3000m,
            DurationMonths = 6,
            StartDate = startDate,
            TotalSlotCapacity = 10,
            BranchId = branch.Id
        });

        // Customer assigns 2 slots:
        // Slot 1: Round 1 + Round 2 prepaid (6000m)
        // Slot 2: Round 1 only (3000m)
        // Customer 2 assigns 1 slot:
        // Slot 3: Round 1 only (3000m)
        await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 1,
            AdditionalPrepaidMonths = 1, // Pays rounds 1 and 2
            PaymentMethod = PaymentMethod.Cash
        });

        await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 1,
            AdditionalPrepaidMonths = 0, // Pays round 1 only
            PaymentMethod = PaymentMethod.Cash
        });

        // Check Roster for Round 1
        var rosterRound1 = await service.GetRosterAsync(staff.Id, plan.Id, new ChituRosterQueryParameters
        {
            RoundIndex = 1
        });

        Assert.Equal(2, rosterRound1.AssignedSlots);
        Assert.Equal(2, rosterRound1.RoundPaidSlotsCount);
        Assert.Equal(0, rosterRound1.RoundUnpaidSlotsCount);

        // Check Roster for Round 2
        var rosterRound2 = await service.GetRosterAsync(staff.Id, plan.Id, new ChituRosterQueryParameters
        {
            RoundIndex = 2
        });

        Assert.Equal(2, rosterRound2.AssignedSlots);
        // In Round 2: Slot 1 has prepaid round 2 (Paid = 1), Slot 2 has NOT paid round 2 (Unpaid = 1)
        Assert.Equal(1, rosterRound2.RoundPaidSlotsCount);
        Assert.Equal(1, rosterRound2.RoundUnpaidSlotsCount);

        // Filter by Unpaid in Round 2
        var unpaidFilter = await service.GetRosterAsync(staff.Id, plan.Id, new ChituRosterQueryParameters
        {
            RoundIndex = 2,
            PaymentFilter = "Unpaid"
        });

        Assert.Single(unpaidFilter.Rows);
        Assert.Equal("ROS01-002", unpaidFilter.Rows[0].SlotDisplayCode);
        Assert.False(unpaidFilter.Rows[0].IsPaidForSelectedRound);
    }

    [Fact]
    public async Task UpdatePlan_WhenNoSlotsAssigned_AllowsFullConfigurationChanges()
    {
        var db = CreateInMemoryDbContext(nameof(UpdatePlan_WhenNoSlotsAssigned_AllowsFullConfigurationChanges));
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            BranchId = branch.Id,
            Code = "UP01",
            Name = "Initial Plan Name",
            DurationMonths = 10,
            MonthlyInstalmentAmount = 10000m,
            TotalSlotCapacity = 50,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            RedemptionType = ChituRedemptionType.Gold,
            Description = "Initial description"
        });

        // Update all configuration parameters
        var updated = await service.UpdatePlanAsync(staff.Id, plan.Id, new UpdateChituPlanRequest
        {
            Name = "Updated Plan Name",
            Code = "UP01-NEW",
            Description = "Updated description",
            TotalSlotCapacity = 80,
            DurationMonths = 12,
            MonthlyInstalmentAmount = 15000m,
            RedemptionType = ChituRedemptionType.Cash,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
        });

        Assert.Equal("Updated Plan Name", updated.Name);
        Assert.Equal("UP01-NEW", updated.Code);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(80, updated.TotalSlotCapacity);
        Assert.Equal(12, updated.DurationMonths);
        Assert.Equal(15000m, updated.MonthlyInstalmentAmount);
        Assert.Equal(ChituRedemptionType.Cash, updated.RedemptionType);
        Assert.Equal(180000m, updated.ChituBenefitValue); // 12 * 15000
    }

    [Fact]
    public async Task UpdatePlan_WhenSlotsAssigned_PreventsCoreTermsChangeAndCapacityReduction()
    {
        var db = CreateInMemoryDbContext(nameof(UpdatePlan_WhenSlotsAssigned_PreventsCoreTermsChangeAndCapacityReduction));
        var (branch, staff, customer) = await SetupTestEnvironmentAsync(db);
        var service = new ChituService(db, new MockAuditLogService(), NullLogger<ChituService>.Instance);

        var plan = await service.CreatePlanAsync(staff.Id, new CreateChituPlanRequest
        {
            BranchId = branch.Id,
            Code = "UP02",
            Name = "Active Plan with Slots",
            DurationMonths = 10,
            MonthlyInstalmentAmount = 10000m,
            TotalSlotCapacity = 10,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            RedemptionType = ChituRedemptionType.Gold
        });

        // Assign 2 slots
        await service.AssignSlotsAsync(staff.Id, new AssignChituSlotsRequest
        {
            PlanId = plan.Id,
            CustomerId = customer.Id,
            SlotsCount = 2,
            PaymentMethod = PaymentMethod.Cash
        });

        // 1. Attempting to reduce capacity below 2 assigned slots must fail
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePlanAsync(staff.Id, plan.Id, new UpdateChituPlanRequest
            {
                TotalSlotCapacity = 1
            }));

        // 2. Attempting to change duration when slots are assigned must fail
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePlanAsync(staff.Id, plan.Id, new UpdateChituPlanRequest
            {
                DurationMonths = 20
            }));

        // 3. Safe updates (Name, Description, Expanding capacity to 20) succeed
        var updated = await service.UpdatePlanAsync(staff.Id, plan.Id, new UpdateChituPlanRequest
        {
            Name = "Renamed Active Plan",
            Description = "Safe remarks",
            TotalSlotCapacity = 20
        });

        Assert.Equal("Renamed Active Plan", updated.Name);
        Assert.Equal("Safe remarks", updated.Description);
        Assert.Equal(20, updated.TotalSlotCapacity);
        Assert.Equal(2, updated.AssignedSlotsCount);
        Assert.Equal(18, updated.AvailableSlotsCount);
    }
}
