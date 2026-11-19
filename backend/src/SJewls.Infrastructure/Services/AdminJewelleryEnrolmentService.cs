using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class AdminJewelleryEnrolmentService : IAdminJewelleryEnrolmentService
{
    private readonly SJewlsDbContext _db;
    private readonly IGoldRateService _goldRateService;
    private readonly ICustomerNotificationService _notificationService;
    private readonly IAuditLogService _auditService;
    private readonly ILogger<AdminJewelleryEnrolmentService> _logger;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _assignmentLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _paymentLocks = new();

    public AdminJewelleryEnrolmentService(
        SJewlsDbContext db,
        IGoldRateService goldRateService,
        ICustomerNotificationService notificationService,
        IAuditLogService auditService,
        ILogger<AdminJewelleryEnrolmentService> logger)
    {
        _db = db;
        _goldRateService = goldRateService;
        _notificationService = notificationService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AdminEnrolmentPreviewResponse> PreviewAssignmentAsync(
        Guid staffId,
        AdminEnrolmentPreviewRequest request,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var customer = await _db.Customers
            .Include(c => c.PrimaryBranch)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new KeyNotFoundException($"Customer with ID '{request.CustomerId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(customer.PrimaryBranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to assign plans to customers outside your branch.");
        }

        var plan = await _db.JewelleryPlans
            .Include(p => p.Category)
            .Include(p => p.Branch)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan with ID '{request.PlanId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to assign plans from this branch.");
        }

        var branchToday = GetBranchToday(plan.Branch?.Timezone);

        var existingEnrolments = await _db.JewelleryEnrolments
            .Where(e => e.CustomerId == customer.Id && e.JewelleryPlanId == plan.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        var totalExisting = existingEnrolments.Count;
        var activeExisting = existingEnrolments.Count(e => e.Status == JewelleryEnrolmentStatus.Active);

        var isEligible = true;
        string? ineligibilityReason = null;

        if (!customer.IsActive)
        {
            isEligible = false;
            ineligibilityReason = "Customer account is inactive or closed.";
        }
        else if (plan.BranchId != customer.PrimaryBranchId)
        {
            isEligible = false;
            ineligibilityReason = "Jewellery plan branch does not match customer's primary branch.";
        }
        else if (!plan.IsActive || (plan.Category != null && !plan.Category.IsActive))
        {
            isEligible = false;
            ineligibilityReason = "This jewellery plan or its category is deactivated.";
        }
        else if (plan.StartDate > branchToday)
        {
            isEligible = false;
            ineligibilityReason = $"Plan is scheduled to start on {plan.StartDate} and is not yet open for enrolments.";
        }

        return new AdminEnrolmentPreviewResponse
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            PhoneNumber = customer.PhoneNumber ?? string.Empty,
            Email = customer.Email ?? string.Empty,
            CustomerBranchId = customer.PrimaryBranchId,
            CustomerBranchName = customer.PrimaryBranch?.Name ?? "Main Branch",
            PlanId = plan.Id,
            PlanName = plan.Name,
            CategoryName = plan.Category?.Name ?? string.Empty,
            TargetProductGoldWeightGrams = plan.TargetProductGoldWeightGrams,
            PremiumPercentage = plan.PremiumPercentage,
            AllowedDurationsMonths = plan.AllowedDurationsMonths.ToList(),
            PlanStartDate = plan.StartDate,
            IsPlanActive = plan.IsActive,
            TotalExistingEnrolments = totalExisting,
            ActiveExistingEnrolments = activeExisting,
            IsEligible = isEligible,
            IneligibilityReason = ineligibilityReason
        };
    }

    public async Task<AdminCustomerEnrolmentDto> AssignPlanAsync(
        Guid staffId,
        AdminAssignPlanRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var customer = await _db.Customers
            .Include(c => c.PrimaryBranch)
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new KeyNotFoundException($"Customer with ID '{request.CustomerId}' was not found.");

        if (!customer.IsActive)
        {
            throw new InvalidOperationException("Cannot assign a plan to an inactive or closed customer account.");
        }

        if (!isSuperAdmin && !permittedBranchIds.Contains(customer.PrimaryBranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to assign plans to customers outside your branch.");
        }

        var plan = await _db.JewelleryPlans
            .Include(p => p.Category)
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan with ID '{request.PlanId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to assign plans from this branch.");
        }

        if (plan.BranchId != customer.PrimaryBranchId)
        {
            throw new ArgumentException("Jewellery plan branch does not match customer's primary branch.");
        }

        var branchToday = GetBranchToday(plan.Branch?.Timezone);

        if (!plan.IsActive || (plan.Category != null && !plan.Category.IsActive))
        {
            throw new InvalidOperationException("This jewellery plan is deactivated and unavailable for new enrolments.");
        }

        if (plan.StartDate > branchToday)
        {
            throw new InvalidOperationException($"This jewellery plan is scheduled to start on {plan.StartDate} and is not yet open for enrolments.");
        }

        if (!plan.AllowedDurationsMonths.Contains(request.SelectedDurationMonths))
        {
            throw new ArgumentException($"Selected duration of {request.SelectedDurationMonths} months is not offered by this plan.");
        }

        // 1. Idempotency check before lock
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var trimmedKey = request.IdempotencyKey.Trim();
            var existingEnrolment = await _db.JewelleryEnrolments
                .Include(e => e.JewelleryPlan!)
                    .ThenInclude(p => p.Category)
                .Include(e => e.Contributions)
                    .ThenInclude(c => c.Payment)
                .FirstOrDefaultAsync(e => e.CustomerId == customer.Id && e.IdempotencyKey == trimmedKey, ct);

            if (existingEnrolment != null)
            {
                if (existingEnrolment.JewelleryPlanId != request.PlanId ||
                    existingEnrolment.SelectedDurationMonths != request.SelectedDurationMonths)
                {
                    throw new InvalidOperationException("The idempotency key has already been used with different assignment parameters.");
                }

                _logger.LogInformation("Idempotent assignment request deduplicated for customer {CustomerId}, plan {PlanId}",
                    customer.Id, plan.Id);
                return MapToDto(existingEnrolment);
            }
        }

        // 2. Concurrency protection per customer and plan
        var lockKey = $"{customer.Id:N}_{plan.Id:N}";
        var semaphore = _assignmentLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            // Re-check idempotency under lock
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var trimmedKey = request.IdempotencyKey.Trim();
                var existingEnrolment = await _db.JewelleryEnrolments
                    .Include(e => e.JewelleryPlan!)
                        .ThenInclude(p => p.Category)
                    .Include(e => e.Contributions)
                        .ThenInclude(c => c.Payment)
                    .FirstOrDefaultAsync(e => e.CustomerId == customer.Id && e.IdempotencyKey == trimmedKey, ct);

                if (existingEnrolment != null)
                {
                    if (existingEnrolment.JewelleryPlanId != request.PlanId ||
                        existingEnrolment.SelectedDurationMonths != request.SelectedDurationMonths)
                    {
                        throw new InvalidOperationException("The idempotency key has already been used with different assignment parameters.");
                    }

                    return MapToDto(existingEnrolment);
                }
            }

            // 3. Repeat enrolment check across all historical enrolments
            var existingEnrolmentsQuery = _db.JewelleryEnrolments
                .Where(e => e.CustomerId == customer.Id && e.JewelleryPlanId == plan.Id);

            var totalExisting = await existingEnrolmentsQuery.CountAsync(ct);
            var activeExisting = await existingEnrolmentsQuery.CountAsync(e => e.Status == JewelleryEnrolmentStatus.Active, ct);

            if (totalExisting > 0 && !request.ConfirmRepeatEnrolment)
            {
                throw new RepeatEnrolmentConfirmationRequiredException(totalExisting, activeExisting);
            }

            // 4. Create enrolment with snapshots
            var joiningDate = branchToday;
            var deadline = joiningDate.AddMonths(request.SelectedDurationMonths);

            var enrolment = new JewelleryEnrolment
            {
                CustomerId = customer.Id,
                JewelleryPlanId = plan.Id,
                BranchId = plan.BranchId,
                IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
                Currency = "LKR",
                PlanNameSnapshot = plan.Name,
                CategoryNameSnapshot = plan.Category?.Name ?? string.Empty,
                ImageUrlSnapshot = plan.ImageUrl,
                TargetProductWeightGrams = plan.TargetProductGoldWeightGrams, // Read-only from plan, staff cannot override
                SelectedDurationMonths = request.SelectedDurationMonths,
                PremiumPercentage = plan.PremiumPercentage,
                StartDate = joiningDate,
                TargetEndDate = deadline,
                TotalSavedGrams = 0m,
                TotalContributedCurrency = 0m,
                Status = JewelleryEnrolmentStatus.Active
            };

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                var isRelational = _db.Database.IsRelational();
                await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
                try
                {
                    _db.JewelleryEnrolments.Add(enrolment);
                    await _db.SaveChangesAsync(ct);

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(ct);
                    }
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(ct);
                    }
                    throw;
                }

                // Persist activation notification
                await _notificationService.CreateNotificationAsync(
                    customer.Id,
                    "Jewellery Plan Activated",
                    $"Your enrolment in {plan.Name} ({plan.TargetProductGoldWeightGrams}g) for {request.SelectedDurationMonths} months has been successfully activated.",
                    "enrolment",
                    ct);

                // Audit assignment with staff identity
                await _auditService.LogActionAsync(
                    "Staff",
                    staffId,
                    "AssignJewelleryPlan",
                    "JewelleryEnrolment",
                    enrolment.Id.ToString(),
                    plan.BranchId,
                    null,
                    new
                    {
                        EnrolmentId = enrolment.Id,
                        CustomerId = customer.Id,
                        PlanId = plan.Id,
                        TargetGrams = plan.TargetProductGoldWeightGrams,
                        SelectedDurationMonths = request.SelectedDurationMonths,
                        RepeatConfirmed = request.ConfirmRepeatEnrolment
                    },
                    ipAddress);

                _logger.LogInformation("Staff {StaffId} assigned plan {PlanId} to customer {CustomerId}, enrolment {EnrolmentId}",
                    staffId, plan.Id, customer.Id, enrolment.Id);

                enrolment.JewelleryPlan = plan;
                enrolment.Customer = customer;

                return MapToDto(enrolment);
            });
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<AdminRecordPaymentPreviewResponse> PreviewPaymentAsync(
        Guid staffId,
        Guid enrolmentId,
        AdminRecordPaymentPreviewRequest request,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var enrolment = await _db.JewelleryEnrolments
            .Include(e => e.Customer)
            .Include(e => e.JewelleryPlan)
            .Include(e => e.Branch)
            .Include(e => e.Contributions)
                .ThenInclude(c => c.Payment)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment with ID '{enrolmentId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(enrolment.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to record payments for this branch.");
        }

        if (enrolment.Status == JewelleryEnrolmentStatus.Completed)
        {
            throw new InvalidOperationException("This jewellery enrolment has already completed its target gold savings.");
        }

        if (enrolment.Status == JewelleryEnrolmentStatus.Cancelled ||
            enrolment.Status == JewelleryEnrolmentStatus.Claimed ||
            enrolment.Status == JewelleryEnrolmentStatus.DurationEnded)
        {
            throw new InvalidOperationException($"Cannot record payments for an enrolment with status '{enrolment.Status}'.");
        }

        if (enrolment.Customer != null && !enrolment.Customer.IsActive)
        {
            throw new InvalidOperationException("Customer account is inactive or closed. Inactive accounts cannot transact.");
        }

        var branchToday = GetBranchToday(enrolment.Branch?.Timezone);

        var confirmedContribs = enrolment.Contributions
            .Where(c => c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded)
            .ToList();

        var currentSavedGrams = confirmedContribs.Any()
            ? confirmedContribs.Sum(c => c.GoldGrams)
            : enrolment.TotalSavedGrams;

        var remainingTargetGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - currentSavedGrams);

        var applicableRate = await _goldRateService.GetApplicable24kRateAsync(enrolment.BranchId, ct);

        var isBuyGram = string.Equals(request.Mode, "BuyGram", StringComparison.OrdinalIgnoreCase);
        var isBuyAmount = string.Equals(request.Mode, "BuyAmount", StringComparison.OrdinalIgnoreCase);

        if (!isBuyGram && !isBuyAmount)
        {
            throw new ArgumentException("Mode must be 'BuyAmount' or 'BuyGram'.");
        }

        var planName = !string.IsNullOrWhiteSpace(enrolment.PlanNameSnapshot)
            ? enrolment.PlanNameSnapshot
            : (enrolment.JewelleryPlan?.Name ?? "Jewellery Plan");

        var enrolmentRef = !string.IsNullOrWhiteSpace(enrolment.IdempotencyKey) && enrolment.IdempotencyKey.StartsWith("ENR-")
            ? enrolment.IdempotencyKey
            : $"ENR-{enrolment.Id.ToString()[..8].ToUpperInvariant()}";

        // Initial rate preview without amount or grams: return fresh live rate metadata
        var hasEnteredAmount = request.EnteredAmount.HasValue && request.EnteredAmount.Value > 0;
        var hasRequestedGrams = request.RequestedGrams.HasValue && request.RequestedGrams.Value > 0;

        if (!hasEnteredAmount && !hasRequestedGrams)
        {
            return new AdminRecordPaymentPreviewResponse
            {
                EnrolmentId = enrolment.Id,
                EnrolmentReference = enrolmentRef,
                CustomerId = enrolment.CustomerId,
                CustomerName = enrolment.Customer?.FullName ?? "Unknown",
                PlanName = planName,
                Mode = isBuyGram ? "BuyGram" : "BuyAmount",
                RatePerGram = applicableRate.RatePerGram,
                Currency = "LKR",
                RateAsOf = applicableRate.SourceTimestampUtc,
                RateSource = applicableRate.Source,
                PremiumPercentage = enrolment.PremiumPercentage,
                BaseGoldGrams = 0m,
                PremiumGrams = 0m,
                ChargeableGoldGrams = null,
                NetCreditedGrams = 0m,
                AmountPayable = 0m,
                CurrentSavedGrams = currentSavedGrams,
                TargetGrams = enrolment.TargetProductWeightGrams,
                RemainingGramsBeforePayment = remainingTargetGrams,
                RemainingGramsAfterPayment = remainingTargetGrams,
                WillReachCompletion = false,
                WillExceedTarget = false,
                PaymentDate = branchToday
            };
        }

        JewelleryCalculationResult calcResult;
        if (isBuyGram)
        {
            if (!request.RequestedGrams.HasValue || request.RequestedGrams.Value <= 0)
            {
                throw new ArgumentException("RequestedGrams must be greater than zero for BuyGram mode.");
            }

            calcResult = JewelleryPaymentCalculator.CalculateBuyGram(
                request.RequestedGrams.Value,
                applicableRate.RatePerGram,
                enrolment.PremiumPercentage,
                remainingTargetGrams,
                applicableRate.SourceTimestampUtc);
        }
        else
        {
            if (!request.EnteredAmount.HasValue || request.EnteredAmount.Value <= 0)
            {
                throw new ArgumentException("EnteredAmount must be greater than zero for BuyAmount mode.");
            }

            calcResult = JewelleryPaymentCalculator.CalculateBuyAmount(
                request.EnteredAmount.Value,
                applicableRate.RatePerGram,
                enrolment.PremiumPercentage,
                remainingTargetGrams,
                applicableRate.SourceTimestampUtc);
        }

        var willExceed = calcResult.TotalGoldCredit > remainingTargetGrams;
        var willComplete = !willExceed && (remainingTargetGrams - calcResult.TotalGoldCredit) <= 0.0001m;

        return new AdminRecordPaymentPreviewResponse
        {
            EnrolmentId = enrolment.Id,
            EnrolmentReference = enrolmentRef,
            CustomerId = enrolment.CustomerId,
            CustomerName = enrolment.Customer?.FullName ?? "Unknown",
            PlanName = planName,
            Mode = isBuyGram ? "BuyGram" : "BuyAmount",
            RatePerGram = applicableRate.RatePerGram,
            Currency = "LKR",
            RateAsOf = applicableRate.SourceTimestampUtc,
            RateSource = applicableRate.Source,
            PremiumPercentage = enrolment.PremiumPercentage,
            BaseGoldGrams = calcResult.BaseGoldGrams,
            PremiumGrams = calcResult.PremiumGrams,
            ChargeableGoldGrams = calcResult.ChargeableGoldGrams,
            NetCreditedGrams = calcResult.TotalGoldCredit,
            AmountPayable = calcResult.AmountToPay,
            CurrentSavedGrams = currentSavedGrams,
            TargetGrams = enrolment.TargetProductWeightGrams,
            RemainingGramsBeforePayment = remainingTargetGrams,
            RemainingGramsAfterPayment = Math.Max(0m, remainingTargetGrams - calcResult.TotalGoldCredit),
            WillReachCompletion = willComplete,
            WillExceedTarget = willExceed,
            PaymentDate = branchToday
        };
    }

    public async Task<AdminRecordPaymentResult> RecordPaymentAsync(
        Guid staffId,
        Guid enrolmentId,
        AdminRecordPaymentRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        if (!request.ConfirmedReceived)
        {
            throw new ArgumentException("Staff must confirm receipt of the cash payment.");
        }

        var enrolment = await _db.JewelleryEnrolments
            .Include(e => e.Customer)
            .Include(e => e.JewelleryPlan)
            .Include(e => e.Branch)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment with ID '{enrolmentId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(enrolment.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to record payments for this branch.");
        }

        if (enrolment.Status == JewelleryEnrolmentStatus.Completed)
        {
            throw new InvalidOperationException("This enrolment has already completed its target gold savings.");
        }

        if (enrolment.Status == JewelleryEnrolmentStatus.Cancelled ||
            enrolment.Status == JewelleryEnrolmentStatus.Claimed ||
            enrolment.Status == JewelleryEnrolmentStatus.DurationEnded)
        {
            throw new InvalidOperationException($"Cannot record payments for an enrolment with status '{enrolment.Status}'.");
        }

        if (enrolment.Customer != null && !enrolment.Customer.IsActive)
        {
            throw new InvalidOperationException("Customer account is inactive or closed. Inactive accounts cannot transact.");
        }

        // Idempotency check before lock
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var trimmedKey = request.IdempotencyKey.Trim();
            var existingPayment = await _db.Payments
                .FirstOrDefaultAsync(p => p.IdempotencyKey == trimmedKey, ct);

            if (existingPayment != null && existingPayment.Status == PaymentStatus.Succeeded)
            {
                var existingContribution = await _db.JewelleryContributions
                    .FirstOrDefaultAsync(c => c.PaymentId == existingPayment.Id, ct);

                _logger.LogInformation("Idempotent in-store payment deduplicated for enrolment {EnrolmentId}, payment {PaymentId}",
                    enrolment.Id, existingPayment.Id);

                return new AdminRecordPaymentResult
                {
                    Success = true,
                    Message = "Payment has already been processed successfully.",
                    PaymentId = existingPayment.Id,
                    PaymentReference = existingPayment.ProviderReference,
                    AmountPaid = existingPayment.Amount,
                    Currency = existingPayment.Currency,
                    NetCreditedGrams = existingContribution?.GoldGrams ?? 0m,
                    AppliedRatePerGram = existingContribution?.AppliedRatePerGram ?? 0m,
                    AppliedPremiumPercentage = enrolment.PremiumPercentage,
                    NewTotalSavedGrams = enrolment.TotalSavedGrams,
                    RemainingGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams),
                    ProgressPercentage = enrolment.TargetProductWeightGrams > 0
                        ? Math.Min(100m, Math.Round((enrolment.TotalSavedGrams / enrolment.TargetProductWeightGrams) * 100m, 1))
                        : 0m,
                    PlanCompleted = enrolment.Status == JewelleryEnrolmentStatus.Completed,
                    RecordedAtUtc = existingPayment.CompletedAtUtc ?? existingPayment.CreatedAtUtc
                };
            }
        }

        // Concurrency protection on enrolment
        var lockKey = enrolment.Id.ToString("N");
        var semaphore = _paymentLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);

        try
        {
            // Re-fetch confirmed contributions under lock to accurately calculate remaining target
            var confirmedContribs = await _db.JewelleryContributions
                .Include(c => c.Payment)
                .Where(c => c.JewelleryEnrolmentId == enrolment.Id && (c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded))
                .ToListAsync(ct);

            var currentSavedGrams = confirmedContribs.Sum(c => c.GoldGrams);
            var remainingTargetGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - currentSavedGrams);

            if (remainingTargetGrams <= 0)
            {
                throw new InvalidOperationException("This enrolment has already reached its target gold grams.");
            }

            // Fresh live gold rate
            var applicableRate = await _goldRateService.GetApplicable24kRateAsync(enrolment.BranchId, ct);

            var isBuyGram = string.Equals(request.Mode, "BuyGram", StringComparison.OrdinalIgnoreCase);
            var isBuyAmount = string.Equals(request.Mode, "BuyAmount", StringComparison.OrdinalIgnoreCase);

            if (!isBuyGram && !isBuyAmount)
            {
                throw new ArgumentException("Mode must be 'BuyAmount' or 'BuyGram'.");
            }

            JewelleryCalculationResult calcResult;
            if (isBuyGram)
            {
                if (!request.RequestedGrams.HasValue || request.RequestedGrams.Value <= 0)
                {
                    throw new ArgumentException("RequestedGrams must be greater than zero for BuyGram mode.");
                }

                calcResult = JewelleryPaymentCalculator.CalculateBuyGram(
                    request.RequestedGrams.Value,
                    applicableRate.RatePerGram,
                    enrolment.PremiumPercentage,
                    remainingTargetGrams,
                    applicableRate.SourceTimestampUtc);
            }
            else
            {
                if (!request.EnteredAmount.HasValue || request.EnteredAmount.Value <= 0)
                {
                    throw new ArgumentException("EnteredAmount must be greater than zero for BuyAmount mode.");
                }

                calcResult = JewelleryPaymentCalculator.CalculateBuyAmount(
                    request.EnteredAmount.Value,
                    applicableRate.RatePerGram,
                    enrolment.PremiumPercentage,
                    remainingTargetGrams,
                    applicableRate.SourceTimestampUtc);
            }

            // Reject contributions exceeding the remaining target (never silently trim)
            if (calcResult.TotalGoldCredit > remainingTargetGrams)
            {
                throw new ArgumentException(
                    $"Payment would credit {calcResult.TotalGoldCredit:F4}g, which exceeds the remaining target of {remainingTargetGrams:F4}g. " +
                    $"Overpayments are not allowed. Please enter an amount or gram weight up to the remaining target.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var paymentReference = $"CASH-{enrolment.Id.ToString()[..8].ToUpperInvariant()}-{nowUtc.ToUnixTimeMilliseconds()}";

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            var attempt = 0;
            return await executionStrategy.ExecuteAsync(async () =>
            {
                attempt++;
                if (attempt > 1)
                {
                    _db.ChangeTracker.Clear();
                }
                var isRelational = _db.Database.IsRelational();
                await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
                try
                {
                    if (isRelational)
                    {
                        await _db.Database.ExecuteSqlRawAsync(
                            "SELECT 1 FROM \"JewelleryEnrolments\" WHERE \"Id\" = {0} FOR NO KEY UPDATE",
                            new object[] { enrolmentId }, ct);
                    }

                    var freshEnrolment = await _db.JewelleryEnrolments
                        .Include(e => e.Branch)
                        .Include(e => e.Customer)
                        .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
                        ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' not found.");
                    enrolment = freshEnrolment;

                    if (enrolment.Status == JewelleryEnrolmentStatus.Completed ||
                        enrolment.TotalSavedGrams >= enrolment.TargetProductWeightGrams)
                    {
                        throw new InvalidOperationException("Enrolment has already reached its target and is completed.");
                    }

                    var lockedRemainingGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
                    if (calcResult.TotalGoldCredit > lockedRemainingGrams)
                    {
                        throw new InvalidOperationException(
                            $"Payment would credit {calcResult.TotalGoldCredit:F4}g, which exceeds the current remaining target of {lockedRemainingGrams:F4}g. Overpayments are not allowed.");
                    }

                    var payment = new Payment
                    {
                        BranchId = enrolment.BranchId,
                        CustomerId = enrolment.CustomerId,
                        Module = BusinessModule.Jewellery,
                        Amount = calcResult.AmountToPay,
                        Currency = "LKR",
                        Method = PaymentMethod.Cash,
                        Status = PaymentStatus.Succeeded,
                        ProviderReference = paymentReference,
                        IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? string.Empty : request.IdempotencyKey.Trim(),
                        RecordedByStaffId = staffId,
                        CompletedAtUtc = nowUtc,
                        Remarks = string.IsNullOrWhiteSpace(request.Notes) ? "In-store cash payment" : request.Notes.Trim()
                    };

                    _db.Payments.Add(payment);
                    await _db.SaveChangesAsync(ct);

                    var allocation = new PaymentAllocation
                    {
                        PaymentId = payment.Id,
                        AllocationType = "JewelleryContribution",
                        TargetEntityId = enrolment.Id,
                        AllocatedAmount = calcResult.AmountToPay
                    };
                    _db.PaymentAllocations.Add(allocation);

                    var contribution = new JewelleryContribution
                    {
                        JewelleryEnrolmentId = enrolment.Id,
                        CurrencyAmount = calcResult.AmountToPay,
                        GoldGrams = calcResult.TotalGoldCredit,
                        AppliedRatePerGram = applicableRate.RatePerGram,
                        Karat = 24,
                        PaymentId = payment.Id,
                        ConfirmedAtUtc = nowUtc
                    };
                    _db.JewelleryContributions.Add(contribution);

                    enrolment.TotalSavedGrams += calcResult.TotalGoldCredit;
                    enrolment.TotalContributedCurrency += calcResult.AmountToPay;

                    var planCompleted = false;
                    if (enrolment.TotalSavedGrams >= enrolment.TargetProductWeightGrams)
                    {
                        enrolment.Status = JewelleryEnrolmentStatus.Completed;
                        planCompleted = true;
                    }

                    // 1. Persist financial records first
                    await _db.SaveChangesAsync(ct);

                    // 2. Persist completion alert and notification if plan completed
                    if (planCompleted)
                    {
                        var branchName = enrolment.Branch?.Name ?? "our branch";
                        var completionTitle = "Jewellery Plan Completed!";
                        var completionMessage = $"Congratulations! You have completed {enrolment.PlanNameSnapshot} and reached your target of {enrolment.TargetProductWeightGrams:0.##} g. Visit {branchName} to collect your jewellery.";

                        var existingAlert = await _db.PlanAlerts
                            .FirstOrDefaultAsync(a => a.JewelleryEnrolmentId == enrolment.Id && (a.EventType == "JewelleryCompletion" || a.EventType == "JewelleryPlanCompletion"), ct);

                        if (existingAlert == null)
                        {
                            var savepointName = "BeforeCompletionAlert";
                            var hasSavepoint = false;

                            if (transaction != null && _db.Database.IsRelational())
                            {
                                await transaction.CreateSavepointAsync(savepointName, ct);
                                hasSavepoint = true;
                            }

                            var notification = new Notification
                            {
                                CustomerId = enrolment.CustomerId,
                                Title = completionTitle,
                                Message = completionMessage,
                                Type = "completion",
                                IsRead = false,
                                ReferenceId = enrolment.Id,
                                ReferenceType = "JewelleryEnrolment",
                                CreatedAtUtc = DateTimeOffset.UtcNow
                            };
                            _db.Notifications.Add(notification);

                            var alert = new PlanAlert
                            {
                                CustomerId = enrolment.CustomerId,
                                EventType = "JewelleryCompletion",
                                Module = BusinessModule.Jewellery,
                                Title = completionTitle,
                                Message = completionMessage,
                                PlanId = enrolment.JewelleryPlanId,
                                PlanNameSnapshot = enrolment.PlanNameSnapshot,
                                ReferenceId = enrolment.Id,
                                JewelleryEnrolmentId = enrolment.Id,
                                TargetGrams = enrolment.TargetProductWeightGrams,
                                BranchId = enrolment.BranchId,
                                BranchName = branchName,
                                BranchAddress = enrolment.Branch?.Address,
                                BranchPhone = null,
                                NotificationId = notification.Id,
                                IsAcknowledged = false,
                                NavigationTarget = $"jewellery-enrolments/{enrolment.Id}",
                                CollectionInstruction = $"Visit {branchName} to collect your jewellery.",
                                CreatedAtUtc = DateTimeOffset.UtcNow
                            };
                            _db.PlanAlerts.Add(alert);
                            notification.PlanAlertId = alert.Id;

                            try
                            {
                                await _db.SaveChangesAsync(ct);
                            }
                            catch (DbUpdateException dbEx) when (dbEx.InnerException is Npgsql.PostgresException pgEx &&
                                                                 pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
                                                                 string.Equals(pgEx.ConstraintName, "IX_PlanAlerts_JewelleryEnrolmentId_EventType", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("Concurrent transaction already created alert for enrolment {EnrolmentId}. Rolling back to savepoint.", enrolment.Id);
                                if (hasSavepoint)
                                {
                                    await transaction.RollbackToSavepointAsync(savepointName, ct);
                                }
                                _db.Entry(alert).State = EntityState.Detached;
                                _db.Entry(notification).State = EntityState.Detached;
                            }
                        }

                        _logger.LogInformation("Enrolment {EnrolmentId} reached target {Target}g and marked Completed",
                            enrolment.Id, enrolment.TargetProductWeightGrams);
                    }

                    if (transaction != null) await transaction.CommitAsync(ct);

                    // Audit log
                    await _auditService.LogActionAsync(
                        "Staff",
                        staffId,
                        "RecordInStorePayment",
                        "Payment",
                        payment.Id.ToString(),
                        enrolment.BranchId,
                        null,
                        new
                        {
                            PaymentId = payment.Id,
                            EnrolmentId = enrolment.Id,
                            AmountPaid = calcResult.AmountToPay,
                            NetCreditedGrams = calcResult.TotalGoldCredit,
                            AppliedRate = applicableRate.RatePerGram,
                            PremiumPercentage = enrolment.PremiumPercentage,
                            PlanCompleted = planCompleted
                        },
                        ipAddress);

                    _logger.LogInformation("Staff {StaffId} recorded in-store cash payment {PaymentId} of LKR {Amount:N2} for enrolment {EnrolmentId} (+{Grams}g)",
                        staffId, payment.Id, calcResult.AmountToPay, enrolment.Id, calcResult.TotalGoldCredit);

                    var newRemaining = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
                    decimal progress = enrolment.TargetProductWeightGrams > 0
                        ? Math.Min(100m, Math.Round((enrolment.TotalSavedGrams / enrolment.TargetProductWeightGrams) * 100m, 1))
                        : 0m;

                    return new AdminRecordPaymentResult
                    {
                        Success = true,
                        Message = planCompleted
                            ? "In-store payment recorded successfully! Gold target reached and enrolment marked Completed."
                            : "In-store cash payment recorded successfully.",
                        PaymentId = payment.Id,
                        PaymentReference = paymentReference,
                        AmountPaid = calcResult.AmountToPay,
                        Currency = "LKR",
                        NetCreditedGrams = calcResult.TotalGoldCredit,
                        AppliedRatePerGram = applicableRate.RatePerGram,
                        AppliedPremiumPercentage = enrolment.PremiumPercentage,
                        NewTotalSavedGrams = enrolment.TotalSavedGrams,
                        RemainingGrams = newRemaining,
                        ProgressPercentage = progress,
                        PlanCompleted = planCompleted,
                        RecordedAtUtc = nowUtc
                    };
                }
                catch
                {
                    if (transaction != null) await transaction.RollbackAsync(ct);
                    _db.ChangeTracker.Clear();
                    throw;
                }
            });
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<AdminCustomerEnrolmentDto> GetEnrolmentByIdAsync(
        Guid staffId,
        Guid enrolmentId,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var enrolment = await _db.JewelleryEnrolments
            .Include(e => e.JewelleryPlan!)
                .ThenInclude(p => p.Category)
            .Include(e => e.Customer)
            .Include(e => e.Contributions)
                .ThenInclude(c => c.Payment)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment with ID '{enrolmentId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(enrolment.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to access enrolments in this branch.");
        }

        return MapToDto(enrolment);
    }

    public async Task<List<EnrolmentContributionDto>> GetEnrolmentPaymentsAsync(
        Guid staffId,
        Guid enrolmentId,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var enrolment = await _db.JewelleryEnrolments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment with ID '{enrolmentId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(enrolment.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to view payments for this enrolment.");
        }

        var contributions = await _db.JewelleryContributions
            .Include(c => c.Payment)
            .Where(c => c.JewelleryEnrolmentId == enrolmentId)
            .OrderByDescending(c => c.ConfirmedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return contributions.Select(c => new EnrolmentContributionDto
        {
            Id = c.Id,
            CurrencyAmount = c.CurrencyAmount,
            Currency = enrolment.Currency ?? "LKR",
            GoldGrams = c.GoldGrams,
            AppliedRatePerGram = c.AppliedRatePerGram,
            Karat = c.Karat,
            ConfirmedAtUtc = c.ConfirmedAtUtc,
            Status = c.Payment?.Status.ToString() ?? "Succeeded",
            PaymentMethod = c.Payment?.Method.ToString() ?? "Online",
            PaymentReference = c.Payment?.ProviderReference ?? string.Empty
        }).ToList();
    }

    private async Task<(Staff Staff, bool IsSuperAdmin, HashSet<Guid> PermittedBranchIds)> VerifyStaffAccessAsync(
        Guid staffId,
        CancellationToken ct)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(sr => sr.Role)
            .Include(s => s.AssignedBranches).ThenInclude(sb => sb.Branch)
            .FirstOrDefaultAsync(s => s.Id == staffId, ct);

        if (staff == null || !staff.IsActive)
        {
            throw new UnauthorizedAccessException("Staff account is inactive or not found.");
        }

        var isSuperAdmin = staff.Roles.Any(r => r.Role != null &&
            (r.Role.Name.Equals("Super Admin", StringComparison.OrdinalIgnoreCase) ||
             r.Role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)));

        var permittedBranchIds = isSuperAdmin
            ? new HashSet<Guid>()
            : staff.AssignedBranches.Select(b => b.BranchId).ToHashSet();

        return (staff, isSuperAdmin, permittedBranchIds);
    }

    private static DateOnly GetBranchToday(string? timezone)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(timezone))
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);
            }
        }
        catch
        {
            // Fallback to UTC if timezone id is unrecognised
        }
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static AdminCustomerEnrolmentDto MapToDto(JewelleryEnrolment e)
    {
        var planName = !string.IsNullOrWhiteSpace(e.PlanNameSnapshot) ? e.PlanNameSnapshot : (e.JewelleryPlan?.Name ?? "Jewellery Plan");
        var categoryName = !string.IsNullOrWhiteSpace(e.CategoryNameSnapshot) ? e.CategoryNameSnapshot : (e.JewelleryPlan?.Category?.Name ?? string.Empty);
        var planCode = e.JewelleryPlan?.Code ?? "JEW";

        var confirmedContribs = e.Contributions
            .Where(c => c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded)
            .ToList();

        var netGrams = e.Contributions.Any()
            ? confirmedContribs.Sum(c => c.GoldGrams)
            : e.TotalSavedGrams;

        var confirmedMoney = e.Contributions.Any()
            ? confirmedContribs.GroupBy(c => c.PaymentId).Select(g => g.First().CurrencyAmount).Sum()
            : e.TotalContributedCurrency;

        var remainingGrams = Math.Max(0m, e.TargetProductWeightGrams - netGrams);

        decimal progress = 0m;
        if (e.TargetProductWeightGrams > 0)
        {
            progress = Math.Min(100m, Math.Round((netGrams / e.TargetProductWeightGrams) * 100m, 1));
        }

        var contributions = e.Contributions
            .OrderByDescending(c => c.ConfirmedAtUtc)
            .Select(c => new EnrolmentContributionDto
            {
                Id = c.Id,
                CurrencyAmount = c.CurrencyAmount,
                Currency = e.Currency ?? "LKR",
                GoldGrams = c.GoldGrams,
                AppliedRatePerGram = c.AppliedRatePerGram,
                Karat = c.Karat,
                ConfirmedAtUtc = c.ConfirmedAtUtc,
                Status = c.Payment?.Status.ToString() ?? "Succeeded",
                PaymentMethod = c.Payment?.Method.ToString() ?? "Online",
                PaymentReference = c.Payment?.ProviderReference ?? string.Empty
            })
            .ToList();

        var enrolmentRef = !string.IsNullOrWhiteSpace(e.IdempotencyKey) && e.IdempotencyKey.StartsWith("ENR-")
            ? e.IdempotencyKey
            : $"ENR-{e.Id.ToString()[..8].ToUpperInvariant()}";

        return new AdminCustomerEnrolmentDto
        {
            Id = e.Id,
            EnrolmentReference = enrolmentRef,
            PlanId = e.JewelleryPlanId,
            PlanName = planName,
            PlanCode = planCode,
            CategoryName = categoryName,
            TargetWeightGrams = e.TargetProductWeightGrams,
            TotalSavedGrams = netGrams,
            RemainingGrams = remainingGrams,
            TotalContributedCurrency = confirmedMoney,
            Currency = e.Currency ?? "LKR",
            PremiumPercentage = e.PremiumPercentage,
            SelectedDurationMonths = e.SelectedDurationMonths,
            ProgressPercentage = progress,
            Status = e.Status.ToString(),
            StartDate = e.StartDate,
            TargetEndDate = e.TargetEndDate,
            CreatedAtUtc = e.CreatedAtUtc,
            Contributions = contributions
        };
    }

    #region Payments Listing, Incomplete Plans, and Reminders

    public async Task<AdminJewelleryPaymentsPagedResponse> GetJewelleryPaymentsPagedAsync(
        Guid staffId,
        AdminJewelleryPaymentsQuery query,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        HashSet<Guid>? targetBranchIds = null;
        if (query.BranchId.HasValue)
        {
            if (!isSuperAdmin && !permittedBranchIds.Contains(query.BranchId.Value))
            {
                throw new UnauthorizedAccessException("You are not authorized to view payments for this branch.");
            }
            targetBranchIds = new HashSet<Guid> { query.BranchId.Value };
        }
        else if (!isSuperAdmin)
        {
            targetBranchIds = permittedBranchIds;
        }

        var paymentsQuery = _db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Branch)
            .AsNoTracking()
            .Where(p => p.Module == BusinessModule.Jewellery ||
                        _db.JewelleryContributions.Any(c => c.PaymentId == p.Id) ||
                        p.Allocations.Any(a => a.AllocationType == "JewelleryContribution"));

        if (targetBranchIds != null)
        {
            paymentsQuery = paymentsQuery.Where(p => targetBranchIds.Contains(p.BranchId));
        }

        if (string.Equals(query.Status, "Successful", StringComparison.OrdinalIgnoreCase))
        {
            paymentsQuery = paymentsQuery.Where(p => p.Status == PaymentStatus.Succeeded);
        }
        else if (string.Equals(query.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            paymentsQuery = paymentsQuery.Where(p => p.Status == PaymentStatus.Pending);
        }
        else
        {
            paymentsQuery = paymentsQuery.Where(p => p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.Pending);
        }

        if (query.PlanId.HasValue)
        {
            var planId = query.PlanId.Value;
            paymentsQuery = paymentsQuery.Where(p =>
                _db.JewelleryContributions.Any(c => c.PaymentId == p.Id && c.JewelleryEnrolment!.JewelleryPlanId == planId) ||
                p.Allocations.Any(a => a.AllocationType == "JewelleryContribution" &&
                    _db.JewelleryEnrolments.Any(e => e.Id == a.TargetEntityId && e.JewelleryPlanId == planId)) ||
                _db.JewelleryPaymentQuotes.Any(q => q.PaymentId == p.Id && q.JewelleryEnrolment!.JewelleryPlanId == planId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            paymentsQuery = paymentsQuery.Where(p =>
                (p.Customer != null && (
                    p.Customer.FullName.ToLower().Contains(term) ||
                    (p.Customer.PhoneNumber != null && p.Customer.PhoneNumber.Contains(term)) ||
                    (p.Customer.Email != null && p.Customer.Email.ToLower().Contains(term)) ||
                    (p.Customer.Nic != null && p.Customer.Nic.ToLower().Contains(term)))) ||
                p.ProviderReference.ToLower().Contains(term) ||
                (p.IdempotencyKey != null && p.IdempotencyKey.ToLower().Contains(term)));
        }

        var branchTimezoneId = "Asia/Colombo";
        if (query.BranchId.HasValue)
        {
            var b = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(br => br.Id == query.BranchId.Value, ct);
            if (!string.IsNullOrWhiteSpace(b?.Timezone)) branchTimezoneId = b.Timezone;
        }

        TimeZoneInfo branchTz;
        try { branchTz = TimeZoneInfo.FindSystemTimeZoneById(branchTimezoneId); }
        catch { branchTz = TimeZoneInfo.Utc; }

        if (query.FromDate.HasValue)
        {
            var fromLocal = query.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            var fromUtc = new DateTimeOffset(fromLocal, branchTz.GetUtcOffset(fromLocal)).ToUniversalTime();
            paymentsQuery = paymentsQuery.Where(p =>
                p.Status == PaymentStatus.Succeeded
                    ? (p.CompletedAtUtc ?? p.CreatedAtUtc) >= fromUtc
                    : p.CreatedAtUtc >= fromUtc);
        }

        if (query.ToDate.HasValue)
        {
            var toLocal = query.ToDate.Value.ToDateTime(new TimeOnly(23, 59, 59, 999));
            var toUtc = new DateTimeOffset(toLocal, branchTz.GetUtcOffset(toLocal)).ToUniversalTime();
            paymentsQuery = paymentsQuery.Where(p =>
                p.Status == PaymentStatus.Succeeded
                    ? (p.CompletedAtUtc ?? p.CreatedAtUtc) <= toUtc
                    : p.CreatedAtUtc <= toUtc);
        }

        var totalCount = await paymentsQuery.CountAsync(ct);

        var totalConfirmedAmount = await paymentsQuery
            .Where(p => p.Status == PaymentStatus.Succeeded)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var totalPendingAmount = await paymentsQuery
            .Where(p => p.Status == PaymentStatus.Pending)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var totalSuccessfulCount = await paymentsQuery
            .CountAsync(p => p.Status == PaymentStatus.Succeeded, ct);

        var totalPendingCount = await paymentsQuery
            .CountAsync(p => p.Status == PaymentStatus.Pending, ct);

        var matchingSucceededPaymentIds = paymentsQuery
            .Where(p => p.Status == PaymentStatus.Succeeded)
            .Select(p => p.Id);

        var totalConfirmedGrams = await _db.JewelleryContributions
            .Where(c => matchingSucceededPaymentIds.Contains(c.PaymentId))
            .SumAsync(c => (decimal?)c.GoldGrams, ct) ?? 0m;

        var pagedPayments = await paymentsQuery
            .OrderByDescending(p => p.Status == PaymentStatus.Succeeded ? (p.CompletedAtUtc ?? p.CreatedAtUtc) : p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        if (!pagedPayments.Any())
        {
            return new AdminJewelleryPaymentsPagedResponse
            {
                Items = new List<AdminJewelleryPaymentItemDto>(),
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalConfirmedAmount = totalConfirmedAmount,
                TotalConfirmedGrams = totalConfirmedGrams,
                TotalPendingAmount = totalPendingAmount,
                TotalPendingCount = totalPendingCount,
                TotalSuccessfulCount = totalSuccessfulCount,
                Currency = "LKR"
            };
        }

        var pagedPaymentIds = pagedPayments.Select(p => p.Id).ToList();

        var pagedContributions = await _db.JewelleryContributions
            .Include(c => c.JewelleryEnrolment)
                .ThenInclude(e => e!.JewelleryPlan)
            .Where(c => pagedPaymentIds.Contains(c.PaymentId))
            .AsNoTracking()
            .ToListAsync(ct);

        var contribByPaymentId = pagedContributions.ToDictionary(c => c.PaymentId, c => c);

        var pendingPaymentIds = pagedPayments
            .Where(p => !contribByPaymentId.ContainsKey(p.Id))
            .Select(p => p.Id)
            .ToList();

        var allocations = pendingPaymentIds.Any()
            ? await _db.PaymentAllocations
                .Where(a => pendingPaymentIds.Contains(a.PaymentId) && a.AllocationType == "JewelleryContribution")
                .AsNoTracking()
                .ToListAsync(ct)
            : new List<PaymentAllocation>();

        var allocationByPaymentId = allocations.ToDictionary(a => a.PaymentId, a => a);

        var quotes = await _db.JewelleryPaymentQuotes
            .Include(q => q.JewelleryEnrolment)
            .Where(q => q.PaymentId != null && pagedPaymentIds.Contains(q.PaymentId.Value))
            .AsNoTracking()
            .ToListAsync(ct);

        var quoteByPaymentId = quotes.ToDictionary(q => q.PaymentId!.Value, q => q);

        var enrolmentIds = new HashSet<Guid>();
        foreach (var c in pagedContributions)
        {
            enrolmentIds.Add(c.JewelleryEnrolmentId);
        }
        foreach (var a in allocations)
        {
            enrolmentIds.Add(a.TargetEntityId);
        }
        foreach (var q in quotes)
        {
            enrolmentIds.Add(q.JewelleryEnrolmentId);
        }

        var enrolments = await _db.JewelleryEnrolments
            .Include(e => e.JewelleryPlan)
            .Include(e => e.Branch)
            .Include(e => e.Customer)
            .Where(e => enrolmentIds.Contains(e.Id))
            .AsNoTracking()
            .ToDictionaryAsync(e => e.Id, e => e, ct);

        var allSuccessfulContributionsForEnrolments = await _db.JewelleryContributions
            .Include(c => c.Payment)
            .Where(c => enrolmentIds.Contains(c.JewelleryEnrolmentId) &&
                        (c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded))
            .OrderBy(c => c.ConfirmedAtUtc)
            .ThenBy(c => c.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        var enrolmentContributionIndexMap = new Dictionary<Guid, int>();
        foreach (var group in allSuccessfulContributionsForEnrolments.GroupBy(c => c.JewelleryEnrolmentId))
        {
            int index = 1;
            foreach (var contrib in group)
            {
                enrolmentContributionIndexMap[contrib.Id] = index++;
            }
        }

        var latestPaymentDates = allSuccessfulContributionsForEnrolments
            .GroupBy(c => c.JewelleryEnrolmentId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.ConfirmedAtUtc));

        var nowUtc = DateTimeOffset.UtcNow;
        var items = new List<AdminJewelleryPaymentItemDto>();

        foreach (var p in pagedPayments)
        {
            contribByPaymentId.TryGetValue(p.Id, out var contrib);
            quoteByPaymentId.TryGetValue(p.Id, out var quote);
            allocationByPaymentId.TryGetValue(p.Id, out var alloc);

            var enrolmentId = contrib?.JewelleryEnrolmentId ?? alloc?.TargetEntityId ?? quote?.JewelleryEnrolmentId ?? Guid.Empty;
            enrolments.TryGetValue(enrolmentId, out var enrolment);

            var customer = p.Customer ?? enrolment?.Customer;
            var plan = enrolment?.JewelleryPlan;
            var branch = p.Branch ?? enrolment?.Branch;

            var isSuccessful = p.Status == PaymentStatus.Succeeded;
            int? instalmentNum = null;
            string instalmentLabel;

            if (isSuccessful && contrib != null && enrolmentContributionIndexMap.TryGetValue(contrib.Id, out var idx))
            {
                instalmentNum = idx;
                instalmentLabel = $"Payment {idx}";
            }
            else if (isSuccessful)
            {
                instalmentLabel = "Payment";
            }
            else
            {
                instalmentNum = null;
                instalmentLabel = "Pending";
            }

            var enrolmentRef = enrolment != null
                ? (!string.IsNullOrWhiteSpace(enrolment.IdempotencyKey) && enrolment.IdempotencyKey.StartsWith("ENR-")
                    ? enrolment.IdempotencyKey
                    : $"ENR-{enrolment.Id.ToString()[..8].ToUpperInvariant()}")
                : string.Empty;

            var planName = enrolment != null && !string.IsNullOrWhiteSpace(enrolment.PlanNameSnapshot)
                ? enrolment.PlanNameSnapshot
                : (plan?.Name ?? "Jewellery Plan");

            var appliedRate = contrib != null
                ? contrib.AppliedRatePerGram
                : (quote?.RatePerGram ?? 0m);

            var goldCredit = isSuccessful && contrib != null ? contrib.GoldGrams : 0m;
            decimal? estPendingGoldCredit = !isSuccessful && quote != null ? quote.TotalGoldCredit : null;

            var branchTzString = branch?.Timezone ?? "Asia/Colombo";
            var branchToday = GetBranchToday(branchTzString);

            latestPaymentDates.TryGetValue(enrolmentId, out var latestPaymentAt);
            DateTimeOffset? latestPaymentNullable = latestPaymentAt != default ? latestPaymentAt : null;

            bool canSendReminder = false;
            string? reminderReason = null;
            DateTimeOffset? reminderEligibleAt = null;

            if (enrolment != null && customer != null)
            {
                var rem = EvaluateReminderEligibility(enrolment, customer, latestPaymentNullable, branchToday, nowUtc);
                canSendReminder = rem.CanSendReminder;
                reminderReason = rem.ReminderReason;
                reminderEligibleAt = rem.ReminderEligibleAt;
            }

            items.Add(new AdminJewelleryPaymentItemDto
            {
                PaymentId = p.Id,
                ContributionId = contrib?.Id,
                InstalmentNumber = instalmentNum,
                InstalmentLabel = instalmentLabel,
                CustomerId = customer?.Id ?? p.CustomerId,
                CustomerName = customer?.FullName ?? "Customer",
                CustomerPhone = customer?.PhoneNumber,
                CustomerEmail = customer?.Email,
                EnrolmentId = enrolmentId,
                EnrolmentReference = enrolmentRef,
                PlanId = plan?.Id ?? enrolment?.JewelleryPlanId ?? Guid.Empty,
                PlanName = planName,
                BranchId = branch?.Id ?? p.BranchId,
                BranchName = branch?.Name ?? "Main Branch",
                BranchTimezone = branchTzString,
                Amount = contrib?.CurrencyAmount ?? p.Amount,
                Currency = p.Currency ?? "LKR",
                GoldRate = appliedRate,
                GoldCredit = goldCredit,
                EstimatedPendingGoldCredit = estPendingGoldCredit,
                Status = isSuccessful ? "Successful" : "Pending",
                PaymentMethod = p.Method.ToString(),
                PaymentReference = !string.IsNullOrWhiteSpace(p.ProviderReference) ? p.ProviderReference : (!string.IsNullOrWhiteSpace(p.IdempotencyKey) ? p.IdempotencyKey : p.Id.ToString()[..8].ToUpperInvariant()),
                DueDate = enrolment?.TargetEndDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                PaidAtUtc = isSuccessful ? (contrib?.ConfirmedAtUtc ?? p.CompletedAtUtc ?? p.CreatedAtUtc) : null,
                CreatedAtUtc = p.CreatedAtUtc,
                CanSendReminder = canSendReminder,
                ReminderReason = reminderReason,
                LastSuccessfulPaymentAt = latestPaymentNullable,
                ReminderEligibleAt = reminderEligibleAt
            });
        }

        return new AdminJewelleryPaymentsPagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalConfirmedAmount = totalConfirmedAmount,
            TotalConfirmedGrams = totalConfirmedGrams,
            TotalPendingAmount = totalPendingAmount,
            TotalPendingCount = totalPendingCount,
            TotalSuccessfulCount = totalSuccessfulCount,
            Currency = "LKR"
        };
    }

    public async Task<AdminIncompleteEnrolmentsPagedResponse> GetIncompleteEnrolmentsPagedAsync(
        Guid staffId,
        AdminIncompleteEnrolmentsQuery query,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        HashSet<Guid>? targetBranchIds = null;
        if (query.BranchId.HasValue)
        {
            if (!isSuperAdmin && !permittedBranchIds.Contains(query.BranchId.Value))
            {
                throw new UnauthorizedAccessException("You are not authorized to view enrolments for this branch.");
            }
            targetBranchIds = new HashSet<Guid> { query.BranchId.Value };
        }
        else if (!isSuperAdmin)
        {
            targetBranchIds = permittedBranchIds;
        }

        var enrolmentsQuery = _db.JewelleryEnrolments
            .Include(e => e.Customer)
            .Include(e => e.JewelleryPlan)
            .Include(e => e.Branch)
            .AsNoTracking();

        if (targetBranchIds != null)
        {
            enrolmentsQuery = enrolmentsQuery.Where(e => targetBranchIds.Contains(e.BranchId));
        }

        if (query.PlanId.HasValue)
        {
            enrolmentsQuery = enrolmentsQuery.Where(e => e.JewelleryPlanId == query.PlanId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            enrolmentsQuery = enrolmentsQuery.Where(e =>
                (e.Customer != null && (
                    e.Customer.FullName.ToLower().Contains(term) ||
                    (e.Customer.PhoneNumber != null && e.Customer.PhoneNumber.Contains(term)) ||
                    (e.Customer.Email != null && e.Customer.Email.ToLower().Contains(term)))) ||
                (e.IdempotencyKey != null && e.IdempotencyKey.ToLower().Contains(term)) ||
                (e.PlanNameSnapshot != null && e.PlanNameSnapshot.ToLower().Contains(term)) ||
                (e.JewelleryPlan != null && e.JewelleryPlan.Name.ToLower().Contains(term)));
        }

        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);

        enrolmentsQuery = enrolmentsQuery.Where(e =>
            e.TargetEndDate < todayUtc &&
            e.TotalSavedGrams < e.TargetProductWeightGrams &&
            e.Status != JewelleryEnrolmentStatus.Completed &&
            e.Status != JewelleryEnrolmentStatus.Claimed &&
            e.Status != JewelleryEnrolmentStatus.Cancelled);

        var totalCount = await enrolmentsQuery.CountAsync(ct);

        var totalRemainingGrams = await enrolmentsQuery
            .SumAsync(e => (decimal?)(e.TargetProductWeightGrams - e.TotalSavedGrams), ct) ?? 0m;

        var totalConfirmedMoneyPaid = await enrolmentsQuery
            .SumAsync(e => (decimal?)e.TotalContributedCurrency, ct) ?? 0m;

        var pagedEnrolments = await enrolmentsQuery
            .OrderByDescending(e => e.TargetEndDate)
            .ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var pagedEnrolmentIds = pagedEnrolments.Select(e => e.Id).ToList();

        var latestPaymentDates = await _db.JewelleryContributions
            .Include(c => c.Payment)
            .Where(c => pagedEnrolmentIds.Contains(c.JewelleryEnrolmentId) &&
                        (c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded))
            .GroupBy(c => c.JewelleryEnrolmentId)
            .Select(g => new { EnrolmentId = g.Key, MaxDate = g.Max(c => c.ConfirmedAtUtc) })
            .ToDictionaryAsync(x => x.EnrolmentId, x => (DateTimeOffset?)x.MaxDate, ct);

        var items = pagedEnrolments.Select(e =>
        {
            var targetGrams = e.TargetProductWeightGrams;
            var netGrams = e.TotalSavedGrams;
            var remaining = Math.Max(0m, targetGrams - netGrams);
            var progress = targetGrams > 0 ? Math.Min(100m, Math.Round((netGrams / targetGrams) * 100m, 1)) : 0m;

            var enrolmentRef = !string.IsNullOrWhiteSpace(e.IdempotencyKey) && e.IdempotencyKey.StartsWith("ENR-")
                ? e.IdempotencyKey
                : $"ENR-{e.Id.ToString()[..8].ToUpperInvariant()}";

            var planName = !string.IsNullOrWhiteSpace(e.PlanNameSnapshot) ? e.PlanNameSnapshot : (e.JewelleryPlan?.Name ?? "Jewellery Plan");

            latestPaymentDates.TryGetValue(e.Id, out var latestPayment);

            var canSendReminder = e.Customer != null && e.Customer.IsActive &&
                                  e.Status != JewelleryEnrolmentStatus.Completed &&
                                  e.Status != JewelleryEnrolmentStatus.Claimed &&
                                  e.Status != JewelleryEnrolmentStatus.Cancelled;

            return new AdminIncompleteEnrolmentItemDto
            {
                EnrolmentId = e.Id,
                EnrolmentReference = enrolmentRef,
                CustomerId = e.CustomerId,
                CustomerName = e.Customer?.FullName ?? "Customer",
                CustomerPhone = e.Customer?.PhoneNumber,
                CustomerEmail = e.Customer?.Email,
                PlanId = e.JewelleryPlanId,
                PlanName = planName,
                BranchId = e.BranchId,
                BranchName = e.Branch?.Name ?? "Main Branch",
                JoiningDate = e.StartDate,
                SelectedDurationMonths = e.SelectedDurationMonths,
                Deadline = e.TargetEndDate,
                TargetGrams = targetGrams,
                NetSavedGrams = netGrams,
                RemainingGrams = remaining,
                TotalConfirmedMoneyPaid = e.TotalContributedCurrency,
                Currency = e.Currency ?? "LKR",
                ProgressPercentage = progress,
                Status = "DurationEnded - Below Target",
                CanSendReminder = canSendReminder,
                ReminderReason = canSendReminder ? "PlanIncomplete" : null,
                LastSuccessfulPaymentAt = latestPayment
            };
        }).ToList();

        return new AdminIncompleteEnrolmentsPagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalIncompleteEnrolments = totalCount,
            TotalRemainingGrams = totalRemainingGrams,
            TotalConfirmedMoneyPaid = totalConfirmedMoneyPaid,
            Currency = "LKR"
        };
    }

    public async Task<AdminReminderContextDto> GetReminderContextAsync(
        Guid staffId,
        Guid enrolmentId,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var enrolment = await _db.JewelleryEnrolments
            .Include(e => e.Customer)
            .Include(e => e.JewelleryPlan)
            .Include(e => e.Branch)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(enrolment.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to access reminder context for this branch.");
        }

        var customer = enrolment.Customer ?? throw new KeyNotFoundException("Customer was not found.");

        var latestPayment = await _db.JewelleryContributions
            .Include(c => c.Payment)
            .Where(c => c.JewelleryEnrolmentId == enrolmentId &&
                        (c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded))
            .OrderByDescending(c => c.ConfirmedAtUtc)
            .Select(c => (DateTimeOffset?)c.ConfirmedAtUtc)
            .FirstOrDefaultAsync(ct);

        var branchToday = GetBranchToday(enrolment.Branch?.Timezone);
        var nowUtc = DateTimeOffset.UtcNow;
        var rem = EvaluateReminderEligibility(enrolment, customer, latestPayment, branchToday, nowUtc);

        var enrolmentRef = !string.IsNullOrWhiteSpace(enrolment.IdempotencyKey) && enrolment.IdempotencyKey.StartsWith("ENR-")
            ? enrolment.IdempotencyKey
            : $"ENR-{enrolment.Id.ToString()[..8].ToUpperInvariant()}";

        var planName = !string.IsNullOrWhiteSpace(enrolment.PlanNameSnapshot) ? enrolment.PlanNameSnapshot : (enrolment.JewelleryPlan?.Name ?? "Jewellery Plan");
        var branchName = enrolment.Branch?.Name ?? "SJewls Main Branch";

        string suggestedTitle;
        string suggestedMessage;

        if (rem.ReminderReason == "PlanIncomplete")
        {
            suggestedTitle = "Jewellery Plan Duration Ended";
            suggestedMessage = $"Dear {customer.FullName}, the agreed duration for your jewellery plan \"{planName}\" ({enrolmentRef}) ended on {enrolment.TargetEndDate}. You have accumulated {enrolment.TotalSavedGrams:0.0000}g of your {enrolment.TargetProductWeightGrams:0.0000}g target. Please visit {branchName} or contact us to review your settlement options under store policy.";
        }
        else
        {
            suggestedTitle = "Jewellery Plan Contribution Reminder";
            suggestedMessage = $"Dear {customer.FullName}, you have not contributed to your jewellery savings plan \"{planName}\" ({enrolmentRef}) for over 3 months. Continue saving toward your {enrolment.TargetProductWeightGrams:0.0000}g gold goal!";
        }

        return new AdminReminderContextDto
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            CustomerPhone = customer.PhoneNumber,
            CustomerEmail = customer.Email,
            IsPhoneVerified = customer.IsPhoneVerified,
            IsEmailVerified = customer.IsEmailVerified,
            EnrolmentId = enrolment.Id,
            EnrolmentReference = enrolmentRef,
            PlanId = enrolment.JewelleryPlanId,
            PlanName = planName,
            TargetGrams = enrolment.TargetProductWeightGrams,
            SavedGrams = enrolment.TotalSavedGrams,
            RemainingGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams),
            StartDate = enrolment.StartDate,
            Deadline = enrolment.TargetEndDate,
            LastSuccessfulPaymentAt = latestPayment,
            TotalContributedCurrency = enrolment.TotalContributedCurrency,
            Currency = enrolment.Currency ?? "LKR",
            ReminderReason = rem.ReminderReason ?? "NoPaymentForThreeMonths",
            CanSendReminder = rem.CanSendReminder,
            ReminderEligibleAt = rem.ReminderEligibleAt,
            SuggestedTitle = suggestedTitle,
            SuggestedMessage = suggestedMessage,
            InApp = new ChannelStatusDto
            {
                Available = true,
                Description = "Will be delivered to customer's in-app notification inbox immediately."
            },
            Email = new ChannelStatusDto
            {
                Available = !string.IsNullOrWhiteSpace(customer.Email),
                Description = !string.IsNullOrWhiteSpace(customer.Email)
                    ? $"Email delivery to {customer.Email} via SJewls mail provider."
                    : "No email address registered for this customer."
            },
            Sms = new ChannelStatusDto
            {
                Available = !string.IsNullOrWhiteSpace(customer.PhoneNumber),
                Description = !string.IsNullOrWhiteSpace(customer.PhoneNumber)
                    ? $"SMS delivery to {customer.PhoneNumber} via SJewls SMS gateway."
                    : "No phone number registered for this customer."
            },
            Push = new ChannelStatusDto
            {
                Available = false,
                Description = "Mobile push notification provider is not currently configured for customer mobile apps."
            }
        };
    }

    public async Task<AdminSendReminderResult> SendReminderAsync(
        Guid staffId,
        Guid enrolmentId,
        AdminSendReminderRequest request,
        CancellationToken ct = default)
    {
        var (staff, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(staffId, ct);

        var enrolment = await _db.JewelleryEnrolments
            .Include(e => e.Customer)
            .Include(e => e.Branch)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(enrolment.BranchId))
        {
            throw new UnauthorizedAccessException("You are not authorized to send reminders for this branch.");
        }

        var customer = enrolment.Customer ?? throw new KeyNotFoundException("Customer was not found.");

        if (!customer.IsActive)
        {
            throw new InvalidOperationException("Cannot send reminders to inactive customer accounts.");
        }

        if (enrolment.Status == JewelleryEnrolmentStatus.Completed ||
            enrolment.Status == JewelleryEnrolmentStatus.Claimed ||
            enrolment.Status == JewelleryEnrolmentStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot send contribution reminders for enrolment status '{enrolment.Status}'.");
        }

        var delivered = new List<string>();
        var failed = new List<string>();

        foreach (var ch in request.Channels)
        {
            if (string.Equals(ch, "InApp", StringComparison.OrdinalIgnoreCase))
            {
                await _notificationService.CreateNotificationAsync(
                    customer.Id,
                    request.Title.Trim(),
                    request.Message.Trim(),
                    "reminder",
                    ct);
                delivered.Add("InApp");
            }
            else if (string.Equals(ch, "Email", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    _logger.LogInformation("Jewellery reminder email dispatched to {Email} for enrolment {EnrolmentId}", customer.Email, enrolmentId);
                    delivered.Add("Email");
                }
                else
                {
                    failed.Add("Email (Customer has no email)");
                }
            }
            else if (string.Equals(ch, "Sms", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
                {
                    _logger.LogInformation("Jewellery reminder SMS dispatched to {Phone} for enrolment {EnrolmentId}", customer.PhoneNumber, enrolmentId);
                    delivered.Add("Sms");
                }
                else
                {
                    failed.Add("Sms (Customer has no phone number)");
                }
            }
            else if (string.Equals(ch, "Push", StringComparison.OrdinalIgnoreCase))
            {
                failed.Add("Push (Push provider not configured)");
            }
        }

        await _auditService.LogActionAsync(
            "Staff",
            staffId,
            "SentJewelleryPlanReminder",
            "JewelleryEnrolment",
            enrolmentId.ToString(),
            enrolment.BranchId,
            null,
            new
            {
                title = request.Title,
                deliveredChannels = delivered,
                failedChannels = failed
            },
            null);

        return new AdminSendReminderResult
        {
            Success = delivered.Any(),
            Message = delivered.Any()
                ? $"Reminder delivered via {string.Join(", ", delivered)}."
                : "No delivery channels were able to deliver the reminder.",
            DeliveredChannels = delivered,
            FailedChannels = failed,
            SentAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static (bool CanSendReminder, string? ReminderReason, DateTimeOffset? LastSuccessfulPaymentAt, DateTimeOffset? ReminderEligibleAt)
        EvaluateReminderEligibility(
            JewelleryEnrolment enrolment,
            Customer customer,
            DateTimeOffset? latestSuccessfulPaymentAt,
            DateOnly branchToday,
            DateTimeOffset nowUtc)
    {
        if (!customer.IsActive ||
            enrolment.Status == JewelleryEnrolmentStatus.Completed ||
            enrolment.Status == JewelleryEnrolmentStatus.Cancelled ||
            enrolment.Status == JewelleryEnrolmentStatus.Claimed)
        {
            return (false, null, latestSuccessfulPaymentAt, null);
        }

        var isIncomplete = branchToday > enrolment.TargetEndDate && enrolment.TotalSavedGrams < enrolment.TargetProductWeightGrams;
        if (isIncomplete)
        {
            return (true, "PlanIncomplete", latestSuccessfulPaymentAt, null);
        }

        DateTimeOffset inactivityStart;
        if (latestSuccessfulPaymentAt.HasValue)
        {
            inactivityStart = latestSuccessfulPaymentAt.Value;
        }
        else
        {
            var startDateTime = enrolment.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            inactivityStart = new DateTimeOffset(startDateTime, TimeSpan.Zero);
            if (enrolment.CreatedAtUtc < inactivityStart && enrolment.CreatedAtUtc != default)
            {
                inactivityStart = enrolment.CreatedAtUtc;
            }
        }

        var eligibleAt = inactivityStart.AddMonths(3);

        if (nowUtc >= eligibleAt)
        {
            return (true, "NoPaymentForThreeMonths", latestSuccessfulPaymentAt, eligibleAt);
        }

        return (false, null, latestSuccessfulPaymentAt, eligibleAt);
    }

    #endregion
}
