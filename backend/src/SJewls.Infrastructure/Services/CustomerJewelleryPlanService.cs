using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class CustomerJewelleryPlanService : ICustomerJewelleryPlanService
{
    private readonly SJewlsDbContext _db;
    private readonly IGoldRateService _goldRateService;
    private readonly ICustomerNotificationService _notificationService;
    private readonly IConfiguration _config;
    private readonly ILogger<CustomerJewelleryPlanService> _logger;

    public CustomerJewelleryPlanService(
        SJewlsDbContext db,
        IGoldRateService goldRateService,
        ICustomerNotificationService notificationService,
        IConfiguration config,
        ILogger<CustomerJewelleryPlanService> logger)
    {
        _db = db;
        _goldRateService = goldRateService;
        _notificationService = notificationService;
        _config = config;
        _logger = logger;
    }

    #region Browsing

    public async Task<List<CustomerJewelleryCategoryDto>> GetCategoriesAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var categories = await _db.JewelleryCategories
            .Include(c => c.JewelleryPlans)
            .Where(c => c.BranchId == customer.PrimaryBranchId && c.IsActive)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return categories.Select(c =>
        {
            var availablePlansCount = c.JewelleryPlans.Count(p =>
                p.IsActive &&
                p.BranchId == customer.PrimaryBranchId &&
                p.StartDate <= branchToday);

            return new CustomerJewelleryCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                AvailablePlansCount = availablePlansCount
            };
        }).ToList();
    }

    public async Task<List<CustomerJewelleryPlanDto>> GetPlansAsync(Guid customerId, Guid? categoryId = null, CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var query = _db.JewelleryPlans
            .Include(p => p.Category)
            .Where(p =>
                p.BranchId == customer.PrimaryBranchId &&
                p.IsActive &&
                p.StartDate <= branchToday &&
                (p.Category == null || p.Category.IsActive));

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var plans = await query
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return plans.Select(p => new CustomerJewelleryPlanDto
        {
            Id = p.Id,
            Name = p.Name,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            ImageUrl = p.ImageUrl,
            TargetGrams = p.TargetProductGoldWeightGrams,
            OfferedDurations = p.AllowedDurationsMonths,
            PremiumPercentage = p.PremiumPercentage,
            StartDate = p.StartDate,
            Description = p.Description
        }).ToList();
    }

    public Task<List<CustomerJewelleryPlanDto>> GetPlansByCategoryAsync(Guid customerId, Guid categoryId, CancellationToken ct = default)
    {
        return GetPlansAsync(customerId, categoryId, ct);
    }

    public async Task<CustomerJewelleryPlanDto> GetPlanDetailsAsync(Guid customerId, Guid planId, CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.JewelleryPlans
            .Include(p => p.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan '{planId}' was not found.");

        if (plan.BranchId != customer.PrimaryBranchId)
        {
            throw new UnauthorizedAccessException("This jewellery plan belongs to a different branch.");
        }

        if (!plan.IsActive || (plan.Category != null && !plan.Category.IsActive) || plan.StartDate > branchToday)
        {
            throw new InvalidOperationException("This jewellery plan is currently unavailable for enrolment.");
        }

        return new CustomerJewelleryPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            CategoryId = plan.CategoryId,
            CategoryName = plan.Category?.Name ?? string.Empty,
            ImageUrl = plan.ImageUrl,
            TargetGrams = plan.TargetProductGoldWeightGrams,
            OfferedDurations = plan.AllowedDurationsMonths,
            PremiumPercentage = plan.PremiumPercentage,
            StartDate = plan.StartDate,
            Description = plan.Description
        };
    }

    #endregion

    #region Enrolment Confirmation & Creation

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _enrolmentLocks = new();

    public async Task<EnrolmentPreviewResponse> PreviewEnrolmentAsync(
        Guid customerId,
        EnrolmentPreviewRequest request,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.JewelleryPlans
            .Include(p => p.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan '{request.PlanId}' was not found.");

        if (plan.BranchId != customer.PrimaryBranchId)
        {
            throw new UnauthorizedAccessException("This jewellery plan is not available for your registered branch.");
        }

        if (!plan.IsActive || (plan.Category != null && !plan.Category.IsActive))
        {
            throw new InvalidOperationException("This plan or its category has been deactivated.");
        }

        if (plan.StartDate > branchToday)
        {
            throw new InvalidOperationException($"This plan is scheduled to start on {plan.StartDate} and is not yet open for enrolments.");
        }

        if (!plan.AllowedDurationsMonths.Contains(request.SelectedDurationMonths))
        {
            throw new ArgumentException($"The selected duration of {request.SelectedDurationMonths} months is not offered by this plan. Offered durations: {string.Join(", ", plan.AllowedDurationsMonths)} months.");
        }

        var proposedJoiningDate = branchToday;
        var proposedDeadline = proposedJoiningDate.AddMonths(request.SelectedDurationMonths);

        // Query historical and active enrolments for this customer and requested plan
        var existingEnrolments = _db.JewelleryEnrolments
            .AsNoTracking()
            .Where(e => e.CustomerId == customerId && e.JewelleryPlanId == plan.Id);

        var totalEnrolmentCount = await existingEnrolments.CountAsync(ct);
        var activeEnrolmentCount = await existingEnrolments.CountAsync(e => e.Status == JewelleryEnrolmentStatus.Active, ct);
        var hasPreviouslyJoined = totalEnrolmentCount > 0;

        return new EnrolmentPreviewResponse
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            CategoryId = plan.CategoryId,
            CategoryName = plan.Category?.Name ?? string.Empty,
            ImageUrl = plan.ImageUrl,
            TargetGrams = plan.TargetProductGoldWeightGrams,
            SelectedDurationMonths = request.SelectedDurationMonths,
            PremiumPercentage = plan.PremiumPercentage,
            ProposedJoiningDate = proposedJoiningDate,
            ProposedDeadline = proposedDeadline,
            Currency = "LKR",
            HasPreviouslyJoined = hasPreviouslyJoined,
            TotalEnrolmentCount = totalEnrolmentCount,
            ActiveEnrolmentCount = activeEnrolmentCount,
            RequiresRepeatJoinConfirmation = hasPreviouslyJoined
        };
    }

    public async Task<CustomerEnrolmentDto> CreateEnrolmentAsync(
        Guid customerId,
        CreateCustomerEnrolmentRequest request,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        // 1. Idempotency check: Resolve valid retry of an already successful request
        // BEFORE treating its resulting enrolment as a previous enrolment requiring confirmation.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var trimmedKey = request.IdempotencyKey.Trim();
            var existingEnrolment = await _db.JewelleryEnrolments
                .Include(e => e.Customer)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.CustomerId == customerId && e.IdempotencyKey == trimmedKey, ct);

            if (existingEnrolment != null)
            {
                // Follow existing payload-conflict handling when a key is reused with different request contents.
                if (existingEnrolment.JewelleryPlanId != request.PlanId ||
                    existingEnrolment.SelectedDurationMonths != request.SelectedDurationMonths)
                {
                    throw new InvalidOperationException("The idempotency key has already been used with a different enrolment request payload.");
                }

                _logger.LogInformation("Idempotent enrolment request deduplicated for customer {CustomerId}, key {Key}",
                    customerId, request.IdempotencyKey);
                return MapToEnrolmentDto(existingEnrolment, customer, branchToday);
            }
        }

        // 2. Revalidate plan availability & eligibility
        var plan = await _db.JewelleryPlans
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new KeyNotFoundException($"Jewellery plan '{request.PlanId}' was not found.");

        if (plan.BranchId != customer.PrimaryBranchId)
        {
            throw new UnauthorizedAccessException("This jewellery plan belongs to a different branch.");
        }

        if (!plan.IsActive || (plan.Category != null && !plan.Category.IsActive))
        {
            throw new InvalidOperationException("This jewellery plan is no longer active for new enrolments.");
        }

        if (plan.StartDate > branchToday)
        {
            throw new InvalidOperationException($"This jewellery plan is scheduled to start on {plan.StartDate} and is not yet open for enrolments.");
        }

        if (!plan.AllowedDurationsMonths.Contains(request.SelectedDurationMonths))
        {
            throw new ArgumentException($"The selected duration of {request.SelectedDurationMonths} months is not offered by this plan.");
        }

        // 3. Concurrency protection per customer and plan:
        // Ensures two simultaneous first-join requests cannot silently bypass repeat-enrolment confirmation.
        var lockKey = $"{customerId:N}_{plan.Id:N}";
        var semaphore = _enrolmentLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            // Re-check idempotency key under lock in case a concurrent request with the same key just completed
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var trimmedKey = request.IdempotencyKey.Trim();
                var existingEnrolment = await _db.JewelleryEnrolments
                    .Include(e => e.Customer)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.CustomerId == customerId && e.IdempotencyKey == trimmedKey, ct);

                if (existingEnrolment != null)
                {
                    if (existingEnrolment.JewelleryPlanId != request.PlanId ||
                        existingEnrolment.SelectedDurationMonths != request.SelectedDurationMonths)
                    {
                        throw new InvalidOperationException("The idempotency key has already been used with a different enrolment request payload.");
                    }

                    return MapToEnrolmentDto(existingEnrolment, customer, branchToday);
                }
            }

            // 4. Repeat enrolment verification against database:
            // Check all historical enrolments (active, completed, closed, etc.) and active count separately
            var existingEnrolmentsQuery = _db.JewelleryEnrolments
                .Where(e => e.CustomerId == customerId && e.JewelleryPlanId == plan.Id);

            var totalEnrolments = await existingEnrolmentsQuery.CountAsync(ct);
            var activeEnrolments = await existingEnrolmentsQuery.CountAsync(e => e.Status == JewelleryEnrolmentStatus.Active, ct);

            if (totalEnrolments > 0 && !request.ConfirmRepeatEnrolment)
            {
                throw new RepeatEnrolmentConfirmationRequiredException(totalEnrolments, activeEnrolments);
            }

            var joiningDate = branchToday;
            var deadline = joiningDate.AddMonths(request.SelectedDurationMonths);

            var enrolment = new JewelleryEnrolment
            {
                CustomerId = customerId,
                JewelleryPlanId = plan.Id,
                BranchId = customer.PrimaryBranchId,
                IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
                Currency = "LKR",
                PlanNameSnapshot = plan.Name,
                CategoryNameSnapshot = plan.Category?.Name ?? string.Empty,
                ImageUrlSnapshot = plan.ImageUrl,
                TargetProductWeightGrams = plan.TargetProductGoldWeightGrams,
                SelectedDurationMonths = request.SelectedDurationMonths,
                PremiumPercentage = plan.PremiumPercentage,
                StartDate = joiningDate,
                TargetEndDate = deadline,
                TotalSavedGrams = 0m,
                TotalContributedCurrency = 0m,
                Status = JewelleryEnrolmentStatus.Active
            };

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
                customerId,
                "Jewellery Plan Activated",
                $"Your enrolment in {plan.Name} ({plan.TargetProductGoldWeightGrams}g) for {request.SelectedDurationMonths} months has been successfully activated.",
                "enrolment",
                ct);

            _logger.LogInformation("Customer {CustomerId} created enrolment {EnrolmentId} for plan {PlanId}",
                customerId, enrolment.Id, plan.Id);

            return MapToEnrolmentDto(enrolment, customer, branchToday);
        }
        finally
        {
            semaphore.Release();
        }
    }

    #endregion

    #region My Investments

    public async Task<CustomerEnrolmentsPagedResponse> GetEnrolmentsAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var query = _db.JewelleryEnrolments
            .Include(e => e.Contributions)
                .ThenInclude(c => c.Payment)
            .Where(e => e.CustomerId == customerId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(e => MapToEnrolmentDto(e, customer, branchToday)).ToList();

        return new CustomerEnrolmentsPagedResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerEnrolmentDto> GetEnrolmentDetailsAsync(
        Guid customerId,
        Guid enrolmentId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var enrolment = await _db.JewelleryEnrolments
            .Include(e => e.Contributions)
                .ThenInclude(c => c.Payment)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' was not found.");

        if (enrolment.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this enrolment.");
        }

        return MapToEnrolmentDto(enrolment, customer, branchToday);
    }

    public async Task<List<CustomerEnrolmentPaymentDto>> GetEnrolmentPaymentsAsync(
        Guid customerId,
        Guid enrolmentId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);

        var enrolment = await _db.JewelleryEnrolments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' was not found.");

        if (enrolment.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view payments for this enrolment.");
        }

        var contributions = await _db.JewelleryContributions
            .Include(c => c.Payment)
            .Where(c => c.JewelleryEnrolmentId == enrolmentId)
            .OrderByDescending(c => c.ConfirmedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        return contributions.Select(c => new CustomerEnrolmentPaymentDto
        {
            PaymentId = c.PaymentId,
            ContributionId = c.Id,
            CurrencyAmount = c.CurrencyAmount,
            Currency = "LKR",
            GoldGramsCredited = c.GoldGrams,
            AppliedRatePerGram = c.AppliedRatePerGram,
            ConfirmedAtUtc = c.ConfirmedAtUtc,
            Status = c.Payment?.Status.ToString() ?? "Succeeded",
            PaymentMethod = c.Payment?.Method.ToString() ?? "Online",
            PaymentReference = c.Payment?.ProviderReference ?? string.Empty
        }).ToList();
    }

    #endregion

    #region Calculation Preview & Locked Quotes

    public async Task<JewelleryCalculationPreviewResponse> PreviewCalculationAsync(
        Guid customerId,
        Guid enrolmentId,
        JewelleryCalculationRequest request,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var enrolment = await _db.JewelleryEnrolments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' was not found.");

        if (enrolment.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You do not own this enrolment.");
        }

        ValidateEnrolmentForContribution(enrolment, customer, branchToday);

        var remainingTargetGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
        var applicableRate = await _goldRateService.GetApplicable24kRateAsync(customer.PrimaryBranchId, ct);

        var isBuyGram = string.Equals(request.InputMode, "BuyGram", StringComparison.OrdinalIgnoreCase);
        var isBuyAmount = string.Equals(request.InputMode, "BuyAmount", StringComparison.OrdinalIgnoreCase);

        if (!isBuyGram && !isBuyAmount)
        {
            throw new ArgumentException("InputMode must be either 'BuyGram' or 'BuyAmount'.");
        }

        JewelleryCalculationResult calcResult;
        decimal? maxPayableForTarget = null;

        if (isBuyGram)
        {
            if (!request.RequestedGrams.HasValue || request.RequestedGrams.Value <= 0)
            {
                throw new ArgumentException("RequestedGrams must be greater than zero for BuyGram mode.");
            }

            if (request.RequestedGrams.Value > remainingTargetGrams)
            {
                throw new ArgumentException($"Requested gold grams ({request.RequestedGrams.Value}g) exceeds the remaining target of {remainingTargetGrams}g.");
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

            maxPayableForTarget = JewelleryPaymentCalculator.CalculateMaxAmountForRemainingGrams(
                remainingTargetGrams,
                applicableRate.RatePerGram,
                enrolment.PremiumPercentage);

            if (calcResult.TotalGoldCredit > remainingTargetGrams)
            {
                throw new ArgumentException(
                    $"Entered amount of LKR {request.EnteredAmount.Value:N2} would yield {calcResult.TotalGoldCredit}g credit, " +
                    $"which exceeds the remaining target of {remainingTargetGrams}g. " +
                    $"The maximum payable amount to complete this plan is LKR {maxPayableForTarget:N2}.");
            }
        }

        return new JewelleryCalculationPreviewResponse
        {
            InputMode = calcResult.InputMode.ToString(),
            EnteredAmount = isBuyAmount ? calcResult.AmountToPay : null,
            RequestedGrams = isBuyGram ? calcResult.RequestedGrams : null,
            Currency = "LKR",
            RatePerGram = calcResult.RatePerGram,
            PremiumPercentage = calcResult.PremiumPercentage,
            RateAsOf = calcResult.RateAsOf,
            RateSource = applicableRate.Source,
            BaseGoldGrams = calcResult.BaseGoldGrams,
            PremiumGrams = calcResult.PremiumGrams,
            ChargeableGoldGrams = calcResult.ChargeableGoldGrams,
            TotalGoldCredit = calcResult.TotalGoldCredit,
            BaseAmount = calcResult.BaseAmount,
            PremiumAmount = calcResult.PremiumAmount,
            AmountToPay = calcResult.AmountToPay,
            RemainingGramsAfterPayment = calcResult.RemainingGramsAfterPayment,
            RemainingTargetGrams = remainingTargetGrams,
            WillExceedTarget = calcResult.WillExceedTarget,
            MaximumPayableAmountForTarget = maxPayableForTarget
        };
    }

    public async Task<CustomerPaymentQuoteDto> CreateLockedQuoteAsync(
        Guid customerId,
        Guid enrolmentId,
        JewelleryCalculationRequest request,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var enrolment = await _db.JewelleryEnrolments
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct)
            ?? throw new KeyNotFoundException($"Enrolment '{enrolmentId}' was not found.");

        if (enrolment.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You do not own this enrolment.");
        }

        ValidateEnrolmentForContribution(enrolment, customer, branchToday);

        var remainingTargetGrams = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
        var applicableRate = await _goldRateService.GetApplicable24kRateAsync(customer.PrimaryBranchId, ct);

        var isBuyGram = string.Equals(request.InputMode, "BuyGram", StringComparison.OrdinalIgnoreCase);
        var isBuyAmount = string.Equals(request.InputMode, "BuyAmount", StringComparison.OrdinalIgnoreCase);

        if (!isBuyGram && !isBuyAmount)
        {
            throw new ArgumentException("InputMode must be either 'BuyGram' or 'BuyAmount'.");
        }

        JewelleryCalculationResult calc;

        if (isBuyGram)
        {
            if (!request.RequestedGrams.HasValue || request.RequestedGrams.Value <= 0)
            {
                throw new ArgumentException("RequestedGrams must be greater than zero.");
            }

            if (request.RequestedGrams.Value > remainingTargetGrams)
            {
                throw new ArgumentException($"Requested gold grams ({request.RequestedGrams.Value}g) exceeds the remaining target of {remainingTargetGrams}g.");
            }

            calc = JewelleryPaymentCalculator.CalculateBuyGram(
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
                throw new ArgumentException("EnteredAmount must be greater than zero.");
            }

            calc = JewelleryPaymentCalculator.CalculateBuyAmount(
                request.EnteredAmount.Value,
                applicableRate.RatePerGram,
                enrolment.PremiumPercentage,
                remainingTargetGrams,
                applicableRate.SourceTimestampUtc);

            if (calc.TotalGoldCredit > remainingTargetGrams)
            {
                var maxPayable = JewelleryPaymentCalculator.CalculateMaxAmountForRemainingGrams(
                    remainingTargetGrams,
                    applicableRate.RatePerGram,
                    enrolment.PremiumPercentage);

                throw new ArgumentException(
                    $"Entered amount of LKR {request.EnteredAmount.Value:N2} would yield {calc.TotalGoldCredit}g credit, " +
                    $"which exceeds remaining target {remainingTargetGrams}g. Max payable is LKR {maxPayable:N2}.");
            }
        }

        var quoteLifetimeMinutes = int.TryParse(_config["JewelleryPlans:QuoteLifetimeMinutes"], out var qm) ? qm : 5;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(quoteLifetimeMinutes);

        var quote = new JewelleryPaymentQuote
        {
            CustomerId = customerId,
            JewelleryEnrolmentId = enrolmentId,
            InputMode = calc.InputMode,
            EnteredValue = isBuyGram ? calc.RequestedGrams : calc.EnteredAmount,
            Currency = "LKR",
            RatePerGram = calc.RatePerGram,
            RateSource = applicableRate.Source,
            RateAsOf = calc.RateAsOf,
            PremiumPercentage = calc.PremiumPercentage,
            BaseGoldGrams = calc.BaseGoldGrams,
            PremiumGrams = calc.PremiumGrams,
            ChargeableGoldGrams = calc.ChargeableGoldGrams,
            TotalGoldCredit = calc.TotalGoldCredit,
            BaseAmount = calc.BaseAmount,
            PremiumAmount = calc.PremiumAmount,
            AmountToPay = calc.AmountToPay,
            RemainingGramsAfterPayment = calc.RemainingGramsAfterPayment,
            ExpiresAtUtc = expiresAt,
            Status = JewelleryQuoteStatus.Active
        };

        _db.JewelleryPaymentQuotes.Add(quote);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Locked payment quote {QuoteId} created for enrolment {EnrolmentId}: mode={Mode}, pay=LKR {Pay}, credit={Credit}g",
            quote.Id, enrolmentId, quote.InputMode, quote.AmountToPay, quote.TotalGoldCredit);

        return MapToQuoteDto(quote);
    }

    public async Task<CustomerPaymentQuoteDto> GetQuoteAsync(
        Guid customerId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var quote = await _db.JewelleryPaymentQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct)
            ?? throw new KeyNotFoundException($"Payment quote '{quoteId}' was not found.");

        if (quote.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this quote.");
        }

        return MapToQuoteDto(quote);
    }

    public async Task<PaymentInitiationResponse> InitiatePaymentAsync(
        Guid customerId,
        Guid quoteId,
        CancellationToken ct = default)
    {
        var quote = await _db.JewelleryPaymentQuotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct)
            ?? throw new KeyNotFoundException($"Payment quote '{quoteId}' was not found.");

        if (quote.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You are not authorized to pay for this quote.");
        }

        if (quote.Status != JewelleryQuoteStatus.Active)
        {
            throw new InvalidOperationException($"Quote '{quoteId}' cannot be paid because its status is {quote.Status}.");
        }

        if (DateTimeOffset.UtcNow > quote.ExpiresAtUtc)
        {
            throw new InvalidOperationException($"Quote '{quoteId}' expired at {quote.ExpiresAtUtc:u}. Please request a new quote.");
        }

        // Payment gateway integration is deferred. No actual gateway or mock simulation allowed.
        _logger.LogInformation("Payment initiation requested for quote {QuoteId}. Gateway deferred (PaymentProviderNotConfigured).", quoteId);

        return new PaymentInitiationResponse
        {
            Code = "PaymentProviderNotConfigured",
            Message = "Payment gateway integration is currently deferred. No online payment provider is configured.",
            QuoteId = quote.Id,
            AmountToPay = quote.AmountToPay,
            Currency = quote.Currency,
            IsConfigured = false
        };
    }

    #endregion

    #region Helpers

    private async Task<Customer> GetActiveCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await _db.Customers
            .Include(c => c.PrimaryBranch)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new UnauthorizedAccessException("Customer record not found.");

        if (!customer.IsActive)
        {
            throw new InvalidOperationException("Customer account is deactivated. Enrolments and transactions are prohibited.");
        }

        return customer;
    }

    private static void ValidateEnrolmentForContribution(JewelleryEnrolment enrolment, Customer customer, DateOnly branchToday)
    {
        if (enrolment.Status == JewelleryEnrolmentStatus.Completed || enrolment.TotalSavedGrams >= enrolment.TargetProductWeightGrams)
        {
            throw new InvalidOperationException("This enrolment has reached its target gold weight and is completed.");
        }

        if (enrolment.Status != JewelleryEnrolmentStatus.Active)
        {
            throw new InvalidOperationException($"Contributions are not permitted for enrolment status '{enrolment.Status}'.");
        }

        if (branchToday > enrolment.TargetEndDate)
        {
            throw new InvalidOperationException($"Enrolment duration ended on {enrolment.TargetEndDate}. Further contributions are closed awaiting settlement.");
        }
    }

    private static CustomerEnrolmentDto MapToEnrolmentDto(
        JewelleryEnrolment e,
        Customer customer,
        DateOnly branchToday)
    {
        var targetGrams = e.TargetProductWeightGrams;
        var confirmedContribs = e.Contributions != null
            ? e.Contributions.Where(c => c.Payment == null || c.Payment.Status == PaymentStatus.Succeeded).ToList()
            : new List<JewelleryContribution>();

        var netGrams = (e.Contributions != null && e.Contributions.Any())
            ? confirmedContribs.Sum(c => c.GoldGrams)
            : e.TotalSavedGrams;

        var confirmedMoney = (e.Contributions != null && e.Contributions.Any())
            ? confirmedContribs.GroupBy(c => c.PaymentId).Select(g => g.First().CurrencyAmount).Sum()
            : e.TotalContributedCurrency;

        var remaining = Math.Max(0m, targetGrams - netGrams);
        var progress = targetGrams > 0 ? Math.Round((netGrams / targetGrams) * 100m, 2) : 0m;
        if (progress > 100m) progress = 100m;

        var isAllowed = customer.IsActive &&
                        e.Status == JewelleryEnrolmentStatus.Active &&
                        branchToday <= e.TargetEndDate &&
                        remaining > 0m;

        string? blockedReason = null;
        if (!isAllowed)
        {
            if (!customer.IsActive) blockedReason = "Customer account is inactive.";
            else if (e.Status == JewelleryEnrolmentStatus.Completed || remaining <= 0m) blockedReason = "Target gold weight reached.";
            else if (branchToday > e.TargetEndDate) blockedReason = "Enrolment duration has ended.";
            else if (e.Status == JewelleryEnrolmentStatus.Cancelled) blockedReason = "Enrolment is cancelled.";
            else if (e.Status == JewelleryEnrolmentStatus.Claimed) blockedReason = "Gold has already been claimed.";
            else blockedReason = $"Contributions unavailable for status '{e.Status}'.";
        }

        return new CustomerEnrolmentDto
        {
            EnrolmentId = e.Id,
            PlanId = e.JewelleryPlanId,
            PlanName = !string.IsNullOrEmpty(e.PlanNameSnapshot) ? e.PlanNameSnapshot : (e.JewelleryPlan?.Name ?? string.Empty),
            CategoryName = !string.IsNullOrEmpty(e.CategoryNameSnapshot) ? e.CategoryNameSnapshot : (e.JewelleryPlan?.Category?.Name ?? string.Empty),
            ImageUrl = e.ImageUrlSnapshot ?? e.JewelleryPlan?.ImageUrl,
            TargetGrams = targetGrams,
            SelectedDurationMonths = e.SelectedDurationMonths,
            JoiningDate = e.StartDate,
            Deadline = e.TargetEndDate,
            PremiumPercentage = e.PremiumPercentage,
            Currency = e.Currency,
            TotalConfirmedMoneyPaid = confirmedMoney,
            NetAccumulatedGoldGrams = netGrams,
            RemainingGrams = remaining,
            IndividualProgressPercentage = progress,
            EnrolmentStatus = e.Status.ToString(),
            IsContributionAllowed = isAllowed,
            ContributionBlockedReason = blockedReason,
            CreatedAtUtc = e.CreatedAtUtc
        };
    }

    private static CustomerPaymentQuoteDto MapToQuoteDto(JewelleryPaymentQuote q)
    {
        var isExpired = DateTimeOffset.UtcNow > q.ExpiresAtUtc;
        var displayStatus = isExpired && q.Status == JewelleryQuoteStatus.Active ? "Expired" : q.Status.ToString();

        return new CustomerPaymentQuoteDto
        {
            QuoteId = q.Id,
            EnrolmentId = q.JewelleryEnrolmentId,
            InputMode = q.InputMode.ToString(),
            EnteredAmount = q.InputMode == JewelleryQuoteInputMode.BuyAmount ? q.EnteredValue : null,
            RequestedGrams = q.InputMode == JewelleryQuoteInputMode.BuyGram ? q.EnteredValue : null,
            Currency = q.Currency,
            RatePerGram = q.RatePerGram,
            PremiumPercentage = q.PremiumPercentage,
            RateAsOf = q.RateAsOf,
            RateSource = q.RateSource,
            BaseGoldGrams = q.BaseGoldGrams,
            PremiumGrams = q.PremiumGrams,
            ChargeableGoldGrams = q.ChargeableGoldGrams,
            TotalGoldCredit = q.TotalGoldCredit,
            BaseAmount = q.BaseAmount,
            PremiumAmount = q.PremiumAmount,
            AmountToPay = q.AmountToPay,
            RemainingGramsAfterPayment = q.RemainingGramsAfterPayment,
            CreatedAtUtc = q.CreatedAtUtc,
            ExpiresAtUtc = q.ExpiresAtUtc,
            Status = displayStatus,
            IsExpired = isExpired
        };
    }

    private static DateOnly GetBranchToday(string? timezoneStr)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneStr ?? "Asia/Colombo");
        }
        catch
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
            }
            catch
            {
                tz = TimeZoneInfo.Utc;
            }
        }

        var branchNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
        return DateOnly.FromDateTime(branchNow.DateTime);
    }

    #endregion
}
