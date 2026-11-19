using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class ChituService : IChituService
{
    private readonly SJewlsDbContext _db;
    private readonly IAuditLogService _auditService;
    private readonly ILogger<ChituService> _logger;

    public ChituService(
        SJewlsDbContext db,
        IAuditLogService auditService,
        ILogger<ChituService> logger)
    {
        _db = db;
        _auditService = auditService;
        _logger = logger;
    }

    #region Plan Management

    public async Task<ChituPlanCardDto> CreatePlanAsync(
        Guid currentStaffId,
        CreateChituPlanRequest request,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        // Branch permission check
        if (!isSuperAdmin && !permittedBranchIds.Contains(request.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to create a Chitu plan for this branch.");
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, ct)
            ?? throw new KeyNotFoundException($"Branch '{request.BranchId}' was not found.");

        if (!branch.IsActive)
        {
            throw new InvalidOperationException("Cannot create a Chitu plan for an inactive branch.");
        }

        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ArgumentException("Chitu plan name is required.", nameof(request.Name));
        }

        var trimmedCode = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedCode))
        {
            throw new ArgumentException("Chitu plan code is required.", nameof(request.Code));
        }

        // Case-normalized code uniqueness within the branch
        var codeExists = await _db.ChituPlans
            .AnyAsync(p => p.BranchId == request.BranchId && p.Code.ToUpper() == trimmedCode, ct);

        if (codeExists)
        {
            throw new InvalidOperationException($"A Chitu plan with code '{trimmedCode}' already exists in this branch.");
        }

        if (request.DurationMonths <= 0)
        {
            throw new ArgumentException("Duration must be a positive whole number of months.", nameof(request.DurationMonths));
        }

        if (request.MonthlyInstalmentAmount <= 0)
        {
            throw new ArgumentException("Monthly slot price must be greater than zero.", nameof(request.MonthlyInstalmentAmount));
        }

        if (request.TotalSlotCapacity <= 0)
        {
            throw new ArgumentException("Maximum slots must be greater than zero.", nameof(request.TotalSlotCapacity));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.StartDate < today)
        {
            throw new ArgumentException("New plan's first raffle date cannot be in the past.", nameof(request.StartDate));
        }

        // Automatically calculated end date: StartDate.AddMonths(DurationMonths - 1)
        var endDate = request.StartDate.AddMonths(request.DurationMonths - 1);
        var benefitValue = request.MonthlyInstalmentAmount * request.DurationMonths;

        var plan = new ChituPlan
        {
            BranchId = request.BranchId,
            Code = trimmedCode,
            Name = trimmedName,
            Description = request.Description?.Trim() ?? string.Empty,
            RedemptionType = request.RedemptionType,
            MonthlyInstalmentAmount = Math.Round(request.MonthlyInstalmentAmount, 2),
            ChituBenefitValue = Math.Round(benefitValue, 2),
            TotalSlotCapacity = request.TotalSlotCapacity,
            DurationMonths = request.DurationMonths,
            StartDate = request.StartDate,
            EndDate = endDate,
            DueDayOfMonth = request.StartDate.Day,
            Status = PlanStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.ChituPlans.Add(plan);
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            "Staff",
            currentStaffId,
            "CreateChituPlan",
            "ChituPlan",
            plan.Id.ToString(),
            plan.BranchId,
            null,
            new
            {
                plan.Code,
                plan.Name,
                plan.RedemptionType,
                plan.MonthlyInstalmentAmount,
                plan.DurationMonths,
                plan.StartDate,
                plan.EndDate,
                plan.TotalSlotCapacity
            },
            null);

        return await MapToCardDtoAsync(plan, today, ct);
    }

    public async Task<ChituPlanCardDto> UpdatePlanAsync(
        Guid currentStaffId,
        Guid planId,
        UpdateChituPlanRequest request,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .Include(p => p.Slots)
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to modify this Chitu plan.");
        }

        var beforeState = new
        {
            plan.Name,
            plan.Description,
            plan.Status,
            plan.Code,
            plan.RedemptionType,
            plan.DurationMonths,
            plan.MonthlyInstalmentAmount,
            plan.TotalSlotCapacity,
            plan.StartDate,
            plan.EndDate,
            plan.BranchId
        };

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            plan.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            plan.Description = request.Description.Trim();
        }

        if (request.Status.HasValue)
        {
            plan.Status = request.Status.Value;
        }

        if (request.TotalSlotCapacity.HasValue)
        {
            var assignedCount = plan.Slots.Count;
            if (request.TotalSlotCapacity.Value < assignedCount)
            {
                throw new InvalidOperationException($"Cannot set slot capacity to {request.TotalSlotCapacity.Value}. There are already {assignedCount} slots assigned.");
            }
            plan.TotalSlotCapacity = request.TotalSlotCapacity.Value;
        }

        var hasAssignedSlots = plan.Slots.Any();
        var hasDraws = await _db.ChituDraws.AnyAsync(d => d.ChituPlanId == plan.Id, ct);

        // If financial parameters are changed, verify that no slots or draws exist
        if (!hasAssignedSlots && !hasDraws)
        {
            if (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty && request.BranchId.Value != plan.BranchId)
            {
                var branchExists = await _db.Branches.AnyAsync(b => b.Id == request.BranchId.Value && b.IsActive, ct);
                if (!branchExists)
                {
                    throw new KeyNotFoundException($"Branch '{request.BranchId.Value}' was not found or is inactive.");
                }
                plan.BranchId = request.BranchId.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                var trimmedCode = request.Code.Trim().ToUpperInvariant();
                var codeExists = await _db.ChituPlans.AnyAsync(p => p.BranchId == plan.BranchId && p.Code == trimmedCode && p.Id != plan.Id, ct);
                if (codeExists)
                {
                    throw new InvalidOperationException($"A Chitu plan with code '{trimmedCode}' already exists for this branch.");
                }
                plan.Code = trimmedCode;
            }

            if (request.RedemptionType.HasValue)
            {
                plan.RedemptionType = request.RedemptionType.Value;
            }

            if (request.DurationMonths.HasValue && request.DurationMonths.Value > 0)
            {
                plan.DurationMonths = request.DurationMonths.Value;
            }

            if (request.MonthlyInstalmentAmount.HasValue && request.MonthlyInstalmentAmount.Value > 0)
            {
                plan.MonthlyInstalmentAmount = Math.Round(request.MonthlyInstalmentAmount.Value, 2);
            }

            if (request.StartDate.HasValue)
            {
                plan.StartDate = request.StartDate.Value;
                plan.DueDayOfMonth = request.StartDate.Value.Day;
            }

            // Recalculate EndDate and ChituBenefitValue
            plan.EndDate = plan.StartDate.AddMonths(plan.DurationMonths - 1);
            plan.ChituBenefitValue = Math.Round(plan.MonthlyInstalmentAmount * plan.DurationMonths, 2);
        }
        else if ((request.Code != null && request.Code.Trim().ToUpperInvariant() != plan.Code)
                 || (request.DurationMonths.HasValue && request.DurationMonths.Value != plan.DurationMonths)
                 || (request.MonthlyInstalmentAmount.HasValue && Math.Round(request.MonthlyInstalmentAmount.Value, 2) != plan.MonthlyInstalmentAmount)
                 || (request.StartDate.HasValue && request.StartDate.Value != plan.StartDate)
                 || (request.RedemptionType.HasValue && request.RedemptionType.Value != plan.RedemptionType)
                 || (request.BranchId.HasValue && request.BranchId.Value != plan.BranchId))
        {
            throw new InvalidOperationException("Core plan terms (code, branch, duration, price, start date, redemption type) cannot be changed once customer slots are assigned.");
        }

        plan.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(
            "Staff",
            currentStaffId,
            "UpdateChituPlan",
            "ChituPlan",
            plan.Id.ToString(),
            plan.BranchId,
            beforeState,
            new
            {
                plan.Name,
                plan.Description,
                plan.Status,
                plan.Code,
                plan.RedemptionType,
                plan.DurationMonths,
                plan.MonthlyInstalmentAmount,
                plan.TotalSlotCapacity,
                plan.StartDate,
                plan.EndDate,
                plan.BranchId
            },
            null);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await MapToCardDtoAsync(plan, today, ct);
    }

    public async Task<ChituPlanListResponse> GetPlansAsync(
        Guid currentStaffId,
        Guid? branchId = null,
        string? search = null,
        string? status = null,
        int? redemptionType = null,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        IQueryable<ChituPlan> query = _db.ChituPlans
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.Slots)
                .ThenInclude(s => s.Instalments)
            .Include(p => p.Draws);

        if (isSuperAdmin)
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                query = query.Where(p => p.BranchId == branchId.Value);
            }
        }
        else
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                if (!permittedBranchIds.Contains(branchId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have permission to view Chitu plans from this branch.");
                }
                query = query.Where(p => p.BranchId == branchId.Value);
            }
            else
            {
                query = query.Where(p => permittedBranchIds.Contains(p.BranchId));
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Code.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == PlanStatus.Active);
            }
            else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) || status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == PlanStatus.Closed || p.Status == PlanStatus.Draft);
            }
        }

        if (redemptionType.HasValue && redemptionType.Value > 0)
        {
            var rt = (ChituRedemptionType)redemptionType.Value;
            query = query.Where(p => p.RedemptionType == rt);
        }

        var plans = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var cards = new List<ChituPlanCardDto>();
        foreach (var plan in plans)
        {
            cards.Add(await MapToCardDtoAsync(plan, today, ct));
        }

        return new ChituPlanListResponse
        {
            Items = cards,
            TotalCount = cards.Count,
            ActiveCount = cards.Count(c => c.Status == PlanStatus.Active),
            InactiveCount = cards.Count(c => c.Status != PlanStatus.Active)
        };
    }

    public async Task<ChituPlanDetailDto> GetPlanByIdAsync(
        Guid currentStaffId,
        Guid planId,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.Slots)
                .ThenInclude(s => s.Instalments)
            .Include(p => p.Draws)
                .ThenInclude(d => d.WinningSlot)
                    .ThenInclude(ws => ws!.Customer)
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to view this Chitu plan.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cardDto = await MapToCardDtoAsync(plan, today, ct);

        var schedule = new List<ChituPlanScheduleRoundDto>();
        for (int r = 1; r <= plan.DurationMonths; r++)
        {
            var scheduledDate = plan.StartDate.AddMonths(r - 1);
            var draw = plan.Draws.FirstOrDefault(d => d.DrawMonthIndex == r);
            var isCompleted = draw != null;
            var isOverdue = !isCompleted && today >= scheduledDate;

            schedule.Add(new ChituPlanScheduleRoundDto
            {
                RoundIndex = r,
                ScheduledRaffleDate = scheduledDate,
                IsCompleted = isCompleted,
                IsOverdue = isOverdue,
                RecordedAtUtc = draw?.RecordedAtUtc,
                WinningSlotId = draw?.WinningSlotId,
                WinningSlotDisplayCode = draw?.WinningSlot?.SlotDisplayCode,
                WinnerCustomerName = draw?.WinningSlot?.Customer?.FullName
            });
        }

        return new ChituPlanDetailDto
        {
            Id = cardDto.Id,
            BranchId = cardDto.BranchId,
            BranchName = cardDto.BranchName,
            Code = cardDto.Code,
            Name = cardDto.Name,
            Description = cardDto.Description,
            RedemptionType = cardDto.RedemptionType,
            DurationMonths = cardDto.DurationMonths,
            MonthlyInstalmentAmount = cardDto.MonthlyInstalmentAmount,
            ChituBenefitValue = cardDto.ChituBenefitValue,
            TotalSlotCapacity = cardDto.TotalSlotCapacity,
            AssignedSlotsCount = cardDto.AssignedSlotsCount,
            StartDate = cardDto.StartDate,
            EndDate = cardDto.EndDate,
            Status = cardDto.Status,
            IsUpcoming = cardDto.IsUpcoming,
            CurrentRoundIndex = cardDto.CurrentRoundIndex,
            CurrentRoundDueDate = cardDto.CurrentRoundDueDate,
            CurrentRoundPaidSlotsCount = cardDto.CurrentRoundPaidSlotsCount,
            CurrentRoundUnpaidSlotsCount = cardDto.CurrentRoundUnpaidSlotsCount,
            WinningSlotsCount = cardDto.WinningSlotsCount,
            ClosedSlotsCount = cardDto.ClosedSlotsCount,
            HasOverdueRaffle = cardDto.HasOverdueRaffle,
            OverdueRaffleRoundIndex = cardDto.OverdueRaffleRoundIndex,
            CreatedAtUtc = cardDto.CreatedAtUtc,
            Schedule = schedule
        };
    }

    #endregion

    #region Slot Assignment

    public async Task<ChituSlotAssignmentPreviewResponse> PreviewAssignmentAsync(
        Guid currentStaffId,
        ChituSlotAssignmentPreviewRequest request,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .Include(p => p.Slots)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{request.PlanId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to assign slots for this plan's branch.");
        }

        if (plan.Status != PlanStatus.Active)
        {
            throw new InvalidOperationException("Slots cannot be assigned to an inactive or closed plan.");
        }

        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new KeyNotFoundException($"Customer '{request.CustomerId}' was not found.");

        if (!customer.IsActive)
        {
            throw new InvalidOperationException("Slots cannot be assigned to an inactive or closed customer account.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Reject new assignments if plan has ended or all raffles recorded
        if (today > plan.EndDate)
        {
            throw new InvalidOperationException("Once the final raffle date has arrived, new slot assignments are rejected.");
        }

        var recordedDrawsCount = await _db.ChituDraws.CountAsync(d => d.ChituPlanId == plan.Id, ct);
        if (recordedDrawsCount >= plan.DurationMonths)
        {
            throw new InvalidOperationException("All raffle draws have already been recorded for this plan. No new slots can be assigned.");
        }

        // Capacity check
        var currentSlotCount = plan.Slots.Count;
        if (currentSlotCount + request.SlotsCount > plan.TotalSlotCapacity)
        {
            throw new InvalidOperationException($"Cannot assign {request.SlotsCount} slots. Plan capacity is {plan.TotalSlotCapacity}, and {currentSlotCount} slots are already assigned ({plan.TotalSlotCapacity - currentSlotCount} remaining).");
        }

        var maxSlotNum = plan.Slots.Any() ? plan.Slots.Max(s => s.SlotNumber) : 0;
        var assignedNumbers = new List<int>();
        var assignedCodes = new List<string>();

        for (int i = 1; i <= request.SlotsCount; i++)
        {
            var num = maxSlotNum + i;
            assignedNumbers.Add(num);
            assignedCodes.Add($"{plan.Code}-{num:D3}");
        }

        // Determine current round and required months
        var currentRoundIndex = DetermineCurrentRoundIndex(plan, today);
        var requiredMonthsPerSlot = currentRoundIndex; // Covers Round 1 through current round
        var totalMonthsPerSlot = requiredMonthsPerSlot + request.AdditionalPrepaidMonths;

        if (totalMonthsPerSlot > plan.DurationMonths)
        {
            throw new ArgumentException($"Total months ({totalMonthsPerSlot}) exceeds plan duration ({plan.DurationMonths} months).", nameof(request.AdditionalPrepaidMonths));
        }

        var requiredAmountPerSlot = totalMonthsPerSlot * plan.MonthlyInstalmentAmount;
        var totalPayableAmount = requiredAmountPerSlot * request.SlotsCount;

        var breakdown = new List<ChituSlotAllocationBreakdownDto>();
        for (int i = 0; i < request.SlotsCount; i++)
        {
            var slotNum = assignedNumbers[i];
            var slotCode = assignedCodes[i];
            var items = new List<ChituInstalmentAllocationItemDto>();

            for (int m = 1; m <= totalMonthsPerSlot; m++)
            {
                items.Add(new ChituInstalmentAllocationItemDto
                {
                    MonthIndex = m,
                    DueDate = plan.StartDate.AddMonths(m - 1),
                    Amount = plan.MonthlyInstalmentAmount
                });
            }

            breakdown.Add(new ChituSlotAllocationBreakdownDto
            {
                SlotNumber = slotNum,
                SlotDisplayCode = slotCode,
                InstalmentsToPay = items,
                TotalForSlot = requiredAmountPerSlot
            });
        }

        return new ChituSlotAssignmentPreviewResponse
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            PlanCode = plan.Code,
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            SlotsCount = request.SlotsCount,
            AssignedSlotNumbers = assignedNumbers,
            AssignedSlotDisplayCodes = assignedCodes,
            CurrentRoundIndex = currentRoundIndex,
            RequiredMonthsPerSlot = requiredMonthsPerSlot,
            AdditionalPrepaidMonths = request.AdditionalPrepaidMonths,
            RequiredAmountPerSlot = requiredAmountPerSlot,
            TotalPayableAmount = totalPayableAmount,
            AllocationBreakdown = breakdown
        };
    }

    public async Task<AssignChituSlotsResponse> AssignSlotsAsync(
        Guid currentStaffId,
        AssignChituSlotsRequest request,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        // Check idempotency if key provided
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingPayment = await _db.Payments
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey.Trim(), ct);

            if (existingPayment != null && existingPayment.Status == PaymentStatus.Succeeded)
            {
                if (existingPayment.CustomerId != request.CustomerId || existingPayment.Module != BusinessModule.Chitu)
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed with different parameters.");
                }

                var existingSlots = await _db.ChituSlots
                    .Include(s => s.ChituPlan)
                    .Include(s => s.Customer)
                    .Include(s => s.Instalments)
                    .Where(s => s.Instalments.Any(i => i.PaymentId == existingPayment.Id))
                    .OrderBy(s => s.SlotNumber)
                    .ThenBy(s => s.Id)
                    .ToListAsync(ct);

                if (existingSlots.Any(s => s.ChituPlanId != request.PlanId))
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed for a different plan.");
                }

                if (existingSlots.Count != request.SlotsCount)
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed with a different slot count.");
                }

                return new AssignChituSlotsResponse
                {
                    PaymentId = existingPayment.Id,
                    TotalAmountPaid = existingPayment.Amount,
                    CreatedSlots = existingSlots.OrderBy(s => s.SlotNumber).ThenBy(s => s.Id).Select(MapToSlotDto).ToList(),
                    Message = "Slots were previously assigned successfully (Idempotent response)."
                };
            }
        }

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
                        "SELECT 1 FROM \"ChituPlans\" WHERE \"Id\" = {0} FOR UPDATE",
                        new object[] { request.PlanId },
                        ct);
                }

                var plan = await _db.ChituPlans
                    .Include(p => p.Slots)
                    .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
                    ?? throw new KeyNotFoundException($"Chitu plan '{request.PlanId}' was not found.");

                if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
                {
                    throw new UnauthorizedAccessException("You do not have permission to assign slots for this plan's branch.");
                }

                if (plan.Status != PlanStatus.Active)
                {
                    throw new InvalidOperationException("Slots cannot be assigned to an inactive or closed plan.");
                }

                var customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                    ?? throw new KeyNotFoundException($"Customer '{request.CustomerId}' was not found.");

                if (!customer.IsActive)
                {
                    throw new InvalidOperationException("Slots cannot be assigned to an inactive or closed customer account.");
                }

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (today > plan.EndDate)
                {
                    throw new InvalidOperationException("Once the final raffle date has arrived, new slot assignments are rejected.");
                }

                var recordedDrawsCount = await _db.ChituDraws.CountAsync(d => d.ChituPlanId == plan.Id, ct);
                if (recordedDrawsCount >= plan.DurationMonths)
                {
                    throw new InvalidOperationException("All raffle draws have already been recorded. No new slots can be assigned.");
                }

                // Capacity validation
                var currentSlotCount = plan.Slots.Count;
                if (currentSlotCount + request.SlotsCount > plan.TotalSlotCapacity)
                {
                    throw new InvalidOperationException($"Cannot assign {request.SlotsCount} slots. Plan capacity is {plan.TotalSlotCapacity}, and {currentSlotCount} slots are already assigned ({plan.TotalSlotCapacity - currentSlotCount} remaining).");
                }

                var currentRoundIndex = DetermineCurrentRoundIndex(plan, today);
                var requiredMonths = currentRoundIndex;
                var totalMonthsToPay = requiredMonths + request.AdditionalPrepaidMonths;

                if (totalMonthsToPay > plan.DurationMonths)
                {
                    throw new ArgumentException($"Total months ({totalMonthsToPay}) exceeds duration ({plan.DurationMonths}).");
                }

                var totalAmountPaid = totalMonthsToPay * plan.MonthlyInstalmentAmount * request.SlotsCount;

                // Create Payment record
                var payment = new Payment
                {
                    BranchId = plan.BranchId,
                    CustomerId = customer.Id,
                    Module = BusinessModule.Chitu,
                    Amount = totalAmountPaid,
                    Currency = "LKR",
                    Method = request.PaymentMethod,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = request.ReferenceNumber?.Trim() ?? string.Empty,
                    IdempotencyKey = request.IdempotencyKey?.Trim() ?? string.Empty,
                    RecordedByStaffId = currentStaffId,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Remarks = $"Initial assignment of {request.SlotsCount} slots for plan {plan.Code}. {request.Notes}"
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);

                var maxSlotNum = plan.Slots.Any() ? plan.Slots.Max(s => s.SlotNumber) : 0;
                var createdSlots = new List<ChituSlot>();

                for (int i = 1; i <= request.SlotsCount; i++)
                {
                    var slotNum = maxSlotNum + i;
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
                    await _db.SaveChangesAsync(ct); // Save slot to get slot.Id

                    for (int m = 1; m <= plan.DurationMonths; m++)
                    {
                        var isPaid = m <= totalMonthsToPay;
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
                }

                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);

                await _auditService.LogActionAsync(
                    "Staff",
                    currentStaffId,
                    "AssignChituSlots",
                    "ChituPlan",
                    plan.Id.ToString(),
                    plan.BranchId,
                    null,
                    new
                    {
                        CustomerId = customer.Id,
                        SlotsCount = request.SlotsCount,
                        SlotCodes = createdSlots.Select(s => s.SlotDisplayCode).ToList(),
                        TotalAmountPaid = totalAmountPaid,
                        PaymentId = payment.Id
                    },
                    ipAddress);

                return new AssignChituSlotsResponse
                {
                    PaymentId = payment.Id,
                    TotalAmountPaid = totalAmountPaid,
                    CreatedSlots = createdSlots.OrderBy(s => s.SlotNumber).ThenBy(s => s.Id).Select(s => new ChituSlotDto
                    {
                        Id = s.Id,
                        PlanId = plan.Id,
                        PlanCode = plan.Code,
                        PlanName = plan.Name,
                        CustomerId = customer.Id,
                        CustomerName = customer.FullName,
                        SlotNumber = s.SlotNumber,
                        SlotDisplayCode = s.SlotDisplayCode,
                        Status = s.Status,
                        MonthsPaidCount = totalMonthsToPay,
                        TotalPaidAmount = totalMonthsToPay * plan.MonthlyInstalmentAmount,
                        ActivatedAtUtc = s.ActivatedAtUtc
                    }).ToList(),
                    Message = $"Successfully assigned {request.SlotsCount} slots to {customer.FullName}."
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

    #region Payments

    public async Task<CustomerPayableSlotsResponse> GetCustomerPayableSlotsAsync(
        Guid currentStaffId,
        Guid planId,
        Guid customerId,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission to view slots for this plan's branch.");
        }

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        var slots = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.Instalments)
            .Where(s => s.ChituPlanId == planId && s.CustomerId == customerId && s.Status == ChituSlotStatus.Active)
            .OrderBy(s => s.SlotNumber)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentRound = DetermineCurrentRoundIndex(plan, today);

        var payableSlots = slots.Select(s =>
        {
            var unpaid = s.Instalments
                .Where(i => i.Status != InstalmentStatus.Paid)
                .OrderBy(i => i.MonthIndex)
                .Select(i => new ChituInstalmentDto
                {
                    Id = i.Id,
                    MonthIndex = i.MonthIndex,
                    DueDate = i.DueDate,
                    AmountDue = i.AmountDue,
                    Status = i.Status,
                    PaidAtUtc = i.PaidAtUtc
                })
                .ToList();

            var paidCount = s.Instalments.Count(i => i.Status == InstalmentStatus.Paid);
            var isPaidForCurrent = s.Instalments.Any(i => i.MonthIndex == currentRound && i.Status == InstalmentStatus.Paid);

            return new ChituPayableSlotDto
            {
                SlotId = s.Id,
                SlotNumber = s.SlotNumber,
                SlotDisplayCode = s.SlotDisplayCode,
                Status = s.Status,
                NextUnpaidMonthIndex = unpaid.FirstOrDefault()?.MonthIndex ?? (plan.DurationMonths + 1),
                TotalUnpaidMonthsRemaining = unpaid.Count,
                InstalmentsPaidCount = paidCount,
                TotalConfirmedPaid = paidCount * plan.MonthlyInstalmentAmount,
                IsPaidForCurrentRound = isPaidForCurrent,
                UnpaidInstalments = unpaid
            };
        }).ToList();

        return new CustomerPayableSlotsResponse
        {
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            PlanId = plan.Id,
            PlanName = plan.Name,
            PlanCode = plan.Code,
            MonthlyInstalmentAmount = plan.MonthlyInstalmentAmount,
            CurrentRoundIndex = currentRound,
            Slots = payableSlots
        };
    }

    public async Task<ChituPaymentPreviewResponse> PreviewPaymentAsync(
        Guid currentStaffId,
        ChituPaymentPreviewRequest request,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{request.PlanId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission for this plan's branch.");
        }

        if (request.SlotAllocations == null || !request.SlotAllocations.Any())
        {
            throw new ArgumentException("At least one slot allocation is required.", nameof(request.SlotAllocations));
        }

        var breakdowns = new List<ChituSlotPaymentPreviewBreakdownDto>();
        decimal totalAmount = 0m;

        foreach (var alloc in request.SlotAllocations)
        {
            if (alloc.Amount <= 0)
            {
                throw new ArgumentException($"Allocation for slot must be greater than zero.");
            }

            // FULL PAYMENTS ONLY: must be a whole multiple of the monthly slot price
            if (alloc.Amount % plan.MonthlyInstalmentAmount != 0)
            {
                throw new ArgumentException($"Amount {alloc.Amount:N2} is invalid. Payments must be whole multiples of the monthly slot price (LKR {plan.MonthlyInstalmentAmount:N2}). No partial instalments allowed.");
            }

            var slot = await _db.ChituSlots
                .Include(s => s.Instalments)
                .FirstOrDefaultAsync(s => s.Id == alloc.SlotId && s.ChituPlanId == plan.Id && s.CustomerId == request.CustomerId, ct)
                ?? throw new KeyNotFoundException($"Slot '{alloc.SlotId}' was not found or does not belong to this customer and plan.");

            if (slot.Status != ChituSlotStatus.Active)
            {
                throw new InvalidOperationException($"Slot '{slot.SlotDisplayCode}' is not active (Status: {slot.Status}). Cannot record payments for closed or winning slots.");
            }

            var monthsToCover = (int)(alloc.Amount / plan.MonthlyInstalmentAmount);

            // Oldest-unpaid-first: order unpaid instalments by MonthIndex
            var unpaidInstalments = slot.Instalments
                .Where(i => i.Status != InstalmentStatus.Paid)
                .OrderBy(i => i.MonthIndex)
                .ToList();

            if (monthsToCover > unpaidInstalments.Count)
            {
                throw new ArgumentException($"Payment of LKR {alloc.Amount:N2} ({monthsToCover} months) exceeds remaining unpaid obligations ({unpaidInstalments.Count} months) for slot {slot.SlotDisplayCode}.");
            }

            var cleared = unpaidInstalments.Take(monthsToCover).Select(i => new ChituInstalmentDto
            {
                Id = i.Id,
                MonthIndex = i.MonthIndex,
                DueDate = i.DueDate,
                AmountDue = i.AmountDue,
                Status = InstalmentStatus.Paid,
                PaidAtUtc = DateTimeOffset.UtcNow
            }).ToList();

            totalAmount += alloc.Amount;
            breakdowns.Add(new ChituSlotPaymentPreviewBreakdownDto
            {
                SlotId = slot.Id,
                SlotDisplayCode = slot.SlotDisplayCode,
                Amount = alloc.Amount,
                MonthsCovered = monthsToCover,
                ClearedInstalments = cleared
            });
        }

        return new ChituPaymentPreviewResponse
        {
            TotalAmount = totalAmount,
            Slots = breakdowns
        };
    }

    public async Task<RecordChituPaymentResponse> RecordPaymentAsync(
        Guid currentStaffId,
        RecordChituPaymentRequest request,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingPayment = await _db.Payments
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey.Trim(), ct);

            if (existingPayment != null && existingPayment.Status == PaymentStatus.Succeeded)
            {
                if (existingPayment.CustomerId != request.CustomerId || existingPayment.Module != BusinessModule.Chitu)
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed with different parameters.");
                }

                var paidInstalments = await _db.ChituInstalments
                    .Include(i => i.ChituSlot)
                    .Where(i => i.PaymentId == existingPayment.Id)
                    .ToListAsync(ct);

                if (paidInstalments.Any(i => i.ChituSlot?.ChituPlanId != request.PlanId))
                {
                    throw new ChituIdempotencyConflictException("An existing operation with the same idempotency key was executed for a different plan.");
                }

                var receipts = paidInstalments
                    .GroupBy(i => i.ChituSlotId)
                    .OrderBy(g => g.First().ChituSlot?.SlotNumber ?? 0)
                    .ThenBy(g => g.Key)
                    .Select(g => new ChituSlotPaymentReceiptItemDto
                    {
                        SlotId = g.Key,
                        SlotDisplayCode = g.First().ChituSlot?.SlotDisplayCode ?? string.Empty,
                        AmountAllocated = g.Sum(i => i.AmountDue),
                        MonthsCovered = g.Count(),
                        ClearedMonthIndices = g.Select(i => i.MonthIndex).OrderBy(m => m).ToList()
                    })
                    .ToList();

                return new RecordChituPaymentResponse
                {
                    PaymentId = existingPayment.Id,
                    TotalAmount = existingPayment.Amount,
                    PaidAtUtc = existingPayment.CompletedAtUtc ?? existingPayment.CreatedAtUtc,
                    PaymentMethod = existingPayment.Method.ToString(),
                    SlotReceipts = receipts,
                    Message = "Payment was previously recorded successfully (Idempotent response)."
                };
            }
        }

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
                    var slotIds = request.SlotAllocations.Select(a => a.SlotId).Distinct().ToArray();
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"ChituSlots\" WHERE \"Id\" = ANY({0}) FOR UPDATE",
                        new object[] { slotIds },
                        ct);
                }

                var plan = await _db.ChituPlans
                    .FirstOrDefaultAsync(p => p.Id == request.PlanId, ct)
                    ?? throw new KeyNotFoundException($"Chitu plan '{request.PlanId}' was not found.");

                if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
                {
                    throw new UnauthorizedAccessException("You do not have permission for this plan's branch.");
                }

                var customer = await _db.Customers
                    .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                    ?? throw new KeyNotFoundException($"Customer '{request.CustomerId}' was not found.");

                if (!customer.IsActive)
                {
                    throw new InvalidOperationException("Cannot record payment for an inactive or closed customer account.");
                }

                decimal totalPaymentAmount = 0m;
                var validatedAllocations = new List<(ChituSlot Slot, int MonthsToCover, List<ChituInstalment> InstalmentsToClear, decimal Amount)>();

                foreach (var alloc in request.SlotAllocations)
                {
                    if (alloc.Amount <= 0)
                    {
                        throw new ArgumentException("Allocation amount must be greater than zero.");
                    }

                    if (alloc.Amount % plan.MonthlyInstalmentAmount != 0)
                    {
                        throw new ArgumentException($"Amount {alloc.Amount:N2} is invalid. Payments must be whole multiples of the monthly slot price (LKR {plan.MonthlyInstalmentAmount:N2}).");
                    }

                    var slot = await _db.ChituSlots
                        .Include(s => s.Instalments)
                        .FirstOrDefaultAsync(s => s.Id == alloc.SlotId && s.ChituPlanId == plan.Id && s.CustomerId == customer.Id, ct)
                        ?? throw new KeyNotFoundException($"Slot '{alloc.SlotId}' was not found.");

                    if (slot.Status != ChituSlotStatus.Active)
                    {
                        throw new InvalidOperationException($"Slot '{slot.SlotDisplayCode}' is not active.");
                    }

                    var monthsToCover = (int)(alloc.Amount / plan.MonthlyInstalmentAmount);
                    var unpaidInstalments = slot.Instalments
                        .Where(i => i.Status != InstalmentStatus.Paid)
                        .OrderBy(i => i.MonthIndex)
                        .ToList();

                    if (alloc.ExpectedStartingMonthIndex.HasValue && unpaidInstalments.FirstOrDefault()?.MonthIndex != alloc.ExpectedStartingMonthIndex.Value)
                    {
                        throw new ChituPreviewExpiredOrChangedException($"The target unpaid instalment for slot '{slot.SlotDisplayCode}' has already been paid. Money will not be shifted to a future month.");
                    }

                    if (monthsToCover > unpaidInstalments.Count)
                    {
                        throw new ArgumentException($"Payment of LKR {alloc.Amount:N2} exceeds remaining unpaid obligations for slot {slot.SlotDisplayCode}.");
                    }

                    var instalmentsToClear = unpaidInstalments.Take(monthsToCover).ToList();
                    totalPaymentAmount += alloc.Amount;
                    validatedAllocations.Add((slot, monthsToCover, instalmentsToClear, alloc.Amount));
                }

                var payment = new Payment
                {
                    BranchId = plan.BranchId,
                    CustomerId = customer.Id,
                    Module = BusinessModule.Chitu,
                    Amount = totalPaymentAmount,
                    Currency = "LKR",
                    Method = request.PaymentMethod,
                    Status = PaymentStatus.Succeeded,
                    ProviderReference = request.ReferenceNumber?.Trim() ?? string.Empty,
                    IdempotencyKey = request.IdempotencyKey?.Trim() ?? string.Empty,
                    RecordedByStaffId = currentStaffId,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Remarks = $"Chitu payment recorded for {validatedAllocations.Count} slots. {request.Notes}"
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);

                var receipts = new List<ChituSlotPaymentReceiptItemDto>();

                foreach (var (slot, monthsToCover, instalmentsToClear, amount) in validatedAllocations)
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

                    receipts.Add(new ChituSlotPaymentReceiptItemDto
                    {
                        SlotId = slot.Id,
                        SlotDisplayCode = slot.SlotDisplayCode,
                        AmountAllocated = amount,
                        MonthsCovered = monthsToCover,
                        ClearedMonthIndices = clearedIndices
                    });
                }

                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);

                await _auditService.LogActionAsync(
                    "Staff",
                    currentStaffId,
                    "RecordChituPayment",
                    "Payment",
                    payment.Id.ToString(),
                    plan.BranchId,
                    null,
                    new
                    {
                        PaymentId = payment.Id,
                        TotalAmount = totalPaymentAmount,
                        Method = request.PaymentMethod.ToString(),
                        SlotsCount = receipts.Count
                    },
                    ipAddress);

                return new RecordChituPaymentResponse
                {
                    PaymentId = payment.Id,
                    TotalAmount = totalPaymentAmount,
                    PaidAtUtc = payment.CompletedAtUtc!.Value,
                    PaymentMethod = payment.Method.ToString(),
                    SlotReceipts = receipts,
                    Message = $"Successfully recorded payment of LKR {totalPaymentAmount:N2} for {receipts.Count} slots."
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

    #region Physical Raffle Draws

    public async Task<ChituEligibleSlotsResponse> GetEligibleRaffleSlotsAsync(
        Guid currentStaffId,
        Guid planId,
        int roundIndex,
        string? search = null,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission for this plan's branch.");
        }

        if (roundIndex < 1 || roundIndex > plan.DurationMonths)
        {
            throw new ArgumentOutOfRangeException(nameof(roundIndex), $"Round index must be between 1 and {plan.DurationMonths}.");
        }

        var scheduledDate = plan.StartDate.AddMonths(roundIndex - 1);

        // Fetch slots belonging to this plan with customer and instalments
        var slots = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Instalments)
            .Where(s => s.ChituPlanId == planId &&
                        s.Status == ChituSlotStatus.Active &&
                        s.WinningRoundIndex == null &&
                        s.Customer != null && s.Customer.IsActive)
            .ToListAsync(ct);

        // Filter slots where EVERY instalment from Round 1 through RoundIndex is fully paid
        var eligibleSlots = slots
            .Where(s =>
            {
                // Check month indices 1 to roundIndex
                for (int m = 1; m <= roundIndex; m++)
                {
                    var inst = s.Instalments.FirstOrDefault(i => i.MonthIndex == m);
                    if (inst == null || inst.Status != InstalmentStatus.Paid)
                    {
                        return false;
                    }
                }
                return true;
            })
            .Select(s => new ChituEligibleSlotDto
            {
                SlotId = s.Id,
                SlotNumber = s.SlotNumber,
                SlotDisplayCode = s.SlotDisplayCode,
                CustomerId = s.CustomerId,
                CustomerName = s.Customer?.FullName ?? "Unknown",
                CustomerPhone = s.Customer?.PhoneNumber,
                CustomerNic = s.Customer?.Nic,
                TotalConfirmedPaid = s.Instalments.Where(i => i.Status == InstalmentStatus.Paid).Sum(i => i.AmountDue),
                MonthsPaidCount = s.Instalments.Count(i => i.Status == InstalmentStatus.Paid)
            })
            .OrderBy(s => s.SlotNumber)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            var cleanNumStr = trimmed.StartsWith("slot", StringComparison.OrdinalIgnoreCase)
                ? trimmed[4..].Trim()
                : (trimmed.StartsWith('#') ? trimmed[1..].Trim() : trimmed);

            bool isNumericQuery = int.TryParse(cleanNumStr, out var queryNum);

            eligibleSlots = eligibleSlots.Where(s =>
            {
                // Customer name
                if (s.CustomerName.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (isNumericQuery)
                {
                    // Exact slot number (e.g. "1", "001", "slot 1", "#1")
                    if (s.SlotNumber == queryNum)
                        return true;

                    // For numbers with 4+ digits, check display code, phone, or NIC
                    if (cleanNumStr.Length >= 4)
                    {
                        if (s.SlotDisplayCode.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                            return true;

                        if (s.CustomerPhone != null && s.CustomerPhone.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                            return true;

                        if (s.CustomerNic != null && s.CustomerNic.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                else
                {
                    // Text query: match display code, phone, or NIC
                    if (s.SlotDisplayCode.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (s.CustomerPhone != null && s.CustomerPhone.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (s.CustomerNic != null && s.CustomerNic.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }).ToList();
        }

        return new ChituEligibleSlotsResponse
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            RoundIndex = roundIndex,
            ScheduledRaffleDate = scheduledDate,
            EligibleCount = eligibleSlots.Count,
            EligibleSlots = eligibleSlots
        };
    }

    public async Task<RecordChituRaffleResponse> RecordRaffleDrawAsync(
        Guid currentStaffId,
        Guid planId,
        int roundIndex,
        RecordChituRaffleRequest request,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

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
                        "SELECT 1 FROM \"ChituPlans\" WHERE \"Id\" = {0} FOR UPDATE",
                        new object[] { planId },
                        ct);
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"ChituSlots\" WHERE \"Id\" = {0} FOR UPDATE",
                        new object[] { request.WinningSlotId },
                        ct);
                }

                var plan = await _db.ChituPlans
                    .Include(p => p.Branch)
                    .FirstOrDefaultAsync(p => p.Id == planId, ct)
                    ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

            if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
            {
                throw new UnauthorizedAccessException("You do not have permission for this plan's branch.");
            }

            if (roundIndex < 1 || roundIndex > plan.DurationMonths)
            {
                throw new ArgumentOutOfRangeException(nameof(roundIndex), $"Round index must be between 1 and {plan.DurationMonths}.");
            }

            var scheduledDate = plan.StartDate.AddMonths(roundIndex - 1);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Chronological order enforcement
            if (roundIndex > 1)
            {
                var priorUnrecordedRound = await _db.ChituDraws
                    .Where(d => d.ChituPlanId == planId && d.DrawMonthIndex < roundIndex)
                    .Select(d => d.DrawMonthIndex)
                    .ToListAsync(ct);

                for (int prev = 1; prev < roundIndex; prev++)
                {
                    if (!priorUnrecordedRound.Contains(prev))
                    {
                        throw new InvalidOperationException($"Raffles must be recorded in chronological order. Round {prev} has not been recorded yet.");
                    }
                }
            }

            // Check if already recorded
            var existingDraw = await _db.ChituDraws
                .FirstOrDefaultAsync(d => d.ChituPlanId == planId && d.DrawMonthIndex == roundIndex, ct);

            if (existingDraw != null)
            {
                throw new InvalidOperationException($"A raffle draw result has already been recorded for Round {roundIndex}.");
            }

            // Scheduled date check: cannot record before scheduled date
            if (today < scheduledDate)
            {
                throw new InvalidOperationException($"Cannot record raffle result before its scheduled date ({scheduledDate:yyyy-MM-dd}).");
            }

            // Re-validate winning slot inside transaction
            var winningSlot = await _db.ChituSlots
                .Include(s => s.Customer)
                .Include(s => s.Instalments)
                .FirstOrDefaultAsync(s => s.Id == request.WinningSlotId && s.ChituPlanId == plan.Id, ct)
                ?? throw new KeyNotFoundException($"Winning slot '{request.WinningSlotId}' was not found for this plan.");

            if (winningSlot.Status != ChituSlotStatus.Active || winningSlot.WinningRoundIndex != null)
            {
                throw new InvalidOperationException($"Slot '{winningSlot.SlotDisplayCode}' is not eligible (Status: {winningSlot.Status}). Already won or closed.");
            }

            if (winningSlot.Customer == null || !winningSlot.Customer.IsActive)
            {
                throw new InvalidOperationException("Slot's customer account is inactive or closed.");
            }

            // Verify all instalments from Round 1 through roundIndex are paid
            for (int m = 1; m <= roundIndex; m++)
            {
                var inst = winningSlot.Instalments.FirstOrDefault(i => i.MonthIndex == m);
                if (inst == null || inst.Status != InstalmentStatus.Paid)
                {
                    throw new InvalidOperationException($"Slot '{winningSlot.SlotDisplayCode}' is ineligible: instalment for month {m} is not fully paid.");
                }
            }

            // Create ChituDraw
            var draw = new ChituDraw
            {
                ChituPlanId = plan.Id,
                DrawMonthIndex = roundIndex,
                DrawDate = scheduledDate,
                WinningSlotId = winningSlot.Id,
                RecordedByStaffId = currentStaffId,
                RecordedAtUtc = DateTimeOffset.UtcNow,
                ReferenceNumber = request.ReferenceNumber?.Trim(),
                Remarks = request.Remarks?.Trim() ?? string.Empty
            };

            _db.ChituDraws.Add(draw);
            await _db.SaveChangesAsync(ct);

            // Winner Effect:
            // 1. Mark slot as Won and record winning round index
            winningSlot.Status = ChituSlotStatus.Won;
            winningSlot.WinningRoundIndex = roundIndex;

            // 2. Stop future payment obligations for months > roundIndex
            // If future months were prepaid, sum them as advance excess
            decimal prepaidExcess = 0m;
            foreach (var inst in winningSlot.Instalments.Where(i => i.MonthIndex > roundIndex))
            {
                if (inst.Status == InstalmentStatus.Paid)
                {
                    prepaidExcess += inst.AmountDue;
                }
                else
                {
                    // Obligation ended
                    inst.Status = InstalmentStatus.Pending; // Keep pending or mark obligation ended
                }
            }

            winningSlot.PrepaidExcessAmount = prepaidExcess;

            // 3. Create WinnerBenefit entitlement
            var entitlementValue = plan.MonthlyInstalmentAmount * plan.DurationMonths;
            var benefit = new WinnerBenefit
            {
                ChituDrawId = draw.Id,
                ChituSlotId = winningSlot.Id,
                CustomerId = winningSlot.CustomerId,
                ChituBenefitValue = entitlementValue,
                AdvanceExcessAmount = prepaidExcess,
                RedemptionType = plan.RedemptionType,
                Status = BenefitStatus.Unclaimed
            };

            _db.WinnerBenefits.Add(benefit);

            // Check if this was the final round of the plan
            if (roundIndex >= plan.DurationMonths)
            {
                plan.Status = PlanStatus.Closed;
            }

            // Winner Notification and Plan Alert (Atomic with winner record)
            var scheduledMonthYear = scheduledDate.ToString("MMMM yyyy");
            var prizeTypeName = plan.RedemptionType == ChituRedemptionType.Gold ? "gold" : "cash";
            var branchName = plan.Branch?.Name ?? "our branch";
            var winnerTitle = "Chitu Raffle Winner!";
            var winnerMessage = $"Congratulations! Your slot {winningSlot.SlotNumber} in {plan.Name} has won Round {roundIndex} for {scheduledMonthYear}. Visit {branchName} to collect your {prizeTypeName} prize.";

            var notification = new Notification
            {
                CustomerId = winningSlot.CustomerId,
                Title = winnerTitle,
                Message = winnerMessage,
                Type = "chitu_winner",
                IsRead = false,
                ReferenceId = plan.Id,
                ReferenceType = "ChituInvestment",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.Notifications.Add(notification);

            var alert = new PlanAlert
            {
                CustomerId = winningSlot.CustomerId,
                EventType = "ChituWinner",
                Module = BusinessModule.Chitu,
                Title = winnerTitle,
                Message = winnerMessage,
                PlanId = plan.Id,
                PlanNameSnapshot = plan.Name,
                ReferenceId = plan.Id,
                ChituDrawId = draw.Id,
                ChituSlotId = winningSlot.Id,
                ChituSlotNumber = winningSlot.SlotNumber,
                ChituSlotDisplayCode = winningSlot.SlotDisplayCode,
                ChituRoundIndex = roundIndex,
                ChituRoundScheduledDate = scheduledDate,
                ChituRoundScheduledMonthYear = scheduledMonthYear,
                RedemptionType = plan.RedemptionType,
                PrizeAmount = entitlementValue,
                BranchId = plan.BranchId,
                BranchName = branchName,
                BranchAddress = plan.Branch?.Address,
                BranchPhone = null,
                NotificationId = notification.Id,
                IsAcknowledged = false,
                NavigationTarget = $"chitu-investments/{plan.Id}",
                CollectionInstruction = $"Visit {branchName} to collect your {prizeTypeName} prize.",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.PlanAlerts.Add(alert);
            notification.PlanAlertId = alert.Id;

            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);

            await _auditService.LogActionAsync(
                "Staff",
                currentStaffId,
                "RecordChituRaffleDraw",
                "ChituDraw",
                draw.Id.ToString(),
                plan.BranchId,
                null,
                new
                {
                    DrawId = draw.Id,
                    RoundIndex = roundIndex,
                    WinningSlotCode = winningSlot.SlotDisplayCode,
                    CustomerId = winningSlot.CustomerId,
                    EntitlementValue = entitlementValue,
                    AdvanceExcessAmount = prepaidExcess
                },
                ipAddress);

            return new RecordChituRaffleResponse
            {
                DrawId = draw.Id,
                PlanId = plan.Id,
                RoundIndex = roundIndex,
                ScheduledDate = scheduledDate,
                RecordedAtUtc = draw.RecordedAtUtc,
                WinningSlotId = winningSlot.Id,
                WinningSlotDisplayCode = winningSlot.SlotDisplayCode,
                CustomerId = winningSlot.CustomerId,
                CustomerName = winningSlot.Customer?.FullName ?? "Unknown",
                WinningEntitlementValue = entitlementValue,
                AdvanceExcessPrepaidAmount = prepaidExcess,
                BenefitStatus = benefit.Status,
                Message = $"Successfully recorded Round {roundIndex} physical raffle winner: {winningSlot.SlotDisplayCode} ({winningSlot.Customer?.FullName})."
            };
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                if (ex is DbUpdateException dbEx &&
                    (dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation ||
                     dbEx.InnerException?.Message.Contains("23505") == true ||
                     dbEx.InnerException?.Message.Contains("ChituDraw") == true))
                {
                    throw new InvalidOperationException($"A raffle draw result has already been recorded for Round {roundIndex}.");
                }
                throw;
            }
        });
    }

    #endregion

    #region Customer / Slot Roster

    public async Task<ChituRosterResponse> GetRosterAsync(
        Guid currentStaffId,
        Guid planId,
        ChituRosterQueryParameters query,
        CancellationToken ct = default)
    {
        var (caller, isSuperAdmin, permittedBranchIds) = await VerifyStaffAccessAsync(currentStaffId, ct);

        var plan = await _db.ChituPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new KeyNotFoundException($"Chitu plan '{planId}' was not found.");

        if (!isSuperAdmin && !permittedBranchIds.Contains(plan.BranchId))
        {
            throw new UnauthorizedAccessException("You do not have permission for this plan's branch.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentRound = DetermineCurrentRoundIndex(plan, today);
        var selectedRound = query.RoundIndex ?? currentRound;

        if (selectedRound < 1) selectedRound = 1;
        if (selectedRound > plan.DurationMonths) selectedRound = plan.DurationMonths;

        // Fetch all slots for this plan with customer, instalments, and winner benefits
        var slots = await _db.ChituSlots
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Instalments)
            .Include(s => s.WinnerBenefit)
            .Where(s => s.ChituPlanId == planId)
            .OrderBy(s => s.SlotNumber)
            .ToListAsync(ct);

        // Database-derived metrics across all slots for selected round
        int totalAssigned = slots.Count;
        int activeSlots = slots.Count(s => s.Status == ChituSlotStatus.Active);
        int winningSlots = slots.Count(s => s.Status == ChituSlotStatus.Won);
        int closedSlots = slots.Count(s => s.Status == ChituSlotStatus.Closed || s.Status == ChituSlotStatus.Cancelled);

        // Slots eligible for payment in selected round (exclude slots that won in an earlier round w < selectedRound)
        var roundEligibleSlots = slots.Where(s => !(s.Status == ChituSlotStatus.Won && s.WinningRoundIndex.HasValue && s.WinningRoundIndex.Value < selectedRound)).ToList();

        int roundPaidSlots = roundEligibleSlots.Count(s => s.Instalments.Any(i => i.MonthIndex == selectedRound && i.Status == InstalmentStatus.Paid));
        int roundUnpaidSlots = roundEligibleSlots.Count - roundPaidSlots;

        // Build roster row DTOs
        var rows = new List<ChituRosterRowDto>();
        foreach (var s in slots)
        {
            var paidInstalments = s.Instalments.Where(i => i.Status == InstalmentStatus.Paid).ToList();
            var paidCount = paidInstalments.Count;
            var paidThroughMonth = paidInstalments.Any() ? paidInstalments.Max(i => i.MonthIndex) : (int?)null;
            var isPaidForSelected = s.Instalments.Any(i => i.MonthIndex == selectedRound && i.Status == InstalmentStatus.Paid);

            // Is fully paid through selected round (1 to selectedRound)
            bool isFullyPaidThrough = true;
            for (int m = 1; m <= selectedRound; m++)
            {
                if (!s.Instalments.Any(i => i.MonthIndex == m && i.Status == InstalmentStatus.Paid))
                {
                    isFullyPaidThrough = false;
                    break;
                }
            }

            var outstandingDue = s.Instalments.Count(i => i.MonthIndex <= selectedRound && i.Status != InstalmentStatus.Paid);
            var totalConfirmedPaid = paidInstalments.Sum(i => i.AmountDue);

            rows.Add(new ChituRosterRowDto
            {
                SlotId = s.Id,
                SlotNumber = s.SlotNumber,
                SlotDisplayCode = s.SlotDisplayCode,
                CustomerId = s.CustomerId,
                CustomerName = s.Customer?.FullName ?? "Unknown",
                CustomerPhone = s.Customer?.PhoneNumber,
                SlotStatus = s.Status,
                IsPaidForSelectedRound = isPaidForSelected,
                IsFullyPaidThroughSelectedRound = isFullyPaidThrough,
                InstalmentsPaidCount = paidCount,
                PaidThroughMonthIndex = paidThroughMonth,
                OutstandingDueMonthsCount = outstandingDue,
                TotalConfirmedPaid = totalConfirmedPaid,
                IsWinner = s.Status == ChituSlotStatus.Won,
                WinningRoundIndex = s.WinningRoundIndex,
                EntitlementAmount = s.WinnerBenefit?.ChituBenefitValue,
                AdvanceExcessAmount = s.WinnerBenefit?.AdvanceExcessAmount ?? s.PrepaidExcessAmount
            });
        }

        // Apply filters
        var filteredRows = rows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            filteredRows = filteredRows.Where(r =>
                r.CustomerName.ToLower().Contains(term) ||
                r.SlotDisplayCode.ToLower().Contains(term) ||
                (r.CustomerPhone != null && r.CustomerPhone.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.StatusFilter) && !query.StatusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (query.StatusFilter.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                filteredRows = filteredRows.Where(r => r.SlotStatus == ChituSlotStatus.Active);
            }
            else if (query.StatusFilter.Equals("Won", StringComparison.OrdinalIgnoreCase))
            {
                filteredRows = filteredRows.Where(r => r.SlotStatus == ChituSlotStatus.Won);
            }
            else if (query.StatusFilter.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            {
                filteredRows = filteredRows.Where(r => r.SlotStatus == ChituSlotStatus.Closed || r.SlotStatus == ChituSlotStatus.Cancelled);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentFilter) && !query.PaymentFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (query.PaymentFilter.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                filteredRows = filteredRows.Where(r => r.IsPaidForSelectedRound);
            }
            else if (query.PaymentFilter.Equals("Unpaid", StringComparison.OrdinalIgnoreCase))
            {
                filteredRows = filteredRows.Where(r => !r.IsPaidForSelectedRound);
            }
        }

        var totalMatching = filteredRows.Count();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var pagedRows = filteredRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ChituRosterResponse
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            PlanCode = plan.Code,
            DurationMonths = plan.DurationMonths,
            SelectedRoundIndex = selectedRound,
            MaxSlots = plan.TotalSlotCapacity,
            AssignedSlots = totalAssigned,
            ActiveSlotsCount = activeSlots,
            WinningSlotsCount = winningSlots,
            ClosedSlotsCount = closedSlots,
            RoundPaidSlotsCount = roundPaidSlots,
            RoundUnpaidSlotsCount = roundUnpaidSlots,
            Rows = pagedRows,
            TotalCount = totalMatching,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Helper Methods

    private static int DetermineCurrentRoundIndex(ChituPlan plan, DateOnly today)
    {
        if (today < plan.StartDate)
        {
            return 1;
        }

        if (today >= plan.EndDate)
        {
            return plan.DurationMonths;
        }

        for (int r = plan.DurationMonths; r >= 1; r--)
        {
            var scheduledDate = plan.StartDate.AddMonths(r - 1);
            if (today >= scheduledDate)
            {
                return r;
            }
        }

        return 1;
    }

    private async Task<ChituPlanCardDto> MapToCardDtoAsync(ChituPlan plan, DateOnly today, CancellationToken ct)
    {
        var branchName = plan.Branch?.Name;
        if (string.IsNullOrEmpty(branchName))
        {
            var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == plan.BranchId, ct);
            branchName = branch?.Name ?? "Unknown";
        }

        var isUpcoming = today < plan.StartDate;
        var currentRound = DetermineCurrentRoundIndex(plan, today);
        var currentDueDate = plan.StartDate.AddMonths(currentRound - 1);

        var slots = plan.Slots.ToList();
        var draws = plan.Draws.ToList();

        // Check overdue raffle: any round r where today >= scheduledDate and draw is unrecorded
        bool hasOverdue = false;
        int? overdueRound = null;

        for (int r = 1; r <= plan.DurationMonths; r++)
        {
            var rDate = plan.StartDate.AddMonths(r - 1);
            if (today >= rDate && !draws.Any(d => d.DrawMonthIndex == r))
            {
                hasOverdue = true;
                overdueRound = r;
                break;
            }
        }

        // Slots paid for current round (exclude slots that won in an earlier round w < currentRound)
        var eligibleForRound = slots.Where(s => !(s.Status == ChituSlotStatus.Won && s.WinningRoundIndex.HasValue && s.WinningRoundIndex.Value < currentRound)).ToList();

        var paidCount = eligibleForRound.Count(s => s.Instalments.Any(i => i.MonthIndex == currentRound && i.Status == InstalmentStatus.Paid));
        var unpaidCount = eligibleForRound.Count - paidCount;

        var winningCount = slots.Count(s => s.Status == ChituSlotStatus.Won);
        var closedCount = slots.Count(s => s.Status == ChituSlotStatus.Closed || s.Status == ChituSlotStatus.Cancelled);

        return new ChituPlanCardDto
        {
            Id = plan.Id,
            BranchId = plan.BranchId,
            BranchName = branchName,
            Code = plan.Code,
            Name = plan.Name,
            Description = plan.Description,
            RedemptionType = plan.RedemptionType,
            DurationMonths = plan.DurationMonths,
            MonthlyInstalmentAmount = plan.MonthlyInstalmentAmount,
            ChituBenefitValue = plan.ChituBenefitValue,
            TotalSlotCapacity = plan.TotalSlotCapacity,
            AssignedSlotsCount = slots.Count,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            Status = plan.Status,
            IsUpcoming = isUpcoming,
            CurrentRoundIndex = currentRound,
            CurrentRoundDueDate = currentDueDate,
            CurrentRoundPaidSlotsCount = paidCount,
            CurrentRoundUnpaidSlotsCount = unpaidCount,
            WinningSlotsCount = winningCount,
            ClosedSlotsCount = closedCount,
            HasOverdueRaffle = hasOverdue,
            OverdueRaffleRoundIndex = overdueRound,
            CreatedAtUtc = plan.CreatedAtUtc
        };
    }

    private static ChituSlotDto MapToSlotDto(ChituSlot s)
    {
        var paidCount = s.Instalments.Count(i => i.Status == InstalmentStatus.Paid);
        return new ChituSlotDto
        {
            Id = s.Id,
            PlanId = s.ChituPlanId,
            PlanCode = s.ChituPlan?.Code ?? string.Empty,
            PlanName = s.ChituPlan?.Name ?? string.Empty,
            CustomerId = s.CustomerId,
            CustomerName = s.Customer?.FullName ?? string.Empty,
            SlotNumber = s.SlotNumber,
            SlotDisplayCode = s.SlotDisplayCode,
            Status = s.Status,
            MonthsPaidCount = paidCount,
            TotalPaidAmount = paidCount * (s.ChituPlan?.MonthlyInstalmentAmount ?? 0m),
            ActivatedAtUtc = s.ActivatedAtUtc
        };
    }

    private async Task<(Staff Caller, bool IsSuperAdmin, List<Guid> PermittedBranchIds)> VerifyStaffAccessAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var staff = await _db.StaffMembers
            .Include(s => s.Roles).ThenInclude(r => r.Role)
            .Include(s => s.AssignedBranches)
            .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

        if (staff == null || !staff.IsActive)
        {
            throw new UnauthorizedAccessException("Unauthorized staff access or account is inactive.");
        }

        var isSuperAdmin = staff.Roles.Any(r =>
            r.Role?.Name == "Super Admin" ||
            r.Role?.RoleType == StaffRoleType.SuperAdmin);

        var permittedBranchIds = staff.AssignedBranches.Select(b => b.BranchId).ToList();

        return (staff, isSuperAdmin, permittedBranchIds);
    }

    #endregion
}
