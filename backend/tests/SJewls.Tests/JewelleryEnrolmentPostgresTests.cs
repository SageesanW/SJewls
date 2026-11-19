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

    private readonly Xunit.Abstractions.ITestOutputHelper? _output;

    public JewelleryEnrolmentPostgresTests(Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        _output = output;
        
        // Resolve connection string: strictly prefer dedicated test database
        var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__TestDatabase")
            ?? Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__TestConnection");

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
                        if (trimmed.StartsWith("ConnectionStrings__TestDatabase=") ||
                            trimmed.StartsWith("POSTGRES_TEST_CONNECTION_STRING="))
                        {
                            envConn = trimmed.Split('=', 2)[1].Trim().Trim('"').Trim('\'');
                            break;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(envConn)) break;
                }
            }
        }

        // Default to dedicated local PostgreSQL test database on port 5433
        _connectionString = !string.IsNullOrWhiteSpace(envConn)
            ? envConn
            : "Host=localhost;Port=5433;Database=sjewls_test;Username=sagee;Trust Server Certificate=true";

        // CRITICAL SAFETY GUARD: Enforce strict allowlist of approved test hosts and databases
        AssertApprovedTestDatabase(_connectionString);
    }

    private static void AssertApprovedTestDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Test connection string is null or empty. Tests must execute against an approved test database.");
        }

        var csBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        var host = csBuilder.Host?.ToLowerInvariant() ?? string.Empty;
        var database = csBuilder.Database?.ToLowerInvariant() ?? string.Empty;

        var allowedHosts = new HashSet<string> { "localhost", "127.0.0.1", "::1", "0.0.0.0", "postgres-test" };

        if (!allowedHosts.Contains(host))
        {
            throw new InvalidOperationException(
                $"CRITICAL SAFETY GUARD: Host '{host}' is NOT in the approved test host allowlist ({string.Join(", ", allowedHosts)}). " +
                "Integration tests must execute exclusively against an isolated local/test database.");
        }

        if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CRITICAL SAFETY GUARD: Database '{database}' is NOT an approved test database (name must contain 'test'). " +
                "Refusing to execute integration tests against non-test database.");
        }

        if (connectionString.Contains("supabase", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("thxeqfxmtcntqovfkiax", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("aws-0-ap-northeast-2.pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CRITICAL SAFETY GUARD: Execution against Supabase production projects is strictly blocked.");
        }
    }

    public async Task InitializeAsync()
    {
        AssertApprovedTestDatabase(_connectionString);
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
            Code = $"JP-PG-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
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
        AssertApprovedTestDatabase(_connectionString);
        try
        {
            if (_cleanupEnrolmentIds.Count > 0)
            {
                var contributions = await _db.JewelleryContributions
                    .Where(c => _cleanupEnrolmentIds.Contains(c.JewelleryEnrolmentId))
                    .ToListAsync();
                _db.JewelleryContributions.RemoveRange(contributions);

                var allocations = await _db.PaymentAllocations
                    .Where(a => _cleanupEnrolmentIds.Contains(a.TargetEntityId))
                    .ToListAsync();
                _db.PaymentAllocations.RemoveRange(allocations);

                var quotes = await _db.JewelleryPaymentQuotes
                    .Where(q => _cleanupEnrolmentIds.Contains(q.JewelleryEnrolmentId))
                    .ToListAsync();
                _db.JewelleryPaymentQuotes.RemoveRange(quotes);

                var enrolments = await _db.JewelleryEnrolments
                    .Where(e => _cleanupEnrolmentIds.Contains(e.Id))
                    .ToListAsync();
                _db.JewelleryEnrolments.RemoveRange(enrolments);
            }

            var payments = await _db.Payments
                .Where(p => p.CustomerId == _testCustomerId)
                .ToListAsync();
            _db.Payments.RemoveRange(payments);

            var notifications = await _db.Notifications
                .Where(n => n.CustomerId == _testCustomerId)
                .ToListAsync();
            _db.Notifications.RemoveRange(notifications);

            var alerts = await _db.PlanAlerts
                .Where(a => a.CustomerId == _testCustomerId)
                .ToListAsync();
            _db.PlanAlerts.RemoveRange(alerts);

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

    [Fact]
    public async Task Postgres_JewelleryCompletion_OnlineVerifiedPayment_CreatesNotificationAndAlert_InRealDatabase()
    {
        // Arrange
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "PG Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quoteId = Guid.NewGuid();
        var quote = new JewelleryPaymentQuote
        {
            Id = quoteId,
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        var notificationService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizationService = new JewelleryPaymentFinalizationService(_db, notificationService, NullLogger<JewelleryPaymentFinalizationService>.Instance);
        var alertService = new PlanAlertService(_db, NullLogger<PlanAlertService>.Instance);

        // Act: Finalize payment in real PostgreSQL database with NpgsqlRetryingExecutionStrategy enabled
        var result = await finalizationService.FinalizePaymentAsync(new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = $"PG-VERIFIED-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresLiveGateway"
        });

        // Assert
        Assert.True(result.Success);
        Assert.True(result.PlanCompleted);
        Assert.Equal(5.0m, result.NewTotalSavedGrams);

        // Verify committed data in PostgreSQL
        var reloadedEnrolment = await _db.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(reloadedEnrolment);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, reloadedEnrolment.Status);

        var alert = await _db.PlanAlerts
            .FirstOrDefaultAsync(a => a.JewelleryEnrolmentId == enrolmentId && a.EventType == "JewelleryCompletion");
        Assert.NotNull(alert);
        Assert.Equal("Jewellery Plan Completed!", alert.Title);
        Assert.Equal(5.0m, alert.TargetGrams);
        Assert.False(alert.IsAcknowledged);

        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.CustomerId == _testCustomerId && n.Type == "completion");
        Assert.NotNull(notif);
        Assert.Equal(alert.Id, notif.PlanAlertId);

        // Test Acknowledgement in PostgreSQL
        var ackResult = await alertService.AcknowledgeAlertAsync(_testCustomerId, alert.Id);
        Assert.True(ackResult.IsAcknowledged);
        Assert.False(ackResult.ShouldShowPopup);
        Assert.NotNull(ackResult.AcknowledgedAtUtc);

        // Test Idempotent repeat acknowledgement in PostgreSQL
        var repeatAck = await alertService.AcknowledgeAlertAsync(_testCustomerId, alert.Id);
        Assert.Equal(ackResult.AcknowledgedAtUtc, repeatAck.AcknowledgedAtUtc);
    }

    [Fact]
    public async Task Postgres_ConcurrentAndRetried_ChituWinner_CreatesSingleAlertAndNotification_WithoutDuplicateBenefit()
    {
        // 1. Arrange isolated Chitu plan and slot in PostgreSQL with NpgsqlRetryingExecutionStrategy
        var staffId = Guid.NewGuid();
        var superAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
        if (superAdminRole == null)
        {
            superAdminRole = new Role { Id = Guid.NewGuid(), Name = "SuperAdmin" };
            _db.Roles.Add(superAdminRole);
        }

        var staff = new Staff
        {
            Id = staffId,
            FullName = "Postgres Raffle Admin",
            Email = $"raffle_admin_{Guid.NewGuid():N}@sjewls.lk",
            IsActive = true
        };
        staff.Roles.Add(new StaffRole { StaffId = staff.Id, RoleId = superAdminRole.Id, Role = superAdminRole });
        staff.AssignedBranches.Add(new StaffBranch { StaffId = staff.Id, BranchId = _testBranchId });
        _db.StaffMembers.Add(staff);

        var chituPlanId = Guid.NewGuid();
        var chituPlan = new ChituPlan
        {
            Id = chituPlanId,
            BranchId = _testBranchId,
            Code = $"CP-PG-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Name = "PostgreSQL Concurrency Chitu Plan",
            Description = "10 Month Plan",
            RedemptionType = ChituRedemptionType.Gold,
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 100000m,
            DurationMonths = 10,
            TotalSlotCapacity = 10,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(8)),
            Status = PlanStatus.Active
        };
        _db.ChituPlans.Add(chituPlan);

        var slotId = Guid.NewGuid();
        var slot = new ChituSlot
        {
            Id = slotId,
            ChituPlanId = chituPlan.Id,
            CustomerId = _testCustomerId,
            SlotNumber = 1,
            SlotDisplayCode = "SLOT-001",
            Status = ChituSlotStatus.Active
        };
        slot.Instalments.Add(new ChituInstalment
        {
            Id = Guid.NewGuid(),
            ChituSlotId = slot.Id,
            MonthIndex = 1,
            DueDate = chituPlan.StartDate,
            AmountDue = 10000m,
            Status = InstalmentStatus.Paid,
            PaidAtUtc = DateTimeOffset.UtcNow
        });
        _db.ChituSlots.Add(slot);

        await _db.SaveChangesAsync();

        var auditLog = new AuditLogService(_db, NullLogger<AuditLogService>.Instance);
        var chituService = new ChituService(_db, auditLog, NullLogger<ChituService>.Instance);

        var raffleRequest = new RecordChituRaffleRequest
        {
            WinningSlotId = slot.Id,
            Remarks = "First round draw in Postgres test"
        };

        // 2. Act: Attempt 1 succeeds and records winner
        var firstDrawResult = await chituService.RecordRaffleDrawAsync(staff.Id, chituPlan.Id, 1, raffleRequest);
        Assert.NotNull(firstDrawResult);
        Assert.Equal(slot.Id, firstDrawResult.WinningSlotId);

        // Act: Attempt 2 (retry or concurrent execution for the same round and plan)
        // Must be rejected by business rules / database state with InvalidOperationException
        var secondAttemptEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            chituService.RecordRaffleDrawAsync(staff.Id, chituPlan.Id, 1, raffleRequest));

        Assert.Contains("already been recorded", secondAttemptEx.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Assert: Exactly ONE raffle draw, ONE winner benefit, ONE alert, and ONE notification in PostgreSQL
        var totalDraws = await _db.ChituDraws
            .CountAsync(d => d.ChituPlanId == chituPlan.Id && d.DrawMonthIndex == 1);
        Assert.Equal(1, totalDraws);

        var totalBenefits = await _db.WinnerBenefits
            .CountAsync(b => b.ChituSlotId == slot.Id);
        Assert.Equal(1, totalBenefits);

        var alerts = await _db.PlanAlerts
            .Where(a => a.ChituSlotId == slot.Id && a.EventType == "ChituWinner")
            .ToListAsync();
        Assert.Single(alerts);
        Assert.Equal("ChituWinner", alerts[0].EventType);
        Assert.Equal(1, alerts[0].ChituRoundIndex);
        Assert.False(alerts[0].IsAcknowledged);

        var notifications = await _db.Notifications
            .Where(n => n.CustomerId == _testCustomerId && n.Type == "chitu_winner" && n.PlanAlertId == alerts[0].Id)
            .ToListAsync();
        Assert.Single(notifications);

        // 4. Assert: Attempting to force-insert a duplicate alert with same (ChituSlotId, ChituRoundIndex)
        // is rejected at the database level by the filtered unique index IX_PlanAlerts_ChituSlotId_ChituRoundIndex
        var duplicateAlert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = _testCustomerId,
            ChituSlotId = slot.Id,
            ChituRoundIndex = 1,
            EventType = "ChituWinner",
            Module = BusinessModule.Chitu,
            Title = "Duplicate Alert Attempt",
            Message = "Should fail",
            PlanId = chituPlan.Id,
            ReferenceId = chituPlan.Id,
            BranchId = _testBranchId,
            BranchName = "Test Branch"
        };
        _db.PlanAlerts.Add(duplicateAlert);

        var dbEx = await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Contains("IX_PlanAlerts_ChituSlotId_ChituRoundIndex", dbEx.InnerException?.Message ?? dbEx.Message);

        // Detach failed entity so context remains clean
        _db.Entry(duplicateAlert).State = EntityState.Detached;

        // Cleanup seeded Chitu test records via direct SQL in reverse dependency order
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"PlanAlerts\" WHERE \"PlanId\" = {0}", chituPlanId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Notifications\" WHERE \"CustomerId\" = {0}", _testCustomerId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"WinnerBenefits\" WHERE \"ChituSlotId\" = {0}", slotId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituDraws\" WHERE \"ChituPlanId\" = {0}", chituPlanId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituInstalments\" WHERE \"ChituSlotId\" = {0}", slotId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituSlots\" WHERE \"ChituPlanId\" = {0}", chituPlanId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituPlans\" WHERE \"Id\" = {0}", chituPlanId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffRoles\" WHERE \"StaffId\" = {0}", staff.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffBranches\" WHERE \"StaffId\" = {0}", staff.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffMembers\" WHERE \"Id\" = {0}", staff.Id);
    }

    [Fact]
    public async Task Postgres_ConcurrentAndRetried_JewelleryCompletion_CreatesSingleAlertAndNotification_WithNoDuplicateFinancialCredit()
    {
        // 1. Arrange isolated Jewellery enrolment with 4.0g accumulated towards a 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "PG Retry Enrolment Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quoteId = Guid.NewGuid();
        var quote = new JewelleryPaymentQuote
        {
            Id = quoteId,
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        var notificationService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizationService = new JewelleryPaymentFinalizationService(_db, notificationService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        var providerRef = $"PG-RETRIED-PAY-{Guid.NewGuid():N}";
        var payload = new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = providerRef,
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresLiveGateway"
        };

        // 2. Act: First finalization call succeeds, brings total to 5.0g, marks Completed, creates alert + notification
        var firstResult = await finalizationService.FinalizePaymentAsync(payload);
        Assert.True(firstResult.Success);
        Assert.True(firstResult.PlanCompleted);
        Assert.Equal(5.0m, firstResult.NewTotalSavedGrams);

        // Act: Second finalization call with identical provider reference (replayed webhook / gateway retry)
        // Must return idempotent success without altering financial balance
        var retryResult = await finalizationService.FinalizePaymentAsync(payload);
        Assert.True(retryResult.Success);

        // 3. Assert Financial Integrity: EXACTLY 5.0g saved (no duplicate financial credit of 6.0g!)
        var reloadedEnrolment = await _db.JewelleryEnrolments
            .Include(e => e.Contributions)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId);

        Assert.NotNull(reloadedEnrolment);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, reloadedEnrolment.Status);
        Assert.Equal(5.0m, reloadedEnrolment.TotalSavedGrams); // Strict check: NO duplicate financial credit!
        Assert.Equal(150000m, reloadedEnrolment.TotalContributedCurrency); // 120,000 + 30,000 only

        // Assert exactly one contribution was recorded for this payment
        Assert.Single(reloadedEnrolment.Contributions);
        Assert.Equal(1.0m, reloadedEnrolment.Contributions.First().GoldGrams);

        // Assert exactly one alert was created
        var alerts = await _db.PlanAlerts
            .Where(a => a.JewelleryEnrolmentId == enrolmentId && a.EventType == "JewelleryCompletion")
            .ToListAsync();
        Assert.Single(alerts);
        Assert.Equal(5.0m, alerts[0].TargetGrams);

        // Assert exactly one notification was created
        var notifications = await _db.Notifications
            .Where(n => n.CustomerId == _testCustomerId && n.Type == "completion" && n.PlanAlertId == alerts[0].Id)
            .ToListAsync();
        Assert.Single(notifications);

        // 4. Assert: Attempting to force-insert a duplicate alert with same (JewelleryEnrolmentId, EventType)
        // is rejected at the database level by the filtered unique index IX_PlanAlerts_JewelleryEnrolmentId_EventType
        var duplicateAlert = new PlanAlert
        {
            Id = Guid.NewGuid(),
            CustomerId = _testCustomerId,
            JewelleryEnrolmentId = enrolment.Id,
            EventType = "JewelleryCompletion",
            Module = BusinessModule.Jewellery,
            Title = "Duplicate Completion Alert",
            Message = "Should fail in PostgreSQL",
            PlanId = _testPlanId,
            ReferenceId = enrolment.Id,
            BranchId = _testBranchId,
            BranchName = "Test Branch"
        };
        _db.PlanAlerts.Add(duplicateAlert);

        var dbEx = await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Contains("IX_PlanAlerts_JewelleryEnrolmentId_EventType", dbEx.InnerException?.Message ?? dbEx.Message);

        // Detach failed entity
        _db.Entry(duplicateAlert).State = EntityState.Detached;
    }

    private SJewlsDbContext CreateSeparateDbContext()
    {
        AssertApprovedTestDatabase(_connectionString);
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseNpgsql(_connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            })
            .Options;

        return new SJewlsDbContext(options);
    }

    [Fact]
    public async Task Postgres_Simultaneous_ChituWinnerConfirmation_SeparateConnections_AssertsSingleWinnerAndSafeConflict()
    {
        // 1. Arrange isolated Chitu plan and slot
        var staffId = Guid.NewGuid();
        var superAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
        if (superAdminRole == null)
        {
            superAdminRole = new Role { Id = Guid.NewGuid(), Name = "SuperAdmin" };
            _db.Roles.Add(superAdminRole);
        }

        var staff = new Staff
        {
            Id = staffId,
            FullName = "Simultaneous Staff",
            Email = $"simul_staff_{Guid.NewGuid():N}@sjewls.lk",
            IsActive = true
        };
        staff.Roles.Add(new StaffRole { StaffId = staff.Id, RoleId = superAdminRole.Id, Role = superAdminRole });
        staff.AssignedBranches.Add(new StaffBranch { StaffId = staff.Id, BranchId = _testBranchId });
        _db.StaffMembers.Add(staff);

        var planId = Guid.NewGuid();
        var plan = new ChituPlan
        {
            Id = planId,
            BranchId = _testBranchId,
            Code = $"CP-SIMUL-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Name = "Simultaneous Test Chitu Plan",
            RedemptionType = ChituRedemptionType.Gold,
            MonthlyInstalmentAmount = 10000m,
            ChituBenefitValue = 100000m,
            DurationMonths = 10,
            TotalSlotCapacity = 10,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(9)),
            Status = PlanStatus.Active
        };
        _db.ChituPlans.Add(plan);

        var slotId = Guid.NewGuid();
        var slot = new ChituSlot
        {
            Id = slotId,
            ChituPlanId = plan.Id,
            CustomerId = _testCustomerId,
            SlotNumber = 1,
            SlotDisplayCode = "SLOT-001",
            Status = ChituSlotStatus.Active
        };
        slot.Instalments.Add(new ChituInstalment
        {
            Id = Guid.NewGuid(),
            ChituSlotId = slot.Id,
            MonthIndex = 1,
            DueDate = plan.StartDate,
            AmountDue = 10000m,
            Status = InstalmentStatus.Paid,
            PaidAtUtc = DateTimeOffset.UtcNow
        });
        _db.ChituSlots.Add(slot);

        await _db.SaveChangesAsync();

        var raffleRequest = new RecordChituRaffleRequest
        {
            WinningSlotId = slot.Id,
            Remarks = "Simultaneous raffle confirmation test"
        };

        // 2. Act: Synchronized simultaneous winner confirmation across two separate physical database connections
        var barrier = new Barrier(2);
        Exception? expectedConflictException = null;
        RecordChituRaffleResponse? successfulResult = null;

        var task1 = Task.Run(async () =>
        {
            await using var db1 = CreateSeparateDbContext();
            var auditLog1 = new AuditLogService(db1, NullLogger<AuditLogService>.Instance);
            var chitu1 = new ChituService(db1, auditLog1, NullLogger<ChituService>.Instance);

            barrier.SignalAndWait();
            try
            {
                return await chitu1.RecordRaffleDrawAsync(staff.Id, plan.Id, 1, raffleRequest);
            }
            catch (Exception ex)
            {
                expectedConflictException = ex;
                return null;
            }
        });

        var task2 = Task.Run(async () =>
        {
            await using var db2 = CreateSeparateDbContext();
            var auditLog2 = new AuditLogService(db2, NullLogger<AuditLogService>.Instance);
            var chitu2 = new ChituService(db2, auditLog2, NullLogger<ChituService>.Instance);

            barrier.SignalAndWait();
            try
            {
                return await chitu2.RecordRaffleDrawAsync(staff.Id, plan.Id, 1, raffleRequest);
            }
            catch (Exception ex)
            {
                expectedConflictException = ex;
                return null;
            }
        });

        var results = await Task.WhenAll(task1, task2);
        successfulResult = results.FirstOrDefault(r => r != null);

        // 3. Assert: Exactly one request succeeded and one caught the uniqueness/conflict guard safely
        Assert.NotNull(successfulResult);
        Assert.Equal(slot.Id, successfulResult.WinningSlotId);
        Assert.NotNull(expectedConflictException);
        Assert.True(
            expectedConflictException is InvalidOperationException ||
            expectedConflictException is DbUpdateException,
            $"Expected conflict exception, got {expectedConflictException.GetType().Name}: {expectedConflictException.Message}");

        // Assert database state via fresh independent connection: Exactly 1 draw, 1 benefit, 1 alert, 1 notification
        await using var verifyDb = CreateSeparateDbContext();
        var draws = await verifyDb.ChituDraws.Where(d => d.ChituPlanId == plan.Id && d.DrawMonthIndex == 1).ToListAsync();
        Assert.Single(draws);

        var benefits = await verifyDb.WinnerBenefits.Where(b => b.ChituSlotId == slot.Id).ToListAsync();
        Assert.Single(benefits);

        var alerts = await verifyDb.PlanAlerts.Where(a => a.ChituSlotId == slot.Id && a.EventType == "ChituWinner").ToListAsync();
        Assert.Single(alerts);

        var notifications = await verifyDb.Notifications.Where(n => n.CustomerId == _testCustomerId && n.Type == "chitu_winner" && n.PlanAlertId == alerts[0].Id).ToListAsync();
        Assert.Single(notifications);

        // Cleanup
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"PlanAlerts\" WHERE \"PlanId\" = {0}", planId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Notifications\" WHERE \"CustomerId\" = {0}", _testCustomerId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"WinnerBenefits\" WHERE \"ChituSlotId\" = {0}", slotId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituDraws\" WHERE \"ChituPlanId\" = {0}", planId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituInstalments\" WHERE \"ChituSlotId\" = {0}", slotId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituSlots\" WHERE \"ChituPlanId\" = {0}", planId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"ChituPlans\" WHERE \"Id\" = {0}", planId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffRoles\" WHERE \"StaffId\" = {0}", staff.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffBranches\" WHERE \"StaffId\" = {0}", staff.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffMembers\" WHERE \"Id\" = {0}", staff.Id);
    }

    [Fact]
    public async Task Postgres_Simultaneous_JewelleryPaymentFinalization_SeparateConnections_AssertsSingleCreditAndSafeConflict()
    {
        // 1. Arrange isolated enrolment with 4.0g accumulated towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "Simultaneous Jewellery Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quoteId = Guid.NewGuid();
        var quote = new JewelleryPaymentQuote
        {
            Id = quoteId,
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        var payload = new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = $"PG-SIMUL-PAY-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresLiveGateway"
        };

        // 2. Act: Synchronized simultaneous payment finalization across two separate physical database connections
        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            await using var db1 = CreateSeparateDbContext();
            var notif1 = new CustomerNotificationService(db1, NullLogger<CustomerNotificationService>.Instance);
            var finalizer1 = new JewelleryPaymentFinalizationService(db1, notif1, NullLogger<JewelleryPaymentFinalizationService>.Instance);

            barrier.SignalAndWait();
            return await finalizer1.FinalizePaymentAsync(payload);
        });

        var task2 = Task.Run(async () =>
        {
            await using var db2 = CreateSeparateDbContext();
            var notif2 = new CustomerNotificationService(db2, NullLogger<CustomerNotificationService>.Instance);
            var finalizer2 = new JewelleryPaymentFinalizationService(db2, notif2, NullLogger<JewelleryPaymentFinalizationService>.Instance);

            barrier.SignalAndWait();
            return await finalizer2.FinalizePaymentAsync(payload);
        });

        var results = await Task.WhenAll(task1, task2);

        // 3. Assert Financial Integrity: BOTH requests terminate safely without unhandled exception
        Assert.All(results, r => Assert.True(r.Success));

        // Assert database state via fresh independent connection: Exactly 5.0g saved, strictly NO duplicate credit!
        await using var verifyDb = CreateSeparateDbContext();
        var reloadedEnrolment = await verifyDb.JewelleryEnrolments
            .Include(e => e.Contributions)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId);

        Assert.NotNull(reloadedEnrolment);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, reloadedEnrolment.Status);
        Assert.Equal(5.0m, reloadedEnrolment.TotalSavedGrams); // Strict check: NO duplicate financial credit (5.0g, NOT 6.0g)!
        Assert.Equal(150000m, reloadedEnrolment.TotalContributedCurrency); // 120,000 + 30,000 only

        // Exactly one contribution was recorded for this payment
        Assert.Single(reloadedEnrolment.Contributions);
        Assert.Equal(1.0m, reloadedEnrolment.Contributions.First().GoldGrams);

        // Exactly one completion alert was created
        var alerts = await verifyDb.PlanAlerts
            .Where(a => a.JewelleryEnrolmentId == enrolmentId && a.EventType == "JewelleryCompletion")
            .ToListAsync();
        Assert.Single(alerts);
        Assert.Equal(5.0m, alerts[0].TargetGrams);

        // Exactly one completion notification was created
        var notifications = await verifyDb.Notifications
            .Where(n => n.CustomerId == _testCustomerId && n.Type == "completion" && n.PlanAlertId == alerts[0].Id)
            .ToListAsync();
        Assert.Single(notifications);
    }

    [Fact]
    public async Task Postgres_DuplicateAlertRecovery_SavepointRollback_PreventsOrphanNotificationAndPreservesFinancialResult()
    {
        // 1. Arrange isolated enrolment with 4.0g towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "Savepoint Test Jewellery Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quoteId = Guid.NewGuid();
        var quote = new JewelleryPaymentQuote
        {
            Id = quoteId,
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        var payload = new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = $"PG-SAVEPOINT-PAY-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresSavepointGateway"
        };

        // 2. Act: Set up payment finalization with test instrumentation hook.
        // The hook triggers AFTER existingAlert == null has been evaluated as true,
        // right before _db.SaveChangesAsync() is invoked for the alert insert.
        // This forces the finalizer into the typed PostgresException (23505) catch block,
        // proving savepoint rollback, canonical verification, and clean transaction commit.
        var notifService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizer = new JewelleryPaymentFinalizationService(_db, notifService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        var hookFired = false;
        Guid canonicalNotificationId = Guid.Empty;
        Guid canonicalAlertId = Guid.Empty;

        finalizer.BeforeAlertSaveHook = async () =>
        {
            hookFired = true;
            await using var concurrentDb = CreateSeparateDbContext();

            var initialNotification = new Notification
            {
                CustomerId = _testCustomerId,
                Title = "Jewellery Plan Completed!",
                Message = "Concurrent winner notification",
                Type = "completion",
                IsRead = false,
                ReferenceId = enrolment.Id,
                ReferenceType = "JewelleryEnrolment",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            concurrentDb.Notifications.Add(initialNotification);

            var initialAlert = new PlanAlert
            {
                CustomerId = _testCustomerId,
                EventType = "JewelleryCompletion",
                Module = BusinessModule.Jewellery,
                Title = "Jewellery Plan Completed!",
                Message = "Concurrent winner alert",
                PlanId = _testPlanId,
                PlanNameSnapshot = enrolment.PlanNameSnapshot,
                ReferenceId = enrolment.Id,
                JewelleryEnrolmentId = enrolment.Id,
                TargetGrams = 5.0m,
                BranchId = _testBranchId,
                BranchName = "Test Branch",
                NotificationId = initialNotification.Id,
                IsAcknowledged = false,
                NavigationTarget = $"jewellery-enrolments/{enrolment.Id}",
                CollectionInstruction = "Visit our branch to collect your jewellery.",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            concurrentDb.PlanAlerts.Add(initialAlert);
            initialNotification.PlanAlertId = initialAlert.Id;

            await concurrentDb.SaveChangesAsync();
            canonicalNotificationId = initialNotification.Id;
            canonicalAlertId = initialAlert.Id;
        };

        var result = await finalizer.FinalizePaymentAsync(payload);

        // 3. Assert: Hook fired and recovery branch was executed
        Assert.True(hookFired, "Expected BeforeAlertSaveHook to fire during finalization.");
        Assert.True(result.Success);
        Assert.Equal(1.0m, result.GoldGramsCredited);
        Assert.Equal(5.0m, result.NewTotalSavedGrams);
        Assert.True(result.PlanCompleted);

        // Enrolment reloaded from DB: financial integrity preserved
        await using var verifyDb = CreateSeparateDbContext();
        var reloadedEnrolment = await verifyDb.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(reloadedEnrolment);
        Assert.Equal(5.0m, reloadedEnrolment.TotalSavedGrams);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, reloadedEnrolment.Status);

        // Exactly 1 alert in DB (the canonical alert committed by concurrent transaction; no duplicate!)
        var alerts = await verifyDb.PlanAlerts
            .Where(a => a.JewelleryEnrolmentId == enrolmentId)
            .ToListAsync();
        Assert.Single(alerts);
        Assert.Equal(canonicalAlertId, alerts[0].Id);
        Assert.Equal("JewelleryCompletion", alerts[0].EventType);

        // Exactly 1 notification in DB (savepoint rollback ensured NO orphan notification exists in PostgreSQL!)
        var notifications = await verifyDb.Notifications
            .Where(n => n.ReferenceId == enrolmentId && n.ReferenceType == "JewelleryEnrolment")
            .ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(canonicalNotificationId, notifications[0].Id);
        Assert.Equal(alerts[0].Id, notifications[0].PlanAlertId);
        Assert.Equal(notifications[0].Id, alerts[0].NotificationId);

        // Reconcile EventType retrieval: Both JewelleryCompletion and JewelleryPlanCompletion aliases retrieve the alert
        var alertService = new PlanAlertService(verifyDb, NullLogger<PlanAlertService>.Instance);
        var retrievedAlert = await alertService.GetEnrolmentAlertAsync(_testCustomerId, enrolmentId);
        Assert.NotNull(retrievedAlert);
        Assert.Equal(alerts[0].Id, retrievedAlert.AlertId);
        Assert.Equal("JewelleryCompletion", retrievedAlert.EventType);

        var config = new ConfigurationBuilder().Build();
        var mockGoldRate = new MockGoldRateService();
        var verifyNotif = new CustomerNotificationService(verifyDb, NullLogger<CustomerNotificationService>.Instance);
        var customerService = new CustomerJewelleryPlanService(verifyDb, mockGoldRate, verifyNotif, config, NullLogger<CustomerJewelleryPlanService>.Instance);
        var enrolmentDto = await customerService.GetEnrolmentDetailsAsync(_testCustomerId, enrolmentId);
        Assert.NotNull(enrolmentDto.CompletionAlert);
        Assert.Equal(alerts[0].Id, enrolmentDto.CompletionAlert.AlertId);
        Assert.Single(enrolmentDto.Alerts);
    }

    [Fact]
    public async Task Postgres_UnrelatedConstraintFailure_IsNotSwallowed_AndTransactionRollsBack()
    {
        // 1. Arrange isolated enrolment with 4.0g towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "Unrelated Failure Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quoteId = Guid.NewGuid();
        var quote = new JewelleryPaymentQuote
        {
            Id = quoteId,
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        var payload = new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = $"PG-UNRELATED-FAIL-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };

        var notifService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizer = new JewelleryPaymentFinalizationService(_db, notifService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        // Inject an unrelated foreign key failure (invalid BranchId) before alert save
        finalizer.BeforeAlertSaveHook = () =>
        {
            var trackedAlert = _db.PlanAlerts.Local.FirstOrDefault();
            if (trackedAlert != null)
            {
                trackedAlert.BranchId = Guid.NewGuid(); // non-existent BranchId causes FK violation
            }
            return Task.CompletedTask;
        };

        // 2. Assert: The unrelated constraint failure is NOT swallowed by IsAlertUniquenessViolation and is re-thrown
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => finalizer.FinalizePaymentAsync(payload));

        // 3. Assert: Transaction was completely rolled back; no payment or gold grams committed
        await using var verifyDb = CreateSeparateDbContext();
        var verifyEnrolment = await verifyDb.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(verifyEnrolment);
        Assert.Equal(4.0m, verifyEnrolment.TotalSavedGrams);
        Assert.Equal(JewelleryEnrolmentStatus.Active, verifyEnrolment.Status);

        var verifyQuote = await verifyDb.JewelleryPaymentQuotes.FindAsync(quoteId);
        Assert.NotNull(verifyQuote);
        Assert.Equal(JewelleryQuoteStatus.Active, verifyQuote.Status);

        var payments = await verifyDb.Payments.Where(p => p.ProviderReference == payload.ProviderReference).ToListAsync();
        Assert.Empty(payments);
    }

    [Fact]
    public async Task Postgres_CompetingPayments_ForFinalRemainingGrams_PreventsTargetOverrun()
    {
        // 1. Arrange isolated enrolment with 4.0g towards 5.0g target (1.0g remaining)
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "Competing Payments Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        // Two distinct quotes, each for 1.0g credit
        var quote1 = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var quote2 = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.AddRange(quote1, quote2);
        await _db.SaveChangesAsync();

        var notifService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizer = new JewelleryPaymentFinalizationService(_db, notifService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        var payload1 = new PaymentVerificationPayload
        {
            QuoteId = quote1.Id,
            ProviderReference = $"PG-COMPETE-PAY1-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };
        var payload2 = new PaymentVerificationPayload
        {
            QuoteId = quote2.Id,
            ProviderReference = $"PG-COMPETE-PAY2-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };

        // 2. Act: Finalize payment 1 (credits 1.0g, reaching 5.0g target)
        var result1 = await finalizer.FinalizePaymentAsync(payload1);
        Assert.True(result1.Success);
        Assert.Equal(1.0m, result1.GoldGramsCredited);
        Assert.Equal(5.0m, result1.NewTotalSavedGrams);
        Assert.True(result1.PlanCompleted);

        // Act: Finalize payment 2 (remaining target is now 0.0g; business rule flags overcredit, places in explicit reconciliation state)
        var result2 = await finalizer.FinalizePaymentAsync(payload2);
        Assert.False(result2.Success);
        Assert.True(result2.RequiresReconciliation);
        Assert.Equal(0.0m, result2.GoldGramsCredited); // 0.0g credited to enrolment to prevent overrun
        Assert.Equal(1.0m, result2.QuotedGoldGrams); // Full original quoted grams preserved
        Assert.Equal(30000m, result2.AmountCollected); // Full verified money preserved
        Assert.Equal(5.0m, result2.NewTotalSavedGrams); // Strict check: NO target overrun (not 6.0g!)
        Assert.Contains("exceeds remaining target", result2.Message);

        // 3. Assert in database: strict 5.0g target adhered to, exactly 1 completion alert
        await using var verifyDb = CreateSeparateDbContext();
        var reloaded = await verifyDb.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(reloaded);
        Assert.Equal(5.0m, reloaded.TotalSavedGrams);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, reloaded.Status);

        // Assert payment 2 is recorded in explicit reconciliation state (NOT silently marked succeeded!)
        var pay2 = await verifyDb.Payments.FindAsync(result2.PaymentId);
        Assert.NotNull(pay2);
        Assert.Equal(PaymentStatus.RequiresReconciliation, pay2.Status);
        Assert.Equal(30000m, pay2.Amount);
        Assert.Contains("REQUIRES RECONCILIATION", pay2.Remarks);

        // Assert quote 2 preserves original locked financial values
        var q2 = await verifyDb.JewelleryPaymentQuotes.FindAsync(quote2.Id);
        Assert.NotNull(q2);
        Assert.Equal(JewelleryQuoteStatus.Used, q2.Status);
        Assert.Equal(1.0m, q2.TotalGoldCredit);
        Assert.Equal(30000m, q2.AmountToPay);

        var alerts = await verifyDb.PlanAlerts.Where(a => a.JewelleryEnrolmentId == enrolmentId).ToListAsync();
        Assert.Single(alerts);

        var notifs = await verifyDb.Notifications.Where(n => n.ReferenceId == enrolmentId && n.Type == "completion").ToListAsync();
        Assert.Single(notifs);
    }

    [Fact]
    public async Task Postgres_InStorePayment_ExceedingRemainingTarget_IsRejectedBeforeCollection()
    {
        // 1. Arrange isolated enrolment with 4.0g towards 5.0g target (1.0g remaining)
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "In-Store Overpayment Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            PremiumPercentage = 0m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        // Add confirmed payment & contribution of 4.0g so initial state is consistent
        var initialPayment = new Payment
        {
            BranchId = _testBranchId,
            CustomerId = _testCustomerId,
            Module = BusinessModule.Jewellery,
            Amount = 120000m,
            Currency = "LKR",
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Succeeded,
            ProviderReference = $"INIT-CASH-{Guid.NewGuid():N}",
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
        _db.Payments.Add(initialPayment);

        var initialContrib = new JewelleryContribution
        {
            JewelleryEnrolmentId = enrolmentId,
            CurrencyAmount = 120000m,
            GoldGrams = 4.0m,
            AppliedRatePerGram = 30000m,
            Karat = 24,
            PaymentId = initialPayment.Id,
            ConfirmedAtUtc = DateTimeOffset.UtcNow
        };
        _db.JewelleryContributions.Add(initialContrib);

        var staffId = Guid.NewGuid();
        var superAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
        if (superAdminRole == null)
        {
            superAdminRole = new Role { Id = Guid.NewGuid(), Name = "SuperAdmin" };
            _db.Roles.Add(superAdminRole);
        }
        var staff = new Staff
        {
            Id = staffId,
            FullName = "InStore Overpayment Staff",
            Email = $"overpay_staff_{Guid.NewGuid():N}@sjewls.lk",
            IsActive = true
        };
        _db.StaffMembers.Add(staff);
        _db.StaffRoles.Add(new StaffRole { StaffId = staffId, RoleId = superAdminRole.Id });
        _db.StaffBranches.Add(new StaffBranch { StaffId = staffId, BranchId = _testBranchId });
        await _db.SaveChangesAsync();

        var adminService = new AdminJewelleryEnrolmentService(
            _db,
            new MockGoldRateService(),
            new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance),
            new AuditLogService(_db, NullLogger<AuditLogService>.Instance),
            NullLogger<AdminJewelleryEnrolmentService>.Instance);

        // 2. Act & Assert: Attempt in-store payment of 2.0g when only 1.0g is remaining
        var request = new AdminRecordPaymentRequest
        {
            Mode = "BuyGram",
            RequestedGrams = 2.0m,
            ConfirmedReceived = true,
            Notes = "Excessive cash payment test"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            adminService.RecordPaymentAsync(staffId, enrolmentId, request, null));

        Assert.Contains("exceeds the remaining target", ex.Message);
        Assert.Contains("Overpayments are not allowed", ex.Message);

        // 3. Verify in database: zero payments created, zero contributions, balances untouched
        await using var verifyDb = CreateSeparateDbContext();
        var reloaded = await verifyDb.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(reloaded);
        Assert.Equal(4.0m, reloaded.TotalSavedGrams);
        Assert.Equal(JewelleryEnrolmentStatus.Active, reloaded.Status);

        var payments = await verifyDb.Payments.Where(p => p.Remarks == "Excessive cash payment test").ToListAsync();
        Assert.Empty(payments);

        // Teardown staff
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffRoles\" WHERE \"StaffId\" = {0}", staffId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffBranches\" WHERE \"StaffId\" = {0}", staffId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"JewelleryContributions\" WHERE \"JewelleryEnrolmentId\" = {0}", enrolmentId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Payments\" WHERE \"Id\" = {0}", initialPayment.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffMembers\" WHERE \"Id\" = {0}", staffId);
    }

    [Fact]
    public async Task Postgres_InStoreCashPayment_CompletingTarget_CreatesAlertAndNotification_InRealDatabase()
    {
        // 1. Arrange isolated enrolment with 4.0g towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "In-Store Cash Completion Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            PremiumPercentage = 0m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var staffId = Guid.NewGuid();
        var superAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
        if (superAdminRole == null)
        {
            superAdminRole = new Role { Id = Guid.NewGuid(), Name = "SuperAdmin" };
            _db.Roles.Add(superAdminRole);
        }
        var staff = new Staff
        {
            Id = staffId,
            FullName = "InStore Cash Staff",
            Email = $"cash_staff_{Guid.NewGuid():N}@sjewls.lk",
            IsActive = true
        };
        _db.StaffMembers.Add(staff);
        _db.StaffRoles.Add(new StaffRole { StaffId = staffId, RoleId = superAdminRole.Id });
        _db.StaffBranches.Add(new StaffBranch { StaffId = staffId, BranchId = _testBranchId });
        await _db.SaveChangesAsync();

        var adminService = new AdminJewelleryEnrolmentService(
            _db,
            new MockGoldRateService(),
            new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance),
            new AuditLogService(_db, NullLogger<AuditLogService>.Instance),
            NullLogger<AdminJewelleryEnrolmentService>.Instance);

        // 2. Act: Record in-store cash payment completing the remaining 1.0g
        var request = new AdminRecordPaymentRequest
        {
            Mode = "BuyGram",
            RequestedGrams = 1.0m,
            ConfirmedReceived = true
        };
        var result = await adminService.RecordPaymentAsync(staffId, enrolmentId, request, null);

        // 3. Assert
        Assert.True(result.Success);
        Assert.True(result.PlanCompleted);
        Assert.Equal(1.0m, result.NetCreditedGrams);

        await using var verifyDb = CreateSeparateDbContext();
        var reloaded = await verifyDb.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(reloaded);
        Assert.Equal(5.0m, reloaded.TotalSavedGrams);
        Assert.Equal(JewelleryEnrolmentStatus.Completed, reloaded.Status);

        var alerts = await verifyDb.PlanAlerts.Where(a => a.JewelleryEnrolmentId == enrolmentId).ToListAsync();
        Assert.Single(alerts);
        Assert.Equal("JewelleryCompletion", alerts[0].EventType);
        Assert.False(alerts[0].IsAcknowledged);

        var notifs = await verifyDb.Notifications.Where(n => n.ReferenceId == enrolmentId && n.Type == "completion").ToListAsync();
        Assert.Single(notifs);

        // Teardown payment and staff
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"PaymentAllocations\" WHERE \"PaymentId\" = {0}", result.PaymentId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"JewelleryContributions\" WHERE \"PaymentId\" = {0}", result.PaymentId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Payments\" WHERE \"Id\" = {0}", result.PaymentId);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffRoles\" WHERE \"StaffId\" = {0}", staff.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffBranches\" WHERE \"StaffId\" = {0}", staff.Id);
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"StaffMembers\" WHERE \"Id\" = {0}", staff.Id);
    }

    [Fact]
    public async Task Postgres_NoAlertOrNotification_FromPendingOrFailedPayment_InRealDatabase()
    {
        // 1. Arrange isolated enrolment with 4.0g towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "No Alert Pending Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        // Create an active (pending) quote
        var quote = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        // 2. Assert: A pending quote or preview NEVER produces completion alerts or notifications
        await using var verifyDb = CreateSeparateDbContext();
        var alerts = await verifyDb.PlanAlerts.Where(a => a.JewelleryEnrolmentId == enrolmentId).ToListAsync();
        Assert.Empty(alerts);

        var notifs = await verifyDb.Notifications.Where(n => n.ReferenceId == enrolmentId && n.Type == "completion").ToListAsync();
        Assert.Empty(notifs);
    }

    [Fact]
    public async Task Postgres_DatabaseAudit_VerifyTestRecordsCleanupAndRemainingSchema()
    {
        // Audit primary Supabase PostgreSQL database tables and remaining data
        var appliedMigrations = await _db.Database
            .SqlQueryRaw<string>("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"")
            .ToListAsync();

        Assert.Contains("20261119033921_AddPlanAlertsAndNotificationLinks", appliedMigrations);

        // Verify PlanAlerts table exists and queryable
        var planAlertsCount = await _db.PlanAlerts.CountAsync();
        var notificationsCount = await _db.Notifications.CountAsync();
        var branchesCount = await _db.Branches.CountAsync();
        var customersCount = await _db.Customers.CountAsync();
        var jewelleryPlansCount = await _db.JewelleryPlans.CountAsync();
        var jewelleryEnrolmentsCount = await _db.JewelleryEnrolments.CountAsync();
        var chituPlansCount = await _db.ChituPlans.CountAsync();
        var chituSlotsCount = await _db.ChituSlots.CountAsync();

        // Check for any lingering test rows with test prefixes
        var testBranches = await _db.Branches.CountAsync(b => b.Code.StartsWith("TST-"));
        var testCustomers = await _db.Customers.CountAsync(c => c.Email.StartsWith("pg_test_"));
        var testJewelleryPlans = await _db.JewelleryPlans.CountAsync(p => p.Code.StartsWith("JP-PG-"));
        var testChituPlans = await _db.ChituPlans.CountAsync(p => p.Code.StartsWith("CP-PG-"));

        _output?.WriteLine($"[SUPABASE AUDIT] Applied Migrations Count: {appliedMigrations.Count}");
        _output?.WriteLine($"[SUPABASE AUDIT] Latest Migration: {appliedMigrations.Last()}");
        _output?.WriteLine($"[SUPABASE AUDIT] Table Counts: PlanAlerts={planAlertsCount}, Notifications={notificationsCount}, Branches={branchesCount}, Customers={customersCount}, JewelleryPlans={jewelleryPlansCount}, JewelleryEnrolments={jewelleryEnrolmentsCount}, ChituPlans={chituPlansCount}, ChituSlots={chituSlotsCount}");
        _output?.WriteLine($"[SUPABASE AUDIT] Lingering Test Records: TestBranches={testBranches}, TestCustomers={testCustomers}, TestJewelleryPlans={testJewelleryPlans}, TestChituPlans={testChituPlans}");

        // Verify unique index existence in postgres pg_indexes catalog
        var indexNames = await _db.Database
            .SqlQueryRaw<string>("SELECT indexname FROM pg_indexes WHERE tablename = 'PlanAlerts'")
            .ToListAsync();

        Assert.Contains("IX_PlanAlerts_ChituSlotId_ChituRoundIndex", indexNames);
        Assert.Contains("IX_PlanAlerts_JewelleryEnrolmentId_EventType", indexNames);
    }

    [Fact]
    public async Task Postgres_ReconciliationCallbackReplay_InspectsLinkedPayment_NeverReportsSuccess()
    {
        // 1. Arrange enrolment with 4.0g towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "Replay Reconciliation Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 4.0m,
            TotalContributedCurrency = 120000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quote1 = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        var quote2 = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.AddRange(quote1, quote2);
        await _db.SaveChangesAsync();

        var notifService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizer = new JewelleryPaymentFinalizationService(_db, notifService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        var payload1 = new PaymentVerificationPayload
        {
            QuoteId = quote1.Id,
            ProviderReference = $"PG-REPLAY-PAY1-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };
        var sharedRef2 = $"PG-REPLAY-PAY2-{Guid.NewGuid():N}";
        var payload2 = new PaymentVerificationPayload
        {
            QuoteId = quote2.Id,
            ProviderReference = sharedRef2,
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };

        // Payment 1 completes the plan (credits 1.0g to reach 5.0g target)
        var res1 = await finalizer.FinalizePaymentAsync(payload1);
        Assert.True(res1.Success);

        // Payment 2 exceeds remaining target (now 0.0g remaining) -> placed in RequiresReconciliation
        var res2 = await finalizer.FinalizePaymentAsync(payload2);
        Assert.False(res2.Success);
        Assert.True(res2.RequiresReconciliation);
        Assert.Equal(0m, res2.GoldGramsCredited);
        Assert.Equal(1.0m, res2.QuotedGoldGrams);
        Assert.Equal(30000m, res2.AmountCollected);

        // 2. ACT: Replay the EXACT same callback for quote2
        var replayRes = await finalizer.FinalizePaymentAsync(payload2);

        // ASSERT: Inspects linked payment; reports existing reconciliation outcome; NEVER reports success!
        Assert.False(replayRes.Success);
        Assert.True(replayRes.RequiresReconciliation);
        Assert.Equal(res2.PaymentId, replayRes.PaymentId);
        Assert.Equal(0m, replayRes.GoldGramsCredited);
        Assert.Equal(1.0m, replayRes.QuotedGoldGrams);
        Assert.Equal(30000m, replayRes.AmountCollected);
        Assert.Contains("exceeds remaining target", replayRes.Message);

        // 3. ACT & ASSERT: Attempt callback replay using the same provider reference with a DIFFERENT quote
        var thirdQuote = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(thirdQuote);
        await _db.SaveChangesAsync();

        var conflictingPayload = new PaymentVerificationPayload
        {
            QuoteId = thirdQuote.Id,
            ProviderReference = sharedRef2, // re-used provider reference for different quote
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finalizer.FinalizePaymentAsync(conflictingPayload));
        Assert.Contains("already been used for another payment", ex.Message);

        // 4. ACT & ASSERT: Attempt replay for used quote with a DIFFERENT provider reference
        var differentRefPayload = new PaymentVerificationPayload
        {
            QuoteId = quote2.Id,
            ProviderReference = $"PG-DIFFERENT-REF-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };
        var diffRefEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finalizer.FinalizePaymentAsync(differentRefPayload));
        Assert.Contains("already been finalized with provider reference", diffRefEx.Message);

        // 5. ACT & ASSERT: Attempt to finalize an expired quote
        var expiredQuote = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Expired,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        _db.JewelleryPaymentQuotes.Add(expiredQuote);
        await _db.SaveChangesAsync();

        var expiredPayload = new PaymentVerificationPayload
        {
            QuoteId = expiredQuote.Id,
            ProviderReference = $"PG-EXPIRED-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };
        var expEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            finalizer.FinalizePaymentAsync(expiredPayload));
        Assert.Contains("expired", expEx.Message);
    }

    [Fact]
    public async Task Postgres_TransientFailure_ExecutionStrategyRetriesWithFreshTrackedState_Succeeds()
    {
        // 1. Arrange isolated enrolment with 3.0g towards 5.0g target
        var enrolmentId = Guid.NewGuid();
        var enrolment = new JewelleryEnrolment
        {
            Id = enrolmentId,
            CustomerId = _testCustomerId,
            JewelleryPlanId = _testPlanId,
            BranchId = _testBranchId,
            PlanNameSnapshot = "Transient Retry Test Plan",
            TargetProductWeightGrams = 5.0m,
            TotalSavedGrams = 3.0m,
            TotalContributedCurrency = 90000m,
            Status = JewelleryEnrolmentStatus.Active
        };
        _db.JewelleryEnrolments.Add(enrolment);
        _cleanupEnrolmentIds.Add(enrolmentId);

        var quote = new JewelleryPaymentQuote
        {
            Id = Guid.NewGuid(),
            JewelleryEnrolmentId = enrolment.Id,
            CustomerId = _testCustomerId,
            AmountToPay = 30000m,
            TotalGoldCredit = 1.0m,
            RatePerGram = 30000m,
            Currency = "LKR",
            Status = JewelleryQuoteStatus.Active,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync();

        var notifService = new CustomerNotificationService(_db, NullLogger<CustomerNotificationService>.Instance);
        var finalizer = new JewelleryPaymentFinalizationService(_db, notifService, NullLogger<JewelleryPaymentFinalizationService>.Instance);

        var attemptTracker = new List<int>();
        finalizer.OnAttemptHook = async (attempt) =>
        {
            attemptTracker.Add(attempt);
            if (attempt == 1)
            {
                // Simulate transient timeout on attempt 1
                throw new TimeoutException("Simulated transient database connection timeout on attempt 1");
            }
            await Task.CompletedTask;
        };

        var payload = new PaymentVerificationPayload
        {
            QuoteId = quote.Id,
            ProviderReference = $"PG-RETRY-{Guid.NewGuid():N}",
            AmountPaid = 30000m,
            Currency = "LKR",
            ProviderName = "PostgresGateway"
        };

        // 2. Act: Finalize payment. Execution strategy catches TimeoutException on attempt 1 and retries
        var result = await finalizer.FinalizePaymentAsync(payload);

        // 3. Assert: Successfully executed through retry attempt
        Assert.True(result.Success);
        Assert.Equal(1.0m, result.GoldGramsCredited);
        Assert.Equal(4.0m, result.NewTotalSavedGrams);
        Assert.False(result.PlanCompleted);
        Assert.Contains(1, attemptTracker);
        Assert.Contains(2, attemptTracker);

        // Verify database: fresh tracked entities committed cleanly without detached entity exceptions
        await using var verifyDb = CreateSeparateDbContext();
        var reloaded = await verifyDb.JewelleryEnrolments.FindAsync(enrolmentId);
        Assert.NotNull(reloaded);
        Assert.Equal(4.0m, reloaded.TotalSavedGrams);

        var pay = await verifyDb.Payments.FindAsync(result.PaymentId);
        Assert.NotNull(pay);
        Assert.Equal(PaymentStatus.Succeeded, pay.Status);
    }

    [Fact]
    public async Task Postgres_AlertAcknowledgement_AtomicConditionalUpdate_PreservesFirstTimestampUnderConcurrency()
    {
        // 1. Arrange: isolated plan alert
        var alertId = Guid.NewGuid();
        var initialNotification = new Notification
        {
            CustomerId = _testCustomerId,
            Title = "Atomic Ack Notification",
            Message = "Ack test message",
            Type = "completion",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.Notifications.Add(initialNotification);

        var alert = new PlanAlert
        {
            Id = alertId,
            CustomerId = _testCustomerId,
            EventType = "JewelleryCompletion",
            Module = BusinessModule.Jewellery,
            Title = "Atomic Ack Alert",
            Message = "Ack test message",
            PlanId = _testPlanId,
            PlanNameSnapshot = "Ack Test Plan",
            ReferenceId = Guid.NewGuid(),
            BranchId = _testBranchId,
            BranchName = "Test Branch",
            NotificationId = initialNotification.Id,
            IsAcknowledged = false,
            AcknowledgedAtUtc = null,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.PlanAlerts.Add(alert);
        await _db.SaveChangesAsync();

        var alertService = new PlanAlertService(_db, NullLogger<PlanAlertService>.Instance);

        // 2. Act 1: First acknowledgement
        var firstDto = await alertService.AcknowledgeAlertAsync(_testCustomerId, alertId);
        Assert.True(firstDto.IsAcknowledged);
        Assert.NotNull(firstDto.AcknowledgedAtUtc);
        var firstTimestamp = firstDto.AcknowledgedAtUtc.Value;

        // Artificial delay so wall-clock advances
        await Task.Delay(50);

        // Act 2: Concurrent/subsequent acknowledgements from multiple threads
        var concurrentTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var separateDb = CreateSeparateDbContext();
            var service = new PlanAlertService(separateDb, NullLogger<PlanAlertService>.Instance);
            return await service.AcknowledgeAlertAsync(_testCustomerId, alertId);
        }).ToList();

        var results = await Task.WhenAll(concurrentTasks);

        // 3. Assert: All returned DTOs preserve the exact first timestamp
        foreach (var r in results)
        {
            Assert.True(r.IsAcknowledged);
            Assert.Equal(firstTimestamp, r.AcknowledgedAtUtc);
        }

        // Verify in database: persisted record retained first timestamp
        await using var verifyDb = CreateSeparateDbContext();
        var reloadedAlert = await verifyDb.PlanAlerts.FindAsync(alertId);
        Assert.NotNull(reloadedAlert);
        Assert.True(reloadedAlert.IsAcknowledged);
        Assert.Equal(firstTimestamp, reloadedAlert.AcknowledgedAtUtc);

        // Teardown
        await verifyDb.Database.ExecuteSqlRawAsync("DELETE FROM \"PlanAlerts\" WHERE \"Id\" = {0}", alertId);
        await verifyDb.Database.ExecuteSqlRawAsync("DELETE FROM \"Notifications\" WHERE \"Id\" = {0}", initialNotification.Id);
    }
}
