using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class CustomerPaymentHistoryTests
{
    private SJewlsDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SJewlsDbContext(options);
    }

    [Fact]
    public async Task UnifiedPaymentHistory_ReturnsBothChituAndJewelleryPayments_UnmultipliedTotals()
    {
        var db = CreateInMemoryDbContext(nameof(UnifiedPaymentHistory_ReturnsBothChituAndJewelleryPayments_UnmultipliedTotals));
        var service = new CustomerPaymentService(db, NullLogger<CustomerPaymentService>.Instance);

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Jaffna Heritage Branch",
            Code = "JAF-01",
            Timezone = "Asia/Colombo",
            IsActive = true
        };
        db.Branches.Add(branch);

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FullName = "Sivaram Tharmalingam",
            PhoneNumber = "+94771234567",
            Nic = "199212345678",
            PrimaryBranchId = branch.Id,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Customers.Add(customer);

        // 1. Chitu Plan & Slot
        var chituPlan = new ChituPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Code = "CHT-TEST",
            Name = "Chitu Savings Plan",
            MonthlyInstalmentAmount = 5000m,
            TotalSlotCapacity = 50,
            DurationMonths = 12,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(11)),
            Status = PlanStatus.Active
        };
        db.ChituPlans.Add(chituPlan);

        var slot1 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = chituPlan.Id,
            CustomerId = customer.Id,
            SlotNumber = 1,
            SlotDisplayCode = "CHT-TEST-001",
            Status = ChituSlotStatus.Active
        };
        var slot2 = new ChituSlot
        {
            Id = Guid.NewGuid(),
            ChituPlanId = chituPlan.Id,
            CustomerId = customer.Id,
            SlotNumber = 2,
            SlotDisplayCode = "CHT-TEST-002",
            Status = ChituSlotStatus.Active
        };
        db.ChituSlots.AddRange(slot1, slot2);

        // Multi-slot Chitu payment of 10,000 covering Slot 1 and Slot 2
        var chituPayment = new Payment
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CustomerId = customer.Id,
            Module = BusinessModule.Chitu,
            Amount = 10000m,
            Currency = "LKR",
            Method = PaymentMethod.MockOnline,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "SIM-CHITU-12345",
            Remarks = "[SIMULATED] Provider=Simulated Source=CustomerMobile Mode=Test. Initial joining payment for 2 slot(s)",
            CompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.Payments.Add(chituPayment);

        var inst1 = new ChituInstalment
        {
            Id = Guid.NewGuid(),
            ChituSlotId = slot1.Id,
            MonthIndex = 1,
            DueDate = chituPlan.StartDate,
            AmountDue = 5000m,
            Status = InstalmentStatus.Paid,
            PaymentId = chituPayment.Id
        };
        var inst2 = new ChituInstalment
        {
            Id = Guid.NewGuid(),
            ChituSlotId = slot2.Id,
            MonthIndex = 1,
            DueDate = chituPlan.StartDate,
            AmountDue = 5000m,
            Status = InstalmentStatus.Paid,
            PaymentId = chituPayment.Id
        };
        db.ChituInstalments.AddRange(inst1, inst2);

        // 2. Jewellery Plan & Enrolment
        var jewelleryPlan = new JewelleryPlan
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            Name = "Swarna Gold Accumulation",
            TargetProductGoldWeightGrams = 20m,
            PremiumPercentage = 5.0m,
            IsActive = true
        };
        db.JewelleryPlans.Add(jewelleryPlan);

        var enrolment = new JewelleryEnrolment
        {
            Id = Guid.NewGuid(),
            JewelleryPlanId = jewelleryPlan.Id,
            CustomerId = customer.Id,
            BranchId = branch.Id,
            PlanNameSnapshot = jewelleryPlan.Name,
            TargetProductWeightGrams = 20m,
            PremiumPercentage = 5.0m,
            Status = JewelleryEnrolmentStatus.Active
        };
        db.JewelleryEnrolments.Add(enrolment);

        // Jewellery payment of 25,000 crediting gold grams
        var jewelleryPayment = new Payment
        {
            Id = Guid.NewGuid(),
            BranchId = branch.Id,
            CustomerId = customer.Id,
            Module = BusinessModule.Jewellery,
            Amount = 25000m,
            Currency = "LKR",
            Method = PaymentMethod.BankTransfer,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "TXN-JEW-789",
            CompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Payments.Add(jewelleryPayment);

        var contribution = new JewelleryContribution
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CurrencyAmount = 25000m,
            GoldGrams = 0.852m,
            AppliedRatePerGram = 28000m,
            Karat = 24,
            PaymentId = jewelleryPayment.Id,
            ConfirmedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.JewelleryContributions.Add(contribution);

        await db.SaveChangesAsync();

        // 1. Fetch All Payments (Unified)
        var allHistory = await service.GetPaymentHistoryAsync(customer.Id, planType: "All");
        Assert.Equal(2, allHistory.TotalCount);

        // Check that multi-slot Chitu payment appears ONCE with 10,000 amount
        var chituSummary = allHistory.Items.First(p => p.PaymentId == chituPayment.Id);
        Assert.Equal("Chitu", chituSummary.PlanType);
        Assert.Equal(10000m, chituSummary.Amount);
        Assert.Equal(2, chituSummary.SlotsCoveredCount);
        Assert.True(chituSummary.IsSimulated);

        // Check Jewellery payment
        var jewellerySummary = allHistory.Items.First(p => p.PaymentId == jewelleryPayment.Id);
        Assert.Equal("Jewellery", jewellerySummary.PlanType);
        Assert.Equal(25000m, jewellerySummary.Amount);
        Assert.False(jewellerySummary.IsSimulated);

        // 2. Filter by planType = "Chitu"
        var chituOnly = await service.GetPaymentHistoryAsync(customer.Id, planType: "Chitu");
        Assert.Single(chituOnly.Items);
        Assert.Equal(chituPayment.Id, chituOnly.Items[0].PaymentId);

        // 3. Filter by planType = "Jewellery"
        var jewOnly = await service.GetPaymentHistoryAsync(customer.Id, planType: "Jewellery");
        Assert.Single(jewOnly.Items);
        Assert.Equal(jewelleryPayment.Id, jewOnly.Items[0].PaymentId);

        // 4. Detail endpoint for Chitu
        var chituDetail = await service.GetPaymentDetailAsync(customer.Id, chituPayment.Id);
        Assert.NotNull(chituDetail.ChituDetails);
        Assert.Null(chituDetail.JewelleryDetails);
        Assert.Equal(2, chituDetail.ChituDetails.SlotAllocations.Count);
        Assert.Contains(chituDetail.ChituDetails.SlotAllocations, s => s.SlotId == slot1.Id && s.AllocatedAmount == 5000m);
        Assert.Contains(chituDetail.ChituDetails.SlotAllocations, s => s.SlotId == slot2.Id && s.AllocatedAmount == 5000m);

        // 5. Detail endpoint for Jewellery
        var jewDetail = await service.GetPaymentDetailAsync(customer.Id, jewelleryPayment.Id);
        Assert.Null(jewDetail.ChituDetails);
        Assert.NotNull(jewDetail.JewelleryDetails);
        Assert.Equal(0.852m, jewDetail.JewelleryDetails.CreditedGrams);
        Assert.Equal(28000m, jewDetail.JewelleryDetails.AppliedGoldRatePerGram);
        Assert.Equal(5.0m, jewDetail.JewelleryDetails.AppliedPremiumPercentage);
    }

    [Fact]
    public async Task PaymentDetail_AnotherCustomerAccess_ThrowsUnauthorizedAccessException()
    {
        var db = CreateInMemoryDbContext(nameof(PaymentDetail_AnotherCustomerAccess_ThrowsUnauthorizedAccessException));
        var service = new CustomerPaymentService(db, NullLogger<CustomerPaymentService>.Instance);

        var customerA = new Customer { Id = Guid.NewGuid(), FullName = "Customer A", IsActive = true };
        var customerB = new Customer { Id = Guid.NewGuid(), FullName = "Customer B", IsActive = true };
        db.Customers.AddRange(customerA, customerB);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerA.Id,
            Amount = 5000m,
            Module = BusinessModule.Chitu,
            Status = PaymentStatus.Succeeded
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // Customer B attempts to view Customer A's receipt
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetPaymentDetailAsync(customerB.Id, payment.Id));
    }
}
