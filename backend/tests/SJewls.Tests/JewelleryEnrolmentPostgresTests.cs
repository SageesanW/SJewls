using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class JewelleryEnrolmentPostgresTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private SJewlsDbContext _db = null!;
    private readonly List<Guid> _cleanupEnrolmentIds = new();
    private readonly List<Guid> _cleanupNotificationIds = new();
    private Guid _testBranchId;
    private Guid _testCustomerId;
    private Guid _testPlanId;
    private Guid _testCategoryId;

    public JewelleryEnrolmentPostgresTests()
    {
        // Resolve connection string from environment or local .env file
        var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(envConn))
        {
            var envCandidatePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".env"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "backend", ".env")
            };

            foreach (var path in envCandidatePaths)
            {
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("ConnectionStrings__DefaultConnection="))
                        {
                            envConn = trimmed["ConnectionStrings__DefaultConnection=".Length..].Trim().Trim('"').Trim('\'');
                            break;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(envConn)) break;
                }
            }
        }

        _connectionString = !string.IsNullOrWhiteSpace(envConn)
            ? envConn
            : "Host=aws-0-ap-northeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.thxeqfxmtcntqovfkiax;Password=SJwels@2026;SslMode=Require;Trust Server Certificate=true";
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseNpgsql(_connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .Options;

        _db = new SJewlsDbContext(options);

        // Verify execution strategy is indeed NpgsqlRetryingExecutionStrategy
        var strategy = _db.Database.CreateExecutionStrategy();
        Assert.NotNull(strategy);
        Assert.Equal("NpgsqlRetryingExecutionStrategy", strategy.GetType().Name);

        // Seed isolated test branch, customer, category, and plan in Postgres
        _testBranchId = Guid.NewGuid();
        var branch = new Branch
        {
            Id = _testBranchId,
            Code = $"TST-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            Name = "PostgreSQL Test Branch",
            Timezone = "Asia/Colombo",
            Currency = "LKR",
            Address = "Test Address",
            City = "Test City",
            IsActive = true
        };
        _db.Branches.Add(branch);

        _testCustomerId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = _testCustomerId,
            FullName = "Postgres Retry Customer",
            Email = $"pg_test_{Guid.NewGuid().ToString("N")[..8]}@sjewls.lk",
            PhoneNumber = $"+9477{Random.Shared.Next(1000000, 9999999)}",
            PrimaryBranchId = _testBranchId,
            IsActive = true,
            IsProfileComplete = true
        };
        _db.Customers.Add(customer);

        _testCategoryId = Guid.NewGuid();
        var category = new JewelleryPlanCategory
        {
            Id = _testCategoryId,
            BranchId = _testBranchId,
            Name = $"Test Cat {Guid.NewGuid().ToString("N")[..6]}",
            Description = "Test Category",
            IsActive = true
        };
        _db.JewelleryCategories.Add(category);

        _testPlanId = Guid.NewGuid();
        var plan = new JewelleryPlan
        {
            Id = _testPlanId,
            Name = "PostgreSQL Test 22K Sovereign Plan",
            CategoryId = _testCategoryId,
            BranchId = _testBranchId,
            TargetProductGoldWeightGrams = 16.0m,
            AllowedDurationsMonths = new[] { 6, 12 },
            PremiumPercentage = 4.0m,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            IsActive = true
        };
        _db.JewelleryPlans.Add(plan);

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_cleanupEnrolmentIds.Count > 0)
            {
                var enrolments = await _db.JewelleryEnrolments
                    .Where(e => _cleanupEnrolmentIds.Contains(e.Id))
                    .ToListAsync();
                _db.JewelleryEnrolments.RemoveRange(enrolments);
            }

            var notifications = await _db.Notifications
                .Where(n => n.CustomerId == _testCustomerId)
                .ToListAsync();
            _db.Notifications.RemoveRange(notifications);

            var plan = await _db.JewelleryPlans.FindAsync(_testPlanId);
            if (plan != null) _db.JewelleryPlans.Remove(plan);

            var cat = await _db.JewelleryCategories.FindAsync(_testCategoryId);
            if (cat != null) _db.JewelleryCategories.Remove(cat);

            var customer = await _db.Customers.FindAsync(_testCustomerId);
            if (customer != null) _db.Customers.Remove(customer);

            var branch = await _db.Branches.FindAsync(_testBranchId);
            if (branch != null) _db.Branches.Remove(branch);

            await _db.SaveChangesAsync();
        }
        catch
        {
            // Best effort cleanup in test tear-down
        }
        finally
        {
            await _db.DisposeAsync();
        }
    }

    private class MockGoldRateService : IGoldRateService
    {
        public Task<ApplicableGoldRateDto> GetApplicable24kRateAsync(Guid branchId, CancellationToken ct = default)
        {
            return Task.FromResult(new ApplicableGoldRateDto
            {
                RatePerGram = 28000m,
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                SourceTimestampUtc = DateTimeOffset.UtcNow,
                Source = "Mock",
                Currency = "LKR",
                IsFresh = true
            });
        }

        public Task<CustomerGoldRateResponse> GetCustomerRateAsync(Guid? branchId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<GoldRateOverviewDto> GetOverviewAsync(Guid? branchId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<GoldRateOverviewDto> SetOverrideAsync(SetGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<GoldRateOverviewDto> ToggleOverrideAsync(ToggleGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<List<GoldRateHistoryDto>> GetHistoryAsync(Guid? branchId, int limit = 50, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task CreateEnrolmentAsync_UnderNpgsqlRetryingExecutionStrategy_SucceedsWithoutUserInitiatedTransactionError()
    {
        // Arrange
        var notificationService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var mockGoldRate = new MockGoldRateService();
        var config = new ConfigurationBuilder().Build();
        var service = new CustomerJewelleryPlanService(_db, mockGoldRate, notificationService, config, NullLogger<CustomerJewelleryPlanService>.Instance);

        var idempotencyKey = $"idem-pg-{Guid.NewGuid():N}";
        var request = new CreateCustomerEnrolmentRequest
        {
            PlanId = _testPlanId,
            SelectedDurationMonths = 12,
            IdempotencyKey = idempotencyKey
        };

        // Act: Must execute without throwing "NpgsqlRetryingExecutionStrategy does not support user-initiated transactions"
        var enrolment = await service.CreateEnrolmentAsync(_testCustomerId, request);
        _cleanupEnrolmentIds.Add(enrolment.EnrolmentId);

        // Assert
        Assert.NotNull(enrolment);
        Assert.NotEqual(Guid.Empty, enrolment.EnrolmentId);
        Assert.Equal(16.0m, enrolment.TargetGrams);
        Assert.Equal(12, enrolment.SelectedDurationMonths);
        Assert.Equal("Active", enrolment.EnrolmentStatus);

        // Verify exactly 1 enrolment in database
        var dbEnrolment = await _db.JewelleryEnrolments
            .FirstOrDefaultAsync(e => e.Id == enrolment.EnrolmentId);
        Assert.NotNull(dbEnrolment);
        Assert.Equal(idempotencyKey, dbEnrolment.IdempotencyKey);

        // Verify exactly 1 activation notification
        var notifications = await _db.Notifications
            .Where(n => n.CustomerId == _testCustomerId)
            .ToListAsync();
        Assert.Single(notifications);
        Assert.Equal("Jewellery Plan Activated", notifications[0].Title);
    }

    [Fact]
    public async Task ReplayingSameIdempotencyKey_UnderNpgsql_DeduplicatesAndDoesNotCreateSecondEnrolmentOrNotification()
    {
        // Arrange
        var notificationService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var mockGoldRate = new MockGoldRateService();
        var config = new ConfigurationBuilder().Build();
        var service = new CustomerJewelleryPlanService(_db, mockGoldRate, notificationService, config, NullLogger<CustomerJewelleryPlanService>.Instance);

        var idempotencyKey = $"idem-pg-replay-{Guid.NewGuid():N}";
        var request = new CreateCustomerEnrolmentRequest
        {
            PlanId = _testPlanId,
            SelectedDurationMonths = 12,
            IdempotencyKey = idempotencyKey
        };

        // Act 1: Initial enrolment
        var enrolment1 = await service.CreateEnrolmentAsync(_testCustomerId, request);
        _cleanupEnrolmentIds.Add(enrolment1.EnrolmentId);

        // Act 2: Replay same request with same idempotency key
        var enrolment2 = await service.CreateEnrolmentAsync(_testCustomerId, request);

        // Assert: Same enrolment returned
        Assert.Equal(enrolment1.EnrolmentId, enrolment2.EnrolmentId);

        // Verify ONLY one enrolment in Postgres for this idempotency key
        var count = await _db.JewelleryEnrolments
            .CountAsync(e => e.CustomerId == _testCustomerId && e.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, count);

        // Verify ONLY one notification was created (notification was not duplicated on replay)
        var notifications = await _db.Notifications
            .Where(n => n.CustomerId == _testCustomerId)
            .ToListAsync();
        Assert.Single(notifications);
    }

    [Fact]
    public async Task ReusingIdempotencyKeyWithDifferentPayload_ThrowsJewelleryPlanConflictException()
    {
        // Arrange
        var notificationService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var mockGoldRate = new MockGoldRateService();
        var config = new ConfigurationBuilder().Build();
        var service = new CustomerJewelleryPlanService(_db, mockGoldRate, notificationService, config, NullLogger<CustomerJewelleryPlanService>.Instance);

        var idempotencyKey = $"idem-pg-conflict-{Guid.NewGuid():N}";
        var initialRequest = new CreateCustomerEnrolmentRequest
        {
            PlanId = _testPlanId,
            SelectedDurationMonths = 12,
            IdempotencyKey = idempotencyKey
        };

        var enrolment = await service.CreateEnrolmentAsync(_testCustomerId, initialRequest);
        _cleanupEnrolmentIds.Add(enrolment.EnrolmentId);

        // Conflicting request: same key, but SelectedDurationMonths changed to 6
        var conflictingRequest = new CreateCustomerEnrolmentRequest
        {
            PlanId = _testPlanId,
            SelectedDurationMonths = 6,
            IdempotencyKey = idempotencyKey
        };

        // Act & Assert: Genuine business conflict throws InvalidOperationException (which maps to 409)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateEnrolmentAsync(_testCustomerId, conflictingRequest));

        Assert.Contains("idempotency key has already been used with a different enrolment request payload", ex.Message);
    }

    [Fact]
    public async Task RepeatEnrolmentWithoutConfirmation_ThrowsRepeatEnrolmentConfirmationRequiredException()
    {
        // Arrange
        var notificationService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var mockGoldRate = new MockGoldRateService();
        var config = new ConfigurationBuilder().Build();
        var service = new CustomerJewelleryPlanService(_db, mockGoldRate, notificationService, config, NullLogger<CustomerJewelleryPlanService>.Instance);

        // 1. Initial enrolment
        var enrolment1 = await service.CreateEnrolmentAsync(_testCustomerId, new CreateCustomerEnrolmentRequest
        {
            PlanId = _testPlanId,
            SelectedDurationMonths = 12,
            IdempotencyKey = $"idem-pg-first-{Guid.NewGuid():N}"
        });
        _cleanupEnrolmentIds.Add(enrolment1.EnrolmentId);

        // 2. Attempt second enrolment for same plan without ConfirmRepeatEnrolment = true
        var secondRequest = new CreateCustomerEnrolmentRequest
        {
            PlanId = _testPlanId,
            SelectedDurationMonths = 6,
            IdempotencyKey = $"idem-pg-second-{Guid.NewGuid():N}",
            ConfirmRepeatEnrolment = false
        };

        // Act & Assert: Genuine business conflict throws RepeatEnrolmentConfirmationRequiredException (maps to 409)
        var ex = await Assert.ThrowsAsync<RepeatEnrolmentConfirmationRequiredException>(
            () => service.CreateEnrolmentAsync(_testCustomerId, secondRequest));

        Assert.Equal("RepeatEnrolmentConfirmationRequired", ex.Code);
        Assert.True(ex.TotalEnrolmentCount >= 1);
        Assert.True(ex.ActiveEnrolmentCount >= 1);
    }
}
