using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class CustomerJewelleryPlanTests
{
    private SJewlsDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SJewlsDbContext(options);
    }

    #region Authoritative Payment Calculation Engine Tests

    [Fact]
    public void BuyGram_100g_5PercentPremium_CalculatesCorrectPayableAndCreditsExactly100g()
    {
        // Arrange: 100g, 5% premium, rate = 25,000 LKR/g
        var requestedGrams = 100m;
        var premiumPct = 5.0m;
        var rate = 25000.00m;
        var remainingTarget = 100m;
        var asOf = DateTimeOffset.UtcNow;

        // Act
        var result = JewelleryPaymentCalculator.CalculateBuyGram(requestedGrams, rate, premiumPct, remainingTarget, asOf);

        // Assert
        Assert.Equal(JewelleryQuoteInputMode.BuyGram, result.InputMode);
        Assert.Equal(100m, result.BaseGoldGrams);
        Assert.Equal(5m, result.PremiumGrams);
        Assert.Equal(105m, result.ChargeableGoldGrams);
        Assert.Equal(100m, result.TotalGoldCredit); // STRICT RULE: Customer is credited ONLY 100g, NEVER 105g

        // Monetary amounts
        Assert.Equal(2500000.00m, result.BaseAmount);
        Assert.Equal(125000.00m, result.PremiumAmount);
        Assert.Equal(2625000.00m, result.AmountToPay); // Charge equivalent of 105g

        Assert.Equal(0m, result.RemainingGramsAfterPayment);
        Assert.False(result.WillExceedTarget);
    }

    [Fact]
    public void BuyGram_HalfGram_5PercentPremium_MatchesSpecificationExample()
    {
        // Specification Example: Requested grams = 0.5 g, Premium = 5%
        // Premium grams = 0.025 g, Chargeable equivalent = 0.525 g, Total Gold Credit = 0.5 g
        var requestedGrams = 0.5m;
        var premiumPct = 5.0m;
        var rate = 20000.00m;
        var remainingTarget = 5.0m;
        var asOf = DateTimeOffset.UtcNow;

        var result = JewelleryPaymentCalculator.CalculateBuyGram(requestedGrams, rate, premiumPct, remainingTarget, asOf);

        Assert.Equal(0.5m, result.BaseGoldGrams);
        Assert.Equal(0.025m, result.PremiumGrams);
        Assert.Equal(0.525m, result.ChargeableGoldGrams);
        Assert.Equal(0.5m, result.TotalGoldCredit);

        Assert.Equal(10000.00m, result.BaseAmount);
        Assert.Equal(500.00m, result.PremiumAmount);
        Assert.Equal(10500.00m, result.AmountToPay);
    }

    [Fact]
    public void BuyAmount_5PercentPremium_DeductsFromGramsAndPayableEqualsEnteredAmount()
    {
        // Specification Example:
        // BaseGoldGrams = 1.9692 g, Premium = 5%, PremiumGrams = 0.09846 g, TotalGoldCredit = 1.87074 g -> truncated/rounded down to 1.8707g
        var enteredAmount = 49230.00m;
        var rate = 25000.00m; // 49230 / 25000 = 1.9692
        var premiumPct = 5.0m;
        var remainingTarget = 10m;
        var asOf = DateTimeOffset.UtcNow;

        var result = JewelleryPaymentCalculator.CalculateBuyAmount(enteredAmount, rate, premiumPct, remainingTarget, asOf);

        Assert.Equal(JewelleryQuoteInputMode.BuyAmount, result.InputMode);
        Assert.Equal(enteredAmount, result.AmountToPay); // Customer pays ONLY entered amount; no extra premium surcharge
        Assert.Equal(1.9692m, result.BaseGoldGrams);
        Assert.Equal(0.0985m, result.PremiumGrams); // 1.9692 * 0.05 = 0.09846
        Assert.Equal(1.8707m, result.TotalGoldCredit); // 1.9692 - 0.09846 = 1.87074, rounded down to 4 decimals
        Assert.Null(result.ChargeableGoldGrams);
        Assert.Null(result.BaseAmount);
        Assert.Null(result.PremiumAmount);
    }

    [Fact]
    public void TargetCheck_ExceedingRemainingGrams_FlagsWillExceedTarget()
    {
        var requestedGrams = 10.5m;
        var remaining = 10.0m;
        var rate = 25000m;

        var result = JewelleryPaymentCalculator.CalculateBuyGram(requestedGrams, rate, 5.0m, remaining, DateTimeOffset.UtcNow);

        Assert.True(result.WillExceedTarget);
        Assert.Equal(0m, result.RemainingGramsAfterPayment);
    }

    [Fact]
    public void CalculateMaxAmountForRemainingGrams_ReturnsExactAmountNeeded()
    {
        var remainingGrams = 2.0m;
        var rate = 25000.00m;
        var premium = 5.0m;

        var maxAmount = JewelleryPaymentCalculator.CalculateMaxAmountForRemainingGrams(remainingGrams, rate, premium);

        // Verification: (2.0 * 25000) / (1 - 0.05) = 50000 / 0.95 = 52631.5789 -> 52631.58
        Assert.Equal(52631.58m, maxAmount);

        // Check that paying this amount yields approximately 2.0g
        var calc = JewelleryPaymentCalculator.CalculateBuyAmount(maxAmount, rate, premium, remainingGrams, DateTimeOffset.UtcNow);
        Assert.True(calc.TotalGoldCredit <= remainingGrams);
    }

    #endregion

    #region Gold Rate Provider & Fallback Tests

    [Fact]
    public async Task GoldRateService_WhenNoLiveKey_FallsBackToAuditedDbRate()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branchId = Guid.NewGuid();

        // Seed 24K gold rate
        db.GoldRates.Add(new GoldRate
        {
            BranchId = branchId,
            Karat = 24,
            RatePerGram = 26500.50m,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddHours(-1),
            RecordedByStaffId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "GoldApi:ApiKey", "" }, // unconfigured
            { "GoldApi:MaxStalenessMinutes", "1440" }
        }).Build();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var service = new GoldRateService(httpClient, config, cache, db, NullLogger<GoldRateService>.Instance);

        var rate = await service.GetApplicable24kRateAsync(branchId);

        Assert.NotNull(rate);
        Assert.Equal(26500.50m, rate.RatePerGram);
        Assert.Equal("LKR", rate.Currency);
        Assert.Equal("BranchManual", rate.Source);
        Assert.True(rate.IsFresh);
    }

    [Fact]
    public async Task GoldRateService_WhenDbRateStale_ThrowsGoldRateUnavailableException()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branchId = Guid.NewGuid();

        // Seed stale 24K rate (3 days old)
        db.GoldRates.Add(new GoldRate
        {
            BranchId = branchId,
            Karat = 24,
            RatePerGram = 26000m,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddDays(-3),
            RecordedByStaffId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "GoldApi:ApiKey", "" },
            { "GoldApi:MaxStalenessMinutes", "1440" } // max 1 day
        }).Build();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var service = new GoldRateService(httpClient, config, cache, db, NullLogger<GoldRateService>.Instance);

        await Assert.ThrowsAsync<GoldRateUnavailableException>(() => service.GetApplicable24kRateAsync(branchId));
    }

    #endregion

    #region Customer Jewellery Plan Service Integration Tests

    [Fact]
    public async Task CreateEnrolment_SnapshotsTerms_AndIsIdempotent()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Jaffna Central", Code = "JAF-01", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Kavitha Raj", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var category = new JewelleryPlanCategory { BranchId = branch.Id, Name = "Gold Coins", NormalizedName = "GOLD COINS", IsActive = true };
        var plan = new JewelleryPlan
        {
            BranchId = branch.Id,
            CategoryId = category.Id,
            Category = category,
            Name = "Sovereign Saver 2026",
            TargetProductGoldWeightGrams = 8.0m,
            AllowedDurationsMonths = new[] { 6, 12, 24 },
            PremiumPercentage = 4.5m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            IsActive = true
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryCategories.Add(category);
        db.JewelleryPlans.Add(plan);
        await db.SaveChangesAsync();

        var notificationService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var mockGoldRateService = new MockGoldRateService(25000m);
        var config = new ConfigurationBuilder().Build();
        var service = new CustomerJewelleryPlanService(db, mockGoldRateService, notificationService, config, NullLogger<CustomerJewelleryPlanService>.Instance);

        var request = new CreateCustomerEnrolmentRequest
        {
            PlanId = plan.Id,
            SelectedDurationMonths = 12,
            IdempotencyKey = "test-key-abc-123"
        };

        // 1. Initial creation
        var enrolment1 = await service.CreateEnrolmentAsync(customer.Id, request);

        Assert.NotNull(enrolment1);
        Assert.Equal(8.0m, enrolment1.TargetGrams);
        Assert.Equal(4.5m, enrolment1.PremiumPercentage);
        Assert.Equal(12, enrolment1.SelectedDurationMonths);
        Assert.Equal("Active", enrolment1.EnrolmentStatus);
        Assert.Equal(0m, enrolment1.TotalConfirmedMoneyPaid);
        Assert.Equal(0m, enrolment1.NetAccumulatedGoldGrams);
        Assert.True(enrolment1.IsContributionAllowed);

        // Verify activation notification was recorded
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer.Id);
        Assert.NotNull(notif);
        Assert.Equal("Jewellery Plan Activated", notif.Title);

        // 2. Retry identical request with same IdempotencyKey
        var enrolment2 = await service.CreateEnrolmentAsync(customer.Id, request);
        Assert.Equal(enrolment1.EnrolmentId, enrolment2.EnrolmentId);

        // Confirm only one enrolment entity exists
        var enrolmentCount = await db.JewelleryEnrolments.CountAsync(e => e.CustomerId == customer.Id);
        Assert.Equal(1, enrolmentCount);
    }

    [Fact]
    public async Task CreateLockedQuote_CreatesQuoteWith5MinuteExpiry()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Jaffna Central", Code = "JAF-01", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Kavitha Raj", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var plan = new JewelleryPlan
        {
            BranchId = branch.Id,
            Name = "Bridal Gold",
            TargetProductGoldWeightGrams = 20.0m,
            AllowedDurationsMonths = new[] { 12 },
            PremiumPercentage = 5.0m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            IsActive = true
        };
        var enrolment = new JewelleryEnrolment
        {
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            BranchId = branch.Id,
            TargetProductWeightGrams = 20.0m,
            SelectedDurationMonths = 12,
            PremiumPercentage = 5.0m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)),
            TotalSavedGrams = 0m,
            Status = JewelleryEnrolmentStatus.Active
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryPlans.Add(plan);
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var notificationService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var mockGoldRateService = new MockGoldRateService(25000m);
        var config = new ConfigurationBuilder().Build();
        var service = new CustomerJewelleryPlanService(db, mockGoldRateService, notificationService, config, NullLogger<CustomerJewelleryPlanService>.Instance);

        var quoteRequest = new JewelleryCalculationRequest
        {
            InputMode = "BuyGram",
            RequestedGrams = 2.0m
        };

        var quote = await service.CreateLockedQuoteAsync(customer.Id, enrolment.Id, quoteRequest);

        Assert.NotNull(quote);
        Assert.Equal(2.0m, quote.RequestedGrams);
        Assert.Equal(2.0m, quote.TotalGoldCredit); // Credited grams
        Assert.Equal(2.1m, quote.ChargeableGoldGrams); // Chargeable breakdown: 2 + 0.1g
        Assert.Equal(52500.00m, quote.AmountToPay); // (2 * 25000) + (0.1 * 25000)
        Assert.Equal("Active", quote.Status);
        Assert.False(quote.IsExpired);
        Assert.True(quote.ExpiresAtUtc > DateTimeOffset.UtcNow);

        // Payment initiation test: deferred gateway
        var payResponse = await service.InitiatePaymentAsync(customer.Id, quote.QuoteId);
        Assert.Equal("PaymentProviderNotConfigured", payResponse.Code);
        Assert.False(payResponse.IsConfigured);
    }

    [Fact]
    public async Task PaymentFinalizationService_AtomicallyCreditsTotalGoldCredit_AndMarksCompleted()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Jaffna Central", Code = "JAF-01", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Ravi Kumar", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var enrolment = new JewelleryEnrolment
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            PlanNameSnapshot = "Daily Saver",
            TargetProductWeightGrams = 2.0m,
            SelectedDurationMonths = 6,
            PremiumPercentage = 5.0m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            TotalSavedGrams = 0m,
            TotalContributedCurrency = 0m,
            Status = JewelleryEnrolmentStatus.Active
        };

        var quote = new JewelleryPaymentQuote
        {
            CustomerId = customer.Id,
            JewelleryEnrolmentId = enrolment.Id,
            JewelleryEnrolment = enrolment,
            InputMode = JewelleryQuoteInputMode.BuyGram,
            EnteredValue = 2.0m,
            Currency = "LKR",
            RatePerGram = 25000m,
            PremiumPercentage = 5.0m,
            BaseGoldGrams = 2.0m,
            PremiumGrams = 0.1m,
            ChargeableGoldGrams = 2.1m,
            TotalGoldCredit = 2.0m,
            AmountToPay = 52500.00m,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            Status = JewelleryQuoteStatus.Active
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryEnrolments.Add(enrolment);
        db.JewelleryPaymentQuotes.Add(quote);
        await db.SaveChangesAsync();

        var notificationService = new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance);
        var finalizationService = new JewelleryPaymentFinalizationService(db, notificationService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        var payload = new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = "TXN-VERIFIED-998877",
            AmountPaid = 52500.00m,
            Currency = "LKR",
            ProviderName = "VerifiedMockGateway"
        };

        // Act
        var result = await finalizationService.FinalizePaymentAsync(payload);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.PlanCompleted);
        Assert.Equal(2.0m, result.GoldGramsCredited);
        Assert.Equal(2.0m, result.NewTotalSavedGrams);

        // Refresh database state
        var updatedEnrolment = await db.JewelleryEnrolments.FindAsync(enrolment.Id);
        Assert.Equal(2.0m, updatedEnrolment!.TotalSavedGrams);
        Assert.Equal(52500.00m, updatedEnrolment.TotalContributedCurrency);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, updatedEnrolment.Status);

        var updatedQuote = await db.JewelleryPaymentQuotes.FindAsync(quote.Id);
        Assert.Equal(JewelleryQuoteStatus.Used, updatedQuote!.Status);

        // Verify completion notification was generated
        var compNotif = await db.Notifications.FirstOrDefaultAsync(n => n.CustomerId == customer.Id && n.Type == "completion");
        Assert.NotNull(compNotif);
        Assert.Equal("Jewellery Plan Completed!", compNotif.Title);

        // Test Idempotency for repeated callback
        var retryResult = await finalizationService.FinalizePaymentAsync(payload);
        Assert.True(retryResult.Success);
        Assert.Contains("already been processed", retryResult.Message);
    }

    [Fact]
    public async Task GetCategoriesAndPlans_FiltersByBranchAndActiveStatusAndFutureStartDate()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch1 = new Branch { Name = "Branch 1", Code = "B1", Currency = "LKR", Timezone = "Asia/Colombo" };
        var branch2 = new Branch { Name = "Branch 2", Code = "B2", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Anusha", PrimaryBranchId = branch1.Id, PrimaryBranch = branch1, IsActive = true };

        // Categories: 1 active for branch1, 1 inactive for branch1, 1 active for branch2
        var cat1Active = new JewelleryPlanCategory { BranchId = branch1.Id, Name = "Necklaces", NormalizedName = "NECKLACES", IsActive = true };
        var cat1Inactive = new JewelleryPlanCategory { BranchId = branch1.Id, Name = "Old Rings", NormalizedName = "OLD RINGS", IsActive = false };
        var cat2Active = new JewelleryPlanCategory { BranchId = branch2.Id, Name = "Bangles", NormalizedName = "BANGLES", IsActive = true };

        // Plans for cat1Active:
        // 1. Available (active, started 2 days ago)
        var planAvailable = new JewelleryPlan
        {
            BranchId = branch1.Id,
            CategoryId = cat1Active.Id,
            Category = cat1Active,
            Name = "Available Plan",
            TargetProductGoldWeightGrams = 10m,
            AllowedDurationsMonths = new[] { 6, 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            IsActive = true
        };
        // 2. Future-dated (active, starts in 5 days) - MUST be excluded from customer browsing
        var planFuture = new JewelleryPlan
        {
            BranchId = branch1.Id,
            CategoryId = cat1Active.Id,
            Category = cat1Active,
            Name = "Future Plan",
            TargetProductGoldWeightGrams = 15m,
            AllowedDurationsMonths = new[] { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            IsActive = true
        };
        // 3. Deactivated (starts in past, but isActive = false) - MUST be excluded from customer browsing
        var planInactive = new JewelleryPlan
        {
            BranchId = branch1.Id,
            CategoryId = cat1Active.Id,
            Category = cat1Active,
            Name = "Deactivated Plan",
            TargetProductGoldWeightGrams = 20m,
            AllowedDurationsMonths = new[] { 24 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            IsActive = false
        };

        db.Branches.AddRange(branch1, branch2);
        db.Customers.Add(customer);
        db.JewelleryCategories.AddRange(cat1Active, cat1Inactive, cat2Active);
        db.JewelleryPlans.AddRange(planAvailable, planFuture, planInactive);
        await db.SaveChangesAsync();

        var service = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(25000m),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        // 1. Categories browsing for customer: only cat1Active should appear
        var categories = await service.GetCategoriesAsync(customer.Id);
        Assert.Single(categories);
        Assert.Equal(cat1Active.Id, categories[0].Id);
        Assert.Equal("Necklaces", categories[0].Name);

        // 2. Plans browsing for cat1Active: only planAvailable should appear
        var plans = await service.GetPlansByCategoryAsync(customer.Id, cat1Active.Id);
        Assert.Single(plans);
        Assert.Equal(planAvailable.Id, plans[0].Id);
        Assert.Equal("Available Plan", plans[0].Name);
        Assert.Equal(new[] { 6, 12 }, plans[0].OfferedDurations); // Exact offered durations
    }

    [Fact]
    public async Task CreateEnrolment_WhenCustomerInactive_ThrowsInvalidOperationException()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Branch 1", Code = "B1", Currency = "LKR", Timezone = "Asia/Colombo" };
        var inactiveCustomer = new Customer { FullName = "Suspended User", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = false };
        var plan = new JewelleryPlan
        {
            BranchId = branch.Id,
            Name = "Test Plan",
            TargetProductGoldWeightGrams = 10m,
            AllowedDurationsMonths = new[] { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            IsActive = true
        };

        db.Branches.Add(branch);
        db.Customers.Add(inactiveCustomer);
        db.JewelleryPlans.Add(plan);
        await db.SaveChangesAsync();

        var service = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(25000m),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        var request = new CreateCustomerEnrolmentRequest
        {
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateEnrolmentAsync(inactiveCustomer.Id, request));
        Assert.Contains("deactivated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateEnrolment_WhenDurationNotOffered_ThrowsArgumentException()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Branch 1", Code = "B1", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Kavitha", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var plan = new JewelleryPlan
        {
            BranchId = branch.Id,
            Name = "Plan 6 or 12",
            TargetProductGoldWeightGrams = 10m,
            AllowedDurationsMonths = new[] { 6, 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            IsActive = true
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryPlans.Add(plan);
        await db.SaveChangesAsync();

        var service = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(25000m),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        var request = new CreateCustomerEnrolmentRequest
        {
            PlanId = plan.Id,
            SelectedDurationMonths = 24 // Not offered!
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateEnrolmentAsync(customer.Id, request));
        Assert.Contains("24 months is not offered", ex.Message);
    }

    [Fact]
    public async Task DeactivatedPlan_BlocksNewEnrolment_ButExistingCustomerMayStillContribute()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Branch 1", Code = "B1", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Kumar", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var plan = new JewelleryPlan
        {
            BranchId = branch.Id,
            Name = "Discontinued Plan",
            TargetProductGoldWeightGrams = 10m,
            AllowedDurationsMonths = new[] { 12 },
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            IsActive = false // Deactivated by Admin!
        };

        var existingEnrolment = new JewelleryEnrolment
        {
            CustomerId = customer.Id,
            JewelleryPlanId = plan.Id,
            JewelleryPlan = plan,
            BranchId = branch.Id,
            TargetProductWeightGrams = 10m,
            SelectedDurationMonths = 12,
            PremiumPercentage = 5m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(11)),
            TotalSavedGrams = 2m,
            Status = JewelleryEnrolmentStatus.Active
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryPlans.Add(plan);
        db.JewelleryEnrolments.Add(existingEnrolment);
        await db.SaveChangesAsync();

        var service = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(25000m),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        // 1. New enrolment into deactivated plan must FAIL
        var newEnrolmentReq = new CreateCustomerEnrolmentRequest
        {
            PlanId = plan.Id,
            SelectedDurationMonths = 12
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateEnrolmentAsync(customer.Id, newEnrolmentReq));

        // 2. Existing enrolment details must still show IsContributionAllowed == true
        var enrolmentDetail = await service.GetEnrolmentDetailsAsync(customer.Id, existingEnrolment.Id);
        Assert.NotNull(enrolmentDetail);
        Assert.True(enrolmentDetail.IsContributionAllowed);
        Assert.Null(enrolmentDetail.ContributionBlockedReason);

        // 3. Calculation preview on existing enrolment must succeed
        var preview = await service.PreviewCalculationAsync(customer.Id, existingEnrolment.Id, new JewelleryCalculationRequest
        {
            InputMode = "BuyGram",
            RequestedGrams = 1.0m
        });
        Assert.Equal(1.0m, preview.TotalGoldCredit);
        Assert.False(preview.WillExceedTarget);
    }

    [Fact]
    public async Task CalculationPreview_WhenExceedingTarget_ThrowsArgumentExceptionWithRemaining()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Branch 1", Code = "B1", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customer = new Customer { FullName = "Kumar", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var enrolment = new JewelleryEnrolment
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            TargetProductWeightGrams = 5m,
            SelectedDurationMonths = 12,
            PremiumPercentage = 5m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)),
            TotalSavedGrams = 4.5m, // Only 0.5g remaining
            Status = JewelleryEnrolmentStatus.Active
        };

        db.Branches.Add(branch);
        db.Customers.Add(customer);
        db.JewelleryEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var service = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(25000m),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        var request = new JewelleryCalculationRequest
        {
            InputMode = "BuyGram",
            RequestedGrams = 1.0m // Exceeds 0.5g remaining!
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewCalculationAsync(customer.Id, enrolment.Id, request));
        Assert.Contains("exceeds the remaining target", ex.Message);
        Assert.Contains("0.5", ex.Message);
    }

    [Fact]
    public async Task ResourceOwnership_PreventsAccessToOtherCustomerResources()
    {
        var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var branch = new Branch { Name = "Branch 1", Code = "B1", Currency = "LKR", Timezone = "Asia/Colombo" };
        var customerA = new Customer { FullName = "Customer A", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };
        var customerB = new Customer { FullName = "Customer B", PrimaryBranchId = branch.Id, PrimaryBranch = branch, IsActive = true };

        var enrolmentA = new JewelleryEnrolment
        {
            CustomerId = customerA.Id,
            BranchId = branch.Id,
            TargetProductWeightGrams = 10m,
            SelectedDurationMonths = 12,
            PremiumPercentage = 5m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TargetEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)),
            Status = JewelleryEnrolmentStatus.Active
        };

        db.Branches.Add(branch);
        db.Customers.AddRange(customerA, customerB);
        db.JewelleryEnrolments.Add(enrolmentA);
        await db.SaveChangesAsync();

        var service = new CustomerJewelleryPlanService(
            db,
            new MockGoldRateService(25000m),
            new CustomerNotificationService(db, NullLogger<CustomerNotificationService>.Instance),
            new ConfigurationBuilder().Build(),
            NullLogger<CustomerJewelleryPlanService>.Instance);

        // Customer B tries to get Customer A's enrolment
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEnrolmentDetailsAsync(customerB.Id, enrolmentA.Id));

        // Customer B tries to preview quote on Customer A's enrolment
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PreviewCalculationAsync(customerB.Id, enrolmentA.Id, new JewelleryCalculationRequest
        {
            InputMode = "BuyGram",
            RequestedGrams = 1m
        }));
    }

    #endregion

    private class MockGoldRateService : IGoldRateService
    {
        private readonly decimal _rate;
        public MockGoldRateService(decimal rate) => _rate = rate;

        public Task<ApplicableGoldRateDto> GetApplicable24kRateAsync(Guid branchId, CancellationToken ct = default)
        {
            return Task.FromResult(new ApplicableGoldRateDto
            {
                RatePerGram = _rate,
                Currency = "LKR",
                Source = "MockProvider",
                SourceTimestampUtc = DateTimeOffset.UtcNow,
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                IsFresh = true
            });
        }
    }
}
