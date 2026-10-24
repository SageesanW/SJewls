using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SJewls.Application.Common;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class CustomerChituService : ICustomerChituService
{
    private readonly SJewlsDbContext _db;
    private readonly ICustomerNotificationService _notificationService;
    private readonly IAuditLogService _auditService;
    private readonly ChituPaymentOptions _paymentOptions;
    private readonly ILogger<CustomerChituService> _logger;

    // In-memory cache for join and payment previews: Key -> PreviewCacheEntry
    private static readonly ConcurrentDictionary<string, PreviewCacheEntry> _previewCache = new();

    private record PreviewCacheEntry(
        string Reference,
        Guid CustomerId,
        Guid PlanId,
        decimal ExpectedTotalAmount,
        DateTimeOffset ExpiresAtUtc,
        string PayloadJson);

    public CustomerChituService(
        SJewlsDbContext db,
        ICustomerNotificationService notificationService,
        IAuditLogService auditService,
        IOptions<ChituPaymentOptions> paymentOptions,
        ILogger<CustomerChituService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _auditService = auditService;
        _paymentOptions = paymentOptions.Value;
        _logger = logger;
    }

    #region Active Chitu Discovery

    public async Task<CustomerChituPlansPagedResponse> GetAvailablePlansAsync(
        Guid customerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var query = _db.ChituPlans
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.Slots)
            .Include(p => p.Draws)
            .Where(p => p.BranchId == customer.PrimaryBranchId && p.Status == PlanStatus.Active);

        var allActivePlans = await query.ToListAsync(ct);

        // Filter according to business rules:
        // 1. Published upcoming plans accepting pre-enrolment (branchToday < StartDate)
        // 2. Running plans accepting late joins (branchToday >= StartDate && branchToday <= EndDate)
        // 3. Exclude full plans (IssuedSlots >= TotalSlotCapacity)
        // 4. Exclude ended plans (branchToday > EndDate)
        // 5. Exclude plans where all raffle draws have already occurred (Draws.Count >= DurationMonths)
        var availablePlans = allActivePlans
            .Where(p =>
            {
                var isUpcoming = branchToday < p.StartDate;
                var isRunning = branchToday >= p.StartDate && branchToday <= p.EndDate;
                var hasCapacity = p.Slots.Count < p.TotalSlotCapacity;
                var hasRemainingDraws = p.Draws.Count < p.DurationMonths;

                return (isUpcoming || isRunning) && hasCapacity && hasRemainingDraws;
            })
            .OrderBy(p => p.StartDate)
            .ThenBy(p => p.Name)
            .ToList();

        var totalCount = availablePlans.Count;
        var pagedItems = availablePlans
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => MapToSummaryDto(p, customerId, branchToday))
            .ToList();

        return new CustomerChituPlansPagedResponse
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerChituPlanDetailDto> GetPlanDetailsAsync(
        Guid customerId,
        Guid planId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.Slots)
            .Include(p => p.Draws)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        if (plan.BranchId != customer.PrimaryBranchId)
        {
            throw new UnauthorizedAccessException("This Chitu plan is not available for your registered branch.");
        }

        var summary = MapToSummaryDto(plan, customerId, branchToday);

        // Construct round-by-round schedule
        var schedule = new List<CustomerChituRoundScheduleDto>();
        var recordedDrawMonths = new HashSet<int>(plan.Draws.Select(d => d.DrawMonthIndex));

        for (int r = 1; r <= plan.DurationMonths; r++)
        {
            var roundDate = plan.StartDate.AddMonths(r - 1);
            var hasRecordedDraw = recordedDrawMonths.Contains(r);
            string status;
            if (hasRecordedDraw || (branchToday >= roundDate && r < summary.CurrentRoundIndex))
            {
                status = "Completed";
            }
            else if (r == summary.CurrentRoundIndex)
            {
                status = "Current";
            }
            else
            {
                status = "Upcoming";
            }

            schedule.Add(new CustomerChituRoundScheduleDto
            {
                RoundIndex = r,
                ScheduledDate = roundDate,
                Status = status,
                HasRecordedDraw = hasRecordedDraw
            });
        }

        var terms = new List<string>
        {
            $"Monthly instalment price: LKR {plan.MonthlyInstalmentAmount:N2} per slot.",
            $"Fixed monthly raffle schedule: Day {plan.DueDayOfMonth} of each month.",
            $"Winning entitlement per slot: LKR {summary.WinningEntitlementPerSlot:N2} (Monthly slot price × {plan.DurationMonths} months).",
            "To be eligible for each monthly raffle draw, all past and current instalments through that round must be fully paid.",
            "Once a slot wins a raffle draw, all its future instalment obligations cease. Other slots remain active and independent.",
            plan.RedemptionType == ChituRedemptionType.Gold
                ? "Gold redemption: Winner receives pure gold value equivalent to the entitlement amount at redemption time."
                : "Cash redemption: Winner receives cash payout equivalent to the entitlement amount.",
            "Non-winning slots are entitled to claim their full confirmed contributions at plan maturity."
        };

        return new CustomerChituPlanDetailDto
        {
            PlanId = summary.PlanId,
            BranchId = summary.BranchId,
            BranchName = summary.BranchName,
            Code = summary.Code,
            Name = summary.Name,
            Description = summary.Description,
            RedemptionType = summary.RedemptionType,
            Currency = summary.Currency,
            DurationMonths = summary.DurationMonths,
            StartDate = summary.StartDate,
            EndDate = summary.EndDate,
            MonthlySlotPrice = summary.MonthlySlotPrice,
            WinningEntitlementPerSlot = summary.WinningEntitlementPerSlot,
            TotalSlotCapacity = summary.TotalSlotCapacity,
            IssuedSlotsCount = summary.IssuedSlotsCount,
            AvailableSlotsCount = summary.AvailableSlotsCount,
            CapacityPercentage = summary.CapacityPercentage,
            CurrentRoundIndex = summary.CurrentRoundIndex,
            NextRaffleRoundIndex = summary.NextRaffleRoundIndex,
            NextRaffleDate = summary.NextRaffleDate,
            CustomerOwnedSlotsCount = summary.CustomerOwnedSlotsCount,
            IsAvailableForJoining = summary.IsAvailableForJoining,
            JoiningAvailabilityReason = summary.JoiningAvailabilityReason,
            Schedule = schedule,
            Terms = terms
        };
    }

    #endregion

    #region Joining & Purchasing Slots

    public async Task<CustomerChituJoinPreviewResponse> PreviewJoinAsync(
        Guid customerId,
        Guid planId,
        CustomerChituJoinPreviewRequest request,
        CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be a positive whole number of slots.", nameof(request.Quantity));
        }

        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .Include(p => p.Slots)
            .Include(p => p.Draws)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        ValidatePlanAvailabilityForJoining(plan, customer, branchToday, request.Quantity);

        var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(plan, branchToday);
        var (nextRaffleRound, nextRaffleDate) = ChituScheduleCalculator.DetermineNextRaffle(plan, plan.Draws, branchToday);

        // Before first raffle (branchToday < StartDate): Round 1 only (requiredInitialInstalments = 1).
        // Late joining (branchToday >= StartDate): Catch-up covering Round 1 through currentRoundIndex.
        var requiredInitialInstalmentsPerSlot = currentRoundIndex;
        var requiredInitialAmountPerSlot = requiredInitialInstalmentsPerSlot * plan.MonthlyInstalmentAmount;
        var totalInitialAmountPayable = requiredInitialAmountPerSlot * request.Quantity;
        var totalOrdinaryMonthlyPayment = plan.MonthlyInstalmentAmount * request.Quantity;
        var winningEntitlementPerSlot = plan.MonthlyInstalmentAmount * plan.DurationMonths;
        var availableSlots = Math.Max(0, plan.TotalSlotCapacity - plan.Slots.Count);
        var existingOwnedCount = plan.Slots.Count(s => s.CustomerId == customerId);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var previewRef = $"JOIN-PREV-{Guid.NewGuid():N}";

        _previewCache[previewRef] = new PreviewCacheEntry(
            previewRef,
            customerId,
            planId,
            totalInitialAmountPayable,
            expiresAt,
            System.Text.Json.JsonSerializer.Serialize(new { request.Quantity, requiredInitialInstalmentsPerSlot }));

        return new CustomerChituJoinPreviewResponse
        {
            PlanId = plan.Id,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            Currency = "LKR",
            MonthlySlotPrice = plan.MonthlyInstalmentAmount,
            RequestedQuantity = request.Quantity,
            TotalOrdinaryMonthlyPayment = totalOrdinaryMonthlyPayment,
            RequiredInitialInstalmentsPerSlot = requiredInitialInstalmentsPerSlot,
            RequiredInitialAmountPerSlot = requiredInitialAmountPerSlot,
            TotalInitialAmountPayable = totalInitialAmountPayable,
            AvailableSlotsCount = availableSlots,
            WinningEntitlementPerSlot = winningEntitlementPerSlot,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            CurrentRoundIndex = currentRoundIndex,
            NextRaffleRoundIndex = nextRaffleRound,
            NextRaffleDate = nextRaffleDate,
            ExistingOwnedSlotsCount = existingOwnedCount,
            PreviewReference = previewRef,
            PreviewCreatedAtUtc = now,
            PreviewExpiresAtUtc = expiresAt
        };
    }

    public async Task<CustomerChituJoinResponse> ConfirmAndJoinAsync(
        Guid customerId,
        Guid planId,
        CustomerChituJoinRequest request,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be a positive whole number of slots.", nameof(request.Quantity));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(request.IdempotencyKey));
        }

        var trimmedKey = request.IdempotencyKey.Trim();

        // 1. Idempotency Check
        var existingPayment = await _db.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == trimmedKey, ct);

        if (existingPayment != null)
        {
            if (existingPayment.CustomerId != customerId || existingPayment.Module != BusinessModule.Chitu)
            {
                throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed with different parameters.");
            }

            if (existingPayment.Status == PaymentStatus.Succeeded)
            {
                var existingSlots = await _db.ChituSlots
                    .Include(s => s.ChituPlan)
                    .Include(s => s.Instalments)
                    .Where(s => s.Instalments.Any(i => i.PaymentId == existingPayment.Id))
                    .OrderBy(s => s.SlotNumber)
                    .ThenBy(s => s.Id)
                    .ToListAsync(ct);

                var existingPlan = existingSlots.FirstOrDefault()?.ChituPlan;
                if (existingPlan != null && existingPlan.Id != planId)
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed for a different plan.");
                }

                if (existingSlots.Count != request.Quantity)
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed with a different slot quantity.");
                }

                return new CustomerChituJoinResponse
                {
                    PaymentId = existingPayment.Id,
                    PlanId = existingPlan?.Id ?? planId,
                    PlanCode = existingPlan?.Code ?? string.Empty,
                    PlanName = existingPlan?.Name ?? string.Empty,
                    SlotsCount = existingSlots.Count,
                    TotalAmountPaid = existingPayment.Amount,
                    Currency = existingPayment.Currency,
                    IsSimulated = existingPayment.ProviderReference.StartsWith("SIM-") || (existingPayment.Remarks != null && existingPayment.Remarks.Contains("[SIMULATED]")),
                    AssignedSlots = existingSlots.OrderBy(s => s.SlotNumber).ThenBy(s => s.Id).Select(s => new CustomerAssignedSlotDto
                    {
                        SlotId = s.Id,
                        SlotNumber = s.SlotNumber,
                        SlotDisplayCode = s.SlotDisplayCode,
                        InitialMonthsPaid = s.Instalments.Count(i => i.PaymentId == existingPayment.Id),
                        ActivatedAtUtc = s.ActivatedAtUtc ?? s.CreatedAtUtc
                    }).ToList(),
                    Message = "Slots were previously assigned successfully (Idempotent response)."
                };
            }
        }

        // 2. Validate Payment Provider Configuration
        if (!_paymentOptions.EnableSimulation)
        {
            throw new PaymentProviderNotConfiguredException();
        }

        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var isRelational = _db.Database.IsRelational();
            await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (isRelational)
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"ChituPlans\" WHERE \"Id\" = {0} FOR UPDATE",
                        new object[] { planId },
                        ct);
                }

                var plan = await _db.ChituPlans
                    .Include(p => p.Slots)
                    .Include(p => p.Draws)
                    .FirstOrDefaultAsync(p => p.Id == planId, ct)
                    ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

                ValidatePlanAvailabilityForJoining(plan, customer, branchToday, request.Quantity);

                var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(plan, branchToday);
                var requiredMonths = currentRoundIndex;
                var requiredAmountPerSlot = requiredMonths * plan.MonthlyInstalmentAmount;
                var totalInitialAmount = requiredAmountPerSlot * request.Quantity;

                // Validate preview terms / expected amount
                if (request.ExpectedTotalInitialAmount.HasValue &&
                    Math.Round(request.ExpectedTotalInitialAmount.Value, 2) != Math.Round(totalInitialAmount, 2))
                {
                    throw new ChituPreviewExpiredOrChangedException(
                        $"Required initial payment has changed from LKR {request.ExpectedTotalInitialAmount.Value:N2} to LKR {totalInitialAmount:N2}. Please preview again.",
                        request.ExpectedTotalInitialAmount.Value,
                        totalInitialAmount);
                }

                if (!string.IsNullOrWhiteSpace(request.PreviewReference))
                {
                    if (_previewCache.TryGetValue(request.PreviewReference, out var cachedPreview))
                    {
                        if (DateTimeOffset.UtcNow > cachedPreview.ExpiresAtUtc ||
                            cachedPreview.CustomerId != customerId ||
                            cachedPreview.PlanId != planId ||
                            Math.Round(cachedPreview.ExpectedTotalAmount, 2) != Math.Round(totalInitialAmount, 2))
                        {
                            throw new ChituPreviewExpiredOrChangedException(
                                "Your preview session has expired or plan conditions changed. Please preview again.",
                                cachedPreview.ExpectedTotalAmount,
                                totalInitialAmount);
                        }
                    }
                }

                // 3. Create simulated Payment
                var payment = new Payment
                {
                    BranchId = plan.BranchId,
                    CustomerId = customer.Id,
                    Module = BusinessModule.Chitu,
                    Amount = totalInitialAmount,
                    Currency = "LKR",
                    Method = PaymentMethod.MockOnline,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = $"SIM-CHITU-{Guid.NewGuid():N}",
                    IdempotencyKey = trimmedKey,
                    RecordedByStaffId = null,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Remarks = $"[SIMULATED] Provider=Simulated Source=CustomerMobile Mode=Test. Initial joining payment for {request.Quantity} slot(s) in plan {plan.Code}."
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);

                // 4. Sequential slot allocation (max slot number + 1)
                var currentMaxSlotNum = plan.Slots.Any() ? plan.Slots.Max(s => s.SlotNumber) : 0;
                var createdSlots = new List<ChituSlot>();
                var assignedSlotDtos = new List<CustomerAssignedSlotDto>();

                for (int i = 1; i <= request.Quantity; i++)
                {
                    var slotNum = currentMaxSlotNum + i;
                    var slotCode = $"{plan.Code}-{slotNum:D3}";

                    var slot = new ChituSlot
                    {
                        ChituPlanId = plan.Id,
                        CustomerId = customer.Id,
                        SlotNumber = slotNum,
                        SlotDisplayCode = slotCode,
                        Status = ChituSlotStatus.Active,
                        ActivatedAtUtc = DateTimeOffset.UtcNow
                    };

                    _db.ChituSlots.Add(slot);
                    await _db.SaveChangesAsync(ct);

                    for (int m = 1; m <= plan.DurationMonths; m++)
                    {
                        var isPaid = m <= requiredMonths;
                        var instalment = new ChituInstalment
                        {
                            ChituSlotId = slot.Id,
                            MonthIndex = m,
                            DueDate = plan.StartDate.AddMonths(m - 1),
                            AmountDue = plan.MonthlyInstalmentAmount,
                            Status = isPaid ? InstalmentStatus.Paid : InstalmentStatus.Pending,
                            PaidAtUtc = isPaid ? DateTimeOffset.UtcNow : null,
                            PaymentId = isPaid ? payment.Id : null
                        };

                        _db.ChituInstalments.Add(instalment);
                        await _db.SaveChangesAsync(ct);

                        if (isPaid)
                        {
                            var allocation = new PaymentAllocation
                            {
                                PaymentId = payment.Id,
                                AllocationType = "ChituInstalment",
                                TargetEntityId = instalment.Id,
                                AllocatedAmount = plan.MonthlyInstalmentAmount
                            };
                            _db.PaymentAllocations.Add(allocation);
                        }
                    }

                    createdSlots.Add(slot);
                    assignedSlotDtos.Add(new CustomerAssignedSlotDto
                    {
                        SlotId = slot.Id,
                        SlotNumber = slot.SlotNumber,
                        SlotDisplayCode = slot.SlotDisplayCode,
                        InitialMonthsPaid = requiredMonths,
                        ActivatedAtUtc = slot.ActivatedAtUtc.Value
                    });
                }

                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);

                // Invalidate preview reference
                if (!string.IsNullOrWhiteSpace(request.PreviewReference))
                {
                    _previewCache.TryRemove(request.PreviewReference, out _);
                }

                // 5. Send Notification
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        customer.Id,
                        "Chitu Plan Joined",
                        $"You have successfully joined {plan.Name} ({plan.Code}) with {request.Quantity} slot(s).",
                        "chitu_join",
                        ct);
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Failed to create customer notification for Chitu join.");
                }

                return new CustomerChituJoinResponse
                {
                    PaymentId = payment.Id,
                    PlanId = plan.Id,
                    PlanCode = plan.Code,
                    PlanName = plan.Name,
                    SlotsCount = request.Quantity,
                    TotalAmountPaid = totalInitialAmount,
                    Currency = "LKR",
                    IsSimulated = true,
                    AssignedSlots = assignedSlotDtos,
                    Message = $"Successfully joined {plan.Name} with {request.Quantity} slot(s)."
                };
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                if (ex is DbUpdateException dbEx && (dbEx.InnerException?.Message.Contains("23505") == true || dbEx.InnerException?.Message.Contains("IX_ChituSlots_ChituPlanId_SlotNumber") == true))
                {
                    throw new ChituCapacityUnavailableException("The requested slot capacity is no longer available due to concurrent assignments.");
                }
                throw;
            }
        });
    }

    #endregion

    #region My Plans (Portfolio)

    public async Task<CustomerChituInvestmentsPagedResponse> GetInvestmentsAsync(
        Guid customerId,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var slots = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.ChituPlan).ThenInclude(p => p.Branch)
            .Include(s => s.ChituPlan.Draws)
            .Include(s => s.Instalments).ThenInclude(i => i.Payment)
            .Include(s => s.WinnerBenefit)
            .Where(s => s.CustomerId == customerId)
            .ToListAsync(ct);

        // Group by ChituPlan
        var groupedPlans = slots
            .GroupBy(s => s.ChituPlanId)
            .Select(g =>
            {
                var plan = g.First().ChituPlan!;
                var planSlots = g.OrderBy(s => s.SlotNumber).ToList();
                return MapToInvestmentPlanDto(plan, planSlots, branchToday);
            })
            .ToList();

        // Optional status filter: "active", "ended", "all"
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            if (normalized == "active")
            {
                groupedPlans = groupedPlans.Where(p => p.PlanStatus == PlanStatus.Active && branchToday <= p.EndDate).ToList();
            }
            else if (normalized == "ended" || normalized == "closed")
            {
                groupedPlans = groupedPlans.Where(p => p.PlanStatus != PlanStatus.Active || branchToday > p.EndDate).ToList();
            }
        }

        var totalCount = groupedPlans.Count;
        var pagedItems = groupedPlans
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CustomerChituInvestmentsPagedResponse
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerChituInvestmentPlanDto> GetInvestmentPlanDetailsAsync(
        Guid customerId,
        Guid planId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.Draws)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        var planSlots = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.Instalments).ThenInclude(i => i.Payment)
            .Include(s => s.WinnerBenefit)
            .Where(s => s.ChituPlanId == planId && s.CustomerId == customerId)
            .OrderBy(s => s.SlotNumber)
            .ToListAsync(ct);

        if (!planSlots.Any())
        {
            throw new KeyNotFoundException($"No slots owned in plan '{planId}'.");
        }

        return MapToInvestmentPlanDto(plan, planSlots, branchToday);
    }

    public async Task<CustomerChituSlotDetailDto> GetSlotDetailsAsync(
        Guid customerId,
        Guid slotId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var slot = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.ChituPlan).ThenInclude(p => p.Branch)
            .Include(s => s.Instalments)
            .Include(s => s.WinnerBenefit)
            .FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new KeyNotFoundException($"Slot '{slotId}' was not found.");

        if (slot.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You do not own this slot.");
        }

        var plan = slot.ChituPlan!;
        var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(plan, branchToday);
        var summary = MapToSlotSummaryDto(slot, plan.MonthlyInstalmentAmount, plan.DurationMonths, currentRoundIndex);

        var instalmentDtos = slot.Instalments
            .OrderBy(i => i.MonthIndex)
            .Select(i => new CustomerChituSlotInstalmentDto
            {
                Id = i.Id,
                MonthIndex = i.MonthIndex,
                DueDate = i.DueDate,
                AmountDue = i.AmountDue,
                Status = i.Status,
                PaidAtUtc = i.PaidAtUtc,
                PaymentId = i.PaymentId
            })
            .ToList();

        CustomerWinnerBenefitDto? winnerBenefitDto = null;
        if (slot.WinnerBenefit != null)
        {
            winnerBenefitDto = new CustomerWinnerBenefitDto
            {
                Id = slot.WinnerBenefit.Id,
                DrawId = slot.WinnerBenefit.ChituDrawId,
                SlotId = slot.WinnerBenefit.ChituSlotId,
                SlotDisplayCode = slot.SlotDisplayCode,
                ChituBenefitValue = slot.WinnerBenefit.ChituBenefitValue,
                AdvanceExcessAmount = slot.WinnerBenefit.AdvanceExcessAmount,
                RedemptionType = slot.WinnerBenefit.RedemptionType,
                Status = slot.WinnerBenefit.Status,
                ClaimedAtUtc = slot.WinnerBenefit.ClaimedAtUtc,
                ClaimRemarks = slot.WinnerBenefit.ClaimRemarks
            };
        }

        return new CustomerChituSlotDetailDto
        {
            SlotId = summary.SlotId,
            SlotNumber = summary.SlotNumber,
            SlotDisplayCode = summary.SlotDisplayCode,
            JoinedAtUtc = summary.JoinedAtUtc,
            MonthlySlotPrice = summary.MonthlySlotPrice,
            DurationMonths = summary.DurationMonths,
            InstalmentsPaidCount = summary.InstalmentsPaidCount,
            PaidThroughRound = summary.PaidThroughRound,
            ArrearsMonths = summary.ArrearsMonths,
            ArrearsAmount = summary.ArrearsAmount,
            AdvancePaidMonths = summary.AdvancePaidMonths,
            AdvancePaidAmount = summary.AdvancePaidAmount,
            IsCurrentRoundPaid = summary.IsCurrentRoundPaid,
            TotalConfirmedContributions = summary.TotalConfirmedContributions,
            SlotStatus = summary.SlotStatus,
            IsWinner = summary.IsWinner,
            WinningRoundIndex = summary.WinningRoundIndex,
            WinningDrawDate = summary.WinningDrawDate,
            WinningPrizeAmount = summary.WinningPrizeAmount,
            WinningEntitlementClaimed = summary.WinningEntitlementClaimed,
            IsRaffleEligible = summary.IsRaffleEligible,
            RaffleEligibilityReason = summary.RaffleEligibilityReason,
            PlanId = plan.Id,
            PlanName = plan.Name,
            PlanCode = plan.Code,
            Currency = "LKR",
            Instalments = instalmentDtos,
            EntitlementClaimInfo = winnerBenefitDto
        };
    }

    public async Task<List<CustomerPaymentSummaryDto>> GetSlotPaymentsAsync(
        Guid customerId,
        Guid slotId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);

        var slot = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.ChituPlan)
            .Include(s => s.Instalments)
            .FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new KeyNotFoundException($"Slot '{slotId}' was not found.");

        if (slot.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You do not own this slot.");
        }

        var paymentIds = slot.Instalments
            .Where(i => i.PaymentId.HasValue)
            .Select(i => i.PaymentId!.Value)
            .Distinct()
            .ToList();

        if (!paymentIds.Any())
        {
            return new List<CustomerPaymentSummaryDto>();
        }

        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .Where(p => paymentIds.Contains(p.Id))
            .OrderByDescending(p => p.CompletedAtUtc ?? p.CreatedAtUtc)
            .ToListAsync(ct);

        return payments.Select(p =>
        {
            var isSimulated = p.ProviderReference.StartsWith("SIM-") || (p.Remarks != null && p.Remarks.Contains("[SIMULATED]"));
            return new CustomerPaymentSummaryDto
            {
                PaymentId = p.Id,
                PlanType = "Chitu",
                PlanId = slot.ChituPlanId,
                PlanName = slot.ChituPlan?.Name ?? string.Empty,
                PlanCode = slot.ChituPlan?.Code ?? string.Empty,
                Currency = p.Currency,
                Amount = p.Amount,
                Status = p.Status.ToString(),
                PaymentMethod = p.Method.ToString(),
                Provider = isSimulated ? "Simulated" : "Cash/Bank",
                ProviderReference = p.ProviderReference,
                CreatedAtUtc = p.CreatedAtUtc,
                ConfirmedAtUtc = p.CompletedAtUtc,
                IsSimulated = isSimulated,
                AllocationsCount = p.Allocations.Count,
                SlotsCoveredCount = 1,
                HasDetails = true,
                SummaryDescription = $"Payment of LKR {p.Amount:N2} for slot {slot.SlotDisplayCode}"
            };
        }).ToList();
    }

    #endregion

    #region Existing-Slot Contributions

    public async Task<CustomerPayableSlotsDto> GetPayableSlotsAsync(
        Guid customerId,
        Guid planId,
        CancellationToken ct = default)
    {
        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        var slots = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.Instalments)
            .Where(s => s.ChituPlanId == planId && s.CustomerId == customerId)
            .OrderBy(s => s.SlotNumber)
            .ToListAsync(ct);

        var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(plan, branchToday);

        var slotItems = slots.Select(s =>
        {
            var paidInstalments = s.Instalments.Where(i => i.Status == InstalmentStatus.Paid).OrderBy(i => i.MonthIndex).ToList();
            var unpaidInstalments = s.Instalments.Where(i => i.Status != InstalmentStatus.Paid).OrderBy(i => i.MonthIndex).ToList();

            var arrearsMonths = unpaidInstalments.Count(i => i.MonthIndex <= currentRoundIndex);
            var arrearsAmount = arrearsMonths * plan.MonthlyInstalmentAmount;
            var isCurrentRoundPaid = paidInstalments.Any(i => i.MonthIndex == currentRoundIndex);

            bool isEligible = true;
            string? ineligibilityReason = null;

            if (s.Status == ChituSlotStatus.Won)
            {
                isEligible = false;
                ineligibilityReason = $"Slot has already won in round {s.WinningRoundIndex?.ToString() ?? "a previous round"} and has no further payment obligations.";
            }
            else if (s.Status != ChituSlotStatus.Active)
            {
                isEligible = false;
                ineligibilityReason = $"Slot is not active (Status: {s.Status}).";
            }
            else if (!unpaidInstalments.Any())
            {
                isEligible = false;
                ineligibilityReason = "All instalments for this slot are fully paid.";
            }

            return new CustomerPayableSlotItemDto
            {
                SlotId = s.Id,
                SlotNumber = s.SlotNumber,
                SlotDisplayCode = s.SlotDisplayCode,
                Status = s.Status,
                IsEligibleForPayment = isEligible,
                IneligibilityReason = ineligibilityReason,
                MonthlyInstalmentPrice = plan.MonthlyInstalmentAmount,
                PaidInstalmentRounds = paidInstalments.Select(i => i.MonthIndex).ToList(),
                UnpaidInstalmentRounds = unpaidInstalments.Select(i => i.MonthIndex).ToList(),
                OutstandingDueAmount = arrearsAmount,
                RemainingPayableMonths = unpaidInstalments.Count,
                IsCurrentRoundPaid = isCurrentRoundPaid,
                InstalmentsPaidCount = paidInstalments.Count,
                TotalConfirmedPaid = paidInstalments.Count * plan.MonthlyInstalmentAmount
            };
        }).ToList();

        return new CustomerPayableSlotsDto
        {
            PlanId = plan.Id,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            MonthlySlotPrice = plan.MonthlyInstalmentAmount,
            Currency = "LKR",
            CurrentRoundIndex = currentRoundIndex,
            Slots = slotItems
        };
    }

    public async Task<CustomerChituPaymentPreviewResponse> PreviewExistingSlotPaymentAsync(
        Guid customerId,
        Guid planId,
        CustomerChituPaymentPreviewRequest request,
        CancellationToken ct = default)
    {
        if (request.SlotSelections == null || !request.SlotSelections.Any())
        {
            throw new ArgumentException("At least one slot must be selected for payment.", nameof(request.SlotSelections));
        }

        // Validate uniqueness of slot selections
        var distinctCount = request.SlotSelections.Select(s => s.SlotId).Distinct().Count();
        if (distinctCount != request.SlotSelections.Count)
        {
            throw new ArgumentException("Duplicate slot IDs detected in selection.");
        }

        var customer = await GetActiveCustomerAsync(customerId, ct);
        var branchToday = GetBranchToday(customer.PrimaryBranch?.Timezone);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(plan, branchToday);

        var previewItems = new List<CustomerSlotPaymentPreviewItemDto>();
        decimal totalPayable = 0m;

        foreach (var selection in request.SlotSelections)
        {
            if (selection.InstalmentsToPayCount <= 0)
            {
                throw new ArgumentException($"Instalments to pay for slot '{selection.SlotId}' must be at least 1.");
            }

            var slot = await _db.ChituSlots
                .AsNoTracking()
                .Include(s => s.Instalments)
                .FirstOrDefaultAsync(s => s.Id == selection.SlotId && s.ChituPlanId == planId && s.CustomerId == customerId, ct)
                ?? throw new KeyNotFoundException($"Slot '{selection.SlotId}' was not found or does not belong to you.");

            if (slot.Status != ChituSlotStatus.Active)
            {
                throw new InvalidOperationException($"Slot '{slot.SlotDisplayCode}' cannot accept payments (Status: {slot.Status}). Winning or closed slots cannot receive payments.");
            }

            var unpaidInstalments = slot.Instalments
                .Where(i => i.Status != InstalmentStatus.Paid)
                .OrderBy(i => i.MonthIndex)
                .ToList();

            if (selection.InstalmentsToPayCount > unpaidInstalments.Count)
            {
                throw new ArgumentException($"Slot '{slot.SlotDisplayCode}' only has {unpaidInstalments.Count} remaining unpaid instalment(s). Cannot pay {selection.InstalmentsToPayCount}.");
            }

            // Oldest unpaid first
            var clearingInstalments = unpaidInstalments.Take(selection.InstalmentsToPayCount).ToList();
            var clearedMonthIndices = clearingInstalments.Select(i => i.MonthIndex).ToList();
            var allocatedAmount = selection.InstalmentsToPayCount * plan.MonthlyInstalmentAmount;
            totalPayable += allocatedAmount;

            // Resulting state simulation
            var simulatedPaidMonths = new HashSet<int>(slot.Instalments.Where(i => i.Status == InstalmentStatus.Paid).Select(i => i.MonthIndex));
            foreach (var m in clearedMonthIndices)
            {
                simulatedPaidMonths.Add(m);
            }

            var resultingArrearsMonths = 0;
            for (int m = 1; m <= currentRoundIndex; m++)
            {
                if (!simulatedPaidMonths.Contains(m)) resultingArrearsMonths++;
            }
            var remainingArrearsAmount = resultingArrearsMonths * plan.MonthlyInstalmentAmount;
            var advanceCoverage = simulatedPaidMonths.Count(m => m > currentRoundIndex);
            var resultingCurrentRoundPaid = simulatedPaidMonths.Contains(currentRoundIndex);

            // Raffle eligibility check: all rounds 1..currentRoundIndex must be paid
            bool resultingRaffleEligible = true;
            for (int m = 1; m <= currentRoundIndex; m++)
            {
                if (!simulatedPaidMonths.Contains(m))
                {
                    resultingRaffleEligible = false;
                    break;
                }
            }

            previewItems.Add(new CustomerSlotPaymentPreviewItemDto
            {
                SlotId = slot.Id,
                SlotDisplayCode = slot.SlotDisplayCode,
                InstalmentsToPayCount = selection.InstalmentsToPayCount,
                AmountAllocated = allocatedAmount,
                ClearedRounds = clearedMonthIndices,
                RemainingArrearsAfterPayment = remainingArrearsAmount,
                AdvanceCoverageMonthsAfterPayment = advanceCoverage,
                ResultingCurrentRoundCoverage = resultingCurrentRoundPaid,
                ResultingRaffleEligible = resultingRaffleEligible
            });
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);
        var previewRef = $"PAY-PREV-{Guid.NewGuid():N}";

        _previewCache[previewRef] = new PreviewCacheEntry(
            previewRef,
            customerId,
            planId,
            totalPayable,
            expiresAt,
            System.Text.Json.JsonSerializer.Serialize(request.SlotSelections));

        return new CustomerChituPaymentPreviewResponse
        {
            PlanId = plan.Id,
            PlanCode = plan.Code,
            PlanName = plan.Name,
            Currency = "LKR",
            MonthlySlotPrice = plan.MonthlyInstalmentAmount,
            TotalPayableAmount = totalPayable,
            Slots = previewItems,
            PreviewReference = previewRef,
            PreviewCreatedAtUtc = now,
            PreviewExpiresAtUtc = expiresAt
        };
    }

    public async Task<CustomerChituPaymentConfirmResponse> ConfirmExistingSlotPaymentAsync(
        Guid customerId,
        Guid planId,
        CustomerChituPaymentConfirmRequest request,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        if (request.SlotSelections == null || !request.SlotSelections.Any())
        {
            throw new ArgumentException("At least one slot must be selected for payment.", nameof(request.SlotSelections));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(request.IdempotencyKey));
        }

        var trimmedKey = request.IdempotencyKey.Trim();

        // 1. Idempotency Check
        var existingPayment = await _db.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == trimmedKey, ct);

        if (existingPayment != null)
        {
            if (existingPayment.CustomerId != customerId || existingPayment.Module != BusinessModule.Chitu)
            {
                throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed with different parameters.");
            }

            if (existingPayment.Status == PaymentStatus.Succeeded)
            {
                var plan = await _db.ChituPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
                var paidInstalments = await _db.ChituInstalments
                    .Include(i => i.ChituSlot)
                    .Where(i => i.PaymentId == existingPayment.Id)
                    .ToListAsync(ct);

                if (paidInstalments.Any(i => i.ChituSlot?.ChituPlanId != planId))
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed for a different plan.");
                }

                var receipts = paidInstalments
                    .GroupBy(i => i.ChituSlotId)
                    .OrderBy(g => g.First().ChituSlot?.SlotNumber ?? 0)
                    .ThenBy(g => g.Key)
                    .Select(g => new CustomerSlotPaymentReceiptDto
                    {
                        SlotId = g.Key,
                        SlotDisplayCode = g.First().ChituSlot?.SlotDisplayCode ?? string.Empty,
                        AmountAllocated = g.Sum(i => i.AmountDue),
                        MonthsCovered = g.Count(),
                        ClearedMonthIndices = g.Select(i => i.MonthIndex).OrderBy(m => m).ToList()
                    })
                    .ToList();

                return new CustomerChituPaymentConfirmResponse
                {
                    PaymentId = existingPayment.Id,
                    PlanId = planId,
                    PlanCode = plan?.Code ?? string.Empty,
                    PlanName = plan?.Name ?? string.Empty,
                    TotalAmount = existingPayment.Amount,
                    Currency = existingPayment.Currency,
                    PaidAtUtc = existingPayment.CompletedAtUtc ?? existingPayment.CreatedAtUtc,
                    PaymentMethod = existingPayment.Method.ToString(),
                    IsSimulated = existingPayment.ProviderReference.StartsWith("SIM-") || (existingPayment.Remarks != null && existingPayment.Remarks.Contains("[SIMULATED]")),
                    Receipts = receipts,
                    Message = "Payment was previously recorded successfully (Idempotent response)."
                };
            }
        }

        // 2. Validate Payment Provider Configuration
        if (!_paymentOptions.EnableSimulation)
        {
            throw new PaymentProviderNotConfiguredException();
        }

        var customer = await GetActiveCustomerAsync(customerId, ct);

        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var isRelational = _db.Database.IsRelational();
            await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (isRelational)
                {
                    var slotIds = request.SlotSelections.Select(s => s.SlotId).Distinct().ToArray();
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"ChituSlots\" WHERE \"Id\" = ANY({0}) FOR UPDATE",
                        new object[] { slotIds },
                        ct);
                }

                var plan = await _db.ChituPlans
                    .FirstOrDefaultAsync(p => p.Id == planId, ct)
                    ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

                decimal calculatedTotal = 0m;
                var allocationsToPost = new List<(ChituSlot Slot, List<ChituInstalment> InstalmentsToClear, decimal Amount)>();

                foreach (var selection in request.SlotSelections)
                {
                    if (selection.InstalmentsToPayCount <= 0)
                    {
                        throw new ArgumentException("Instalments to pay must be greater than zero.");
                    }

                    var slot = await _db.ChituSlots
                        .Include(s => s.Instalments)
                        .FirstOrDefaultAsync(s => s.Id == selection.SlotId && s.ChituPlanId == planId && s.CustomerId == customerId, ct)
                        ?? throw new KeyNotFoundException($"Slot '{selection.SlotId}' was not found.");

                    if (slot.Status != ChituSlotStatus.Active)
                    {
                        throw new ChituPreviewExpiredOrChangedException($"Slot '{slot.SlotDisplayCode}' is not active (Status: {slot.Status}). Cannot accept payment.");
                    }

                    var unpaidInstalments = slot.Instalments
                        .Where(i => i.Status != InstalmentStatus.Paid)
                        .OrderBy(i => i.MonthIndex)
                        .ToList();

                    if (selection.ExpectedStartingMonthIndex.HasValue && unpaidInstalments.FirstOrDefault()?.MonthIndex != selection.ExpectedStartingMonthIndex.Value)
                    {
                        throw new ChituPreviewExpiredOrChangedException($"The target unpaid instalment for slot '{slot.SlotDisplayCode}' has already been paid. Money will not be shifted to a future month.");
                    }

                    if (selection.InstalmentsToPayCount > unpaidInstalments.Count)
                    {
                        throw new ChituPreviewExpiredOrChangedException(
                            $"Slot '{slot.SlotDisplayCode}' only has {unpaidInstalments.Count} unpaid instalment(s) remaining. Previous preview has changed.");
                    }

                    var instalmentsToClear = unpaidInstalments.Take(selection.InstalmentsToPayCount).ToList();
                    var slotAmount = selection.InstalmentsToPayCount * plan.MonthlyInstalmentAmount;
                    calculatedTotal += slotAmount;

                    allocationsToPost.Add((slot, instalmentsToClear, slotAmount));
                }

                // Verify preview reference / expected amount
                if (request.ExpectedTotalAmount.HasValue &&
                    Math.Round(request.ExpectedTotalAmount.Value, 2) != Math.Round(calculatedTotal, 2))
                {
                    throw new ChituPreviewExpiredOrChangedException(
                        $"Total payable amount changed from LKR {request.ExpectedTotalAmount.Value:N2} to LKR {calculatedTotal:N2}. Please preview again.",
                        request.ExpectedTotalAmount.Value,
                        calculatedTotal);
                }

                if (!string.IsNullOrWhiteSpace(request.PreviewReference))
                {
                    if (_previewCache.TryGetValue(request.PreviewReference, out var cachedPreview))
                    {
                        if (DateTimeOffset.UtcNow > cachedPreview.ExpiresAtUtc ||
                            cachedPreview.CustomerId != customerId ||
                            cachedPreview.PlanId != planId ||
                            Math.Round(cachedPreview.ExpectedTotalAmount, 2) != Math.Round(calculatedTotal, 2))
                        {
                            throw new ChituPreviewExpiredOrChangedException(
                                "Payment preview expired or slot states have changed. Please generate a fresh preview.",
                                cachedPreview.ExpectedTotalAmount,
                                calculatedTotal);
                        }
                    }
                }

                // 3. Create simulated Payment
                var payment = new Payment
                {
                    BranchId = plan.BranchId,
                    CustomerId = customer.Id,
                    Module = BusinessModule.Chitu,
                    Amount = calculatedTotal,
                    Currency = "LKR",
                    Method = PaymentMethod.MockOnline,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = $"SIM-CHITU-{Guid.NewGuid():N}",
                    IdempotencyKey = trimmedKey,
                    RecordedByStaffId = null,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Remarks = $"[SIMULATED] Provider=Simulated Source=CustomerMobile Mode=Test. Existing slots payment for {allocationsToPost.Count} slot(s) in plan {plan.Code}."
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);

                var receipts = new List<CustomerSlotPaymentReceiptDto>();

                // 4. Clear instalments and create allocations
                foreach (var (slot, instalmentsToClear, amount) in allocationsToPost)
                {
                    var clearedIndices = new List<int>();

                    foreach (var inst in instalmentsToClear)
                    {
                        inst.Status = InstalmentStatus.Paid;
                        inst.PaidAtUtc = DateTimeOffset.UtcNow;
                        inst.PaymentId = payment.Id;
                        clearedIndices.Add(inst.MonthIndex);

                        var allocation = new PaymentAllocation
                        {
                            PaymentId = payment.Id,
                            AllocationType = "ChituInstalment",
                            TargetEntityId = inst.Id,
                            AllocatedAmount = plan.MonthlyInstalmentAmount
                        };
                        _db.PaymentAllocations.Add(allocation);
                    }

                    receipts.Add(new CustomerSlotPaymentReceiptDto
                    {
                        SlotId = slot.Id,
                        SlotDisplayCode = slot.SlotDisplayCode,
                        AmountAllocated = amount,
                        MonthsCovered = instalmentsToClear.Count,
                        ClearedMonthIndices = clearedIndices
                    });
                }

                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);

                if (!string.IsNullOrWhiteSpace(request.PreviewReference))
                {
                    _previewCache.TryRemove(request.PreviewReference, out _);
                }

                // 5. Send Notification
                try
                {
                    await _notificationService.CreateNotificationAsync(
                        customer.Id,
                        "Chitu Payment Received",
                        $"Payment of LKR {calculatedTotal:N2} received for {allocationsToPost.Count} slot(s) in {plan.Name}.",
                        "chitu_payment",
                        ct);
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Failed to create customer payment notification.");
                }

                return new CustomerChituPaymentConfirmResponse
                {
                    PaymentId = payment.Id,
                    PlanId = plan.Id,
                    PlanCode = plan.Code,
                    PlanName = plan.Name,
                    TotalAmount = calculatedTotal,
                    Currency = "LKR",
                    PaidAtUtc = payment.CompletedAtUtc.Value,
                    PaymentMethod = "Simulated",
                    IsSimulated = true,
                    Receipts = receipts,
                    Message = $"Successfully posted payment of LKR {calculatedTotal:N2} for {allocationsToPost.Count} slot(s)."
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

    #endregion

    #region Helper Methods

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

    private static void ValidatePlanAvailabilityForJoining(ChituPlan plan, Customer customer, DateOnly branchToday, int requestedQuantity)
    {
        if (plan.BranchId != customer.PrimaryBranchId)
        {
            throw new UnauthorizedAccessException("This Chitu plan belongs to a different branch and is not available for enrolment.");
        }

        if (plan.Status != PlanStatus.Active)
        {
            throw new InvalidOperationException("This Chitu plan is not currently active for joining.");
        }

        if (branchToday > plan.EndDate)
        {
            throw new InvalidOperationException("The final raffle date has passed. No further slot enrolments are accepted.");
        }

        if (plan.Draws.Count >= plan.DurationMonths)
        {
            throw new InvalidOperationException("All monthly raffle draws have already occurred. Joining is closed.");
        }

        var available = plan.TotalSlotCapacity - plan.Slots.Count;
        if (requestedQuantity > available)
        {
            throw new InvalidOperationException($"Capacity exceeded. Only {available} slot(s) available for joining.");
        }
    }

    private static CustomerChituPlanSummaryDto MapToSummaryDto(ChituPlan p, Guid customerId, DateOnly branchToday)
    {
        var issuedSlots = p.Slots.Count;
        var availableSlots = Math.Max(0, p.TotalSlotCapacity - issuedSlots);
        var capacityPct = p.TotalSlotCapacity > 0 ? Math.Round((decimal)issuedSlots / p.TotalSlotCapacity * 100m, 2) : 0m;
        var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(p, branchToday);
        var (nextRaffleRound, nextRaffleDate) = ChituScheduleCalculator.DetermineNextRaffle(p, p.Draws, branchToday);
        var customerOwnedCount = p.Slots.Count(s => s.CustomerId == customerId);
        var winningEntitlement = p.MonthlyInstalmentAmount * p.DurationMonths;

        var isAvailable = p.Status == PlanStatus.Active &&
                          availableSlots > 0 &&
                          branchToday <= p.EndDate &&
                          p.Draws.Count < p.DurationMonths;

        string reason = isAvailable ? "Open for joining" : (availableSlots <= 0 ? "Plan is full" : "Joining closed");

        return new CustomerChituPlanSummaryDto
        {
            PlanId = p.Id,
            BranchId = p.BranchId,
            BranchName = p.Branch?.Name ?? string.Empty,
            Code = p.Code,
            Name = p.Name,
            Description = p.Description,
            RedemptionType = p.RedemptionType,
            Currency = "LKR",
            DurationMonths = p.DurationMonths,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            MonthlySlotPrice = p.MonthlyInstalmentAmount,
            WinningEntitlementPerSlot = winningEntitlement,
            TotalSlotCapacity = p.TotalSlotCapacity,
            IssuedSlotsCount = issuedSlots,
            AvailableSlotsCount = availableSlots,
            CapacityPercentage = capacityPct,
            CurrentRoundIndex = currentRoundIndex,
            NextRaffleRoundIndex = nextRaffleRound,
            NextRaffleDate = nextRaffleDate,
            CustomerOwnedSlotsCount = customerOwnedCount,
            IsAvailableForJoining = isAvailable,
            JoiningAvailabilityReason = reason
        };
    }

    private static CustomerChituInvestmentPlanDto MapToInvestmentPlanDto(
        ChituPlan plan,
        List<ChituSlot> planSlots,
        DateOnly branchToday)
    {
        var currentRoundIndex = ChituScheduleCalculator.DetermineCurrentRoundIndex(plan, branchToday);
        var (nextRaffleRound, nextRaffleDate) = ChituScheduleCalculator.DetermineNextRaffle(plan, plan.Draws, branchToday);

        var slotSummaries = planSlots
            .Select(s => MapToSlotSummaryDto(s, plan.MonthlyInstalmentAmount, plan.DurationMonths, currentRoundIndex))
            .ToList();

        var totalOwned = planSlots.Count;
        var activeSlots = planSlots.Count(s => s.Status == ChituSlotStatus.Active);
        var winningSlots = planSlots.Count(s => s.Status == ChituSlotStatus.Won);

        // Commitment: sum of NormalMonthlyCommitment for slots that still require ongoing contributions
        var normalMonthlyCommitment = slotSummaries.Sum(s => s.MonthlySlotPrice > 0 && s.SlotStatus == ChituSlotStatus.Active && !s.IsWinner && s.PaidThroughRound < plan.DurationMonths ? plan.MonthlyInstalmentAmount : 0m);
        var totalOutstandingArrears = slotSummaries.Sum(s => s.ArrearsAmount);
        var currentRoundPaidCount = slotSummaries.Count(s => s.IsCurrentRoundPaid);
        var currentRoundUnpaidCount = totalOwned - currentRoundPaidCount;
        var totalConfirmedContributions = slotSummaries.Sum(s => s.TotalConfirmedContributions);

        var canPurchaseMore = plan.Status == PlanStatus.Active &&
                              branchToday <= plan.EndDate &&
                              plan.Draws.Count < plan.DurationMonths &&
                              plan.Slots.Count < plan.TotalSlotCapacity;

        var canPayExisting = planSlots.Any(s => s.Status == ChituSlotStatus.Active && s.Instalments.Any(i => i.Status != InstalmentStatus.Paid));

        var isSimulated = planSlots.Any(s => s.Instalments.Any(i =>
            i.Payment != null &&
            (i.Payment.ProviderReference.StartsWith("SIM-") || (i.Payment.Remarks != null && i.Payment.Remarks.Contains("[SIMULATED]")))));

        return new CustomerChituInvestmentPlanDto
        {
            PlanId = plan.Id,
            BranchId = plan.BranchId,
            BranchName = plan.Branch?.Name ?? string.Empty,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            RedemptionType = plan.RedemptionType,
            Currency = "LKR",
            DurationMonths = plan.DurationMonths,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            CurrentRoundIndex = currentRoundIndex,
            NextRaffleRoundIndex = nextRaffleRound,
            NextRaffleDate = nextRaffleDate,
            PlanStatus = plan.Status,
            TotalOwnedSlotsCount = totalOwned,
            ActiveSlotsCount = activeSlots,
            WinningSlotsCount = winningSlots,
            MonthlySlotPrice = plan.MonthlyInstalmentAmount,
            NormalMonthlyCommitment = normalMonthlyCommitment,
            ActualOutstandingDueThroughCurrentRound = totalOutstandingArrears,
            CurrentRoundPaidSlotsCount = currentRoundPaidCount,
            CurrentRoundUnpaidSlotsCount = currentRoundUnpaidCount,
            TotalConfirmedContributions = totalConfirmedContributions,
            CanPurchaseAdditionalSlots = canPurchaseMore,
            CanPayExistingSlots = canPayExisting,
            IsSimulated = isSimulated,
            Slots = slotSummaries
        };
    }

    private static CustomerChituSlotSummaryDto MapToSlotSummaryDto(
        ChituSlot slot,
        decimal monthlySlotPrice,
        int durationMonths,
        int currentRoundIndex)
    {
        var metrics = ChituScheduleCalculator.CalculateSlotMetrics(slot, monthlySlotPrice, durationMonths, currentRoundIndex);
        var isWinner = slot.Status == ChituSlotStatus.Won;

        return new CustomerChituSlotSummaryDto
        {
            SlotId = slot.Id,
            SlotNumber = slot.SlotNumber,
            SlotDisplayCode = slot.SlotDisplayCode,
            JoinedAtUtc = slot.ActivatedAtUtc ?? slot.CreatedAtUtc,
            MonthlySlotPrice = monthlySlotPrice,
            DurationMonths = durationMonths,
            InstalmentsPaidCount = metrics.TotalPaidMonths,
            PaidThroughRound = metrics.PaidThroughRound,
            ArrearsMonths = metrics.ArrearsMonths,
            ArrearsAmount = metrics.ArrearsAmount,
            AdvancePaidMonths = metrics.AdvancePaidMonths,
            AdvancePaidAmount = metrics.AdvancePaidAmount,
            IsCurrentRoundPaid = metrics.IsCurrentRoundPaid,
            TotalConfirmedContributions = metrics.TotalConfirmedContributions,
            SlotStatus = slot.Status,
            IsWinner = isWinner,
            WinningRoundIndex = slot.WinningRoundIndex,
            WinningDrawDate = slot.WinnerBenefit?.ChituDraw?.DrawDate,
            WinningPrizeAmount = slot.WinnerBenefit?.ChituBenefitValue,
            WinningEntitlementClaimed = slot.WinnerBenefit?.Status == BenefitStatus.Claimed,
            IsRaffleEligible = metrics.IsRaffleEligible,
            RaffleEligibilityReason = metrics.RaffleEligibilityReason
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
