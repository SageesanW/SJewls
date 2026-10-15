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
            return await executionStrategy.ExecuteAsync(async () =>
            {
                var isRelational = _db.Database.IsRelational();
                await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
                try
                {
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

                        // Trigger completion notification once
                        await _notificationService.CreateNotificationAsync(
                            enrolment.CustomerId,
                            "Jewellery Plan Completed!",
                            $"Congratulations! You have reached your gold target of {enrolment.TargetProductWeightGrams}g for {enrolment.PlanNameSnapshot}.",
                            "completion",
                            ct);

                        _logger.LogInformation("Enrolment {EnrolmentId} reached target {Target}g and marked Completed",
                            enrolment.Id, enrolment.TargetProductWeightGrams);
                    }

                    await _db.SaveChangesAsync(ct);
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
}
