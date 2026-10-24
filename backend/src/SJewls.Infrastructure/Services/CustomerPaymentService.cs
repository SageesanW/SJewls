using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly SJewlsDbContext _db;
    private readonly ILogger<CustomerPaymentService> _logger;

    public CustomerPaymentService(
        SJewlsDbContext db,
        ILogger<CustomerPaymentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerPaymentsPagedResponse> GetPaymentHistoryAsync(
        Guid customerId,
        string? planType = null,
        Guid? planId = null,
        string? status = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new UnauthorizedAccessException("Customer record not found.");

        if (!customer.IsActive)
        {
            throw new InvalidOperationException("Customer account is inactive.");
        }

        var query = _db.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .Where(p => p.CustomerId == customerId);

        // Filter: PlanType (Chitu / Jewellery / All)
        if (!string.IsNullOrWhiteSpace(planType) && !planType.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (planType.Equals("Chitu", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Module == BusinessModule.Chitu);
            }
            else if (planType.Equals("Jewellery", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Module == BusinessModule.Jewellery);
            }
        }

        // Filter: Status
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<PaymentStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(p => p.Status == parsedStatus);
            }
        }

        // Filter: Date Range
        if (startDate.HasValue)
        {
            query = query.Where(p => (p.CompletedAtUtc ?? p.CreatedAtUtc) >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(p => (p.CompletedAtUtc ?? p.CreatedAtUtc) <= endDate.Value);
        }

        // Filter: Search (Reference or Remarks)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(p =>
                p.ProviderReference.Contains(trimmedSearch) ||
                (p.Remarks != null && p.Remarks.Contains(trimmedSearch)));
        }

        var totalCount = await query.CountAsync(ct);

        var payments = await query
            .OrderByDescending(p => p.CompletedAtUtc ?? p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Fetch related Chitu / Jewellery entities in batch for mapping
        var paymentIds = payments.Select(p => p.Id).ToList();

        var chituInstalments = await _db.ChituInstalments
            .AsNoTracking()
            .Include(i => i.ChituSlot).ThenInclude(s => s.ChituPlan)
            .Where(i => i.PaymentId.HasValue && paymentIds.Contains(i.PaymentId.Value))
            .ToListAsync(ct);

        var jewelleryContributions = await _db.JewelleryContributions
            .AsNoTracking()
            .Include(c => c.JewelleryEnrolment).ThenInclude(e => e.JewelleryPlan)
            .Where(c => paymentIds.Contains(c.PaymentId))
            .ToListAsync(ct);

        var summaryItems = new List<CustomerPaymentSummaryDto>();

        foreach (var p in payments)
        {
            var isSimulated = p.ProviderReference.StartsWith("SIM-") || (p.Remarks != null && p.Remarks.Contains("[SIMULATED]"));
            var planTypeStr = p.Module == BusinessModule.Chitu ? "Chitu" : "Jewellery";

            Guid? resolvedPlanId = null;
            string resolvedPlanName = string.Empty;
            string resolvedPlanCode = string.Empty;
            int? slotsCovered = null;
            string summaryDesc;

            if (p.Module == BusinessModule.Chitu)
            {
                var relatedInsts = chituInstalments.Where(i => i.PaymentId == p.Id).ToList();
                var firstSlot = relatedInsts.FirstOrDefault()?.ChituSlot;
                var plan = firstSlot?.ChituPlan;

                resolvedPlanId = plan?.Id;
                resolvedPlanName = plan?.Name ?? "Chitu Plan";
                resolvedPlanCode = plan?.Code ?? string.Empty;
                slotsCovered = relatedInsts.Select(i => i.ChituSlotId).Distinct().Count();

                var isJoining = p.Remarks != null && p.Remarks.Contains("Initial joining payment");
                summaryDesc = isJoining
                    ? $"Initial Chitu join: {slotsCovered} slot(s) in {resolvedPlanName}"
                    : $"Chitu payment: {slotsCovered} slot(s) in {resolvedPlanName}";
            }
            else
            {
                var relatedContrib = jewelleryContributions.FirstOrDefault(c => c.PaymentId == p.Id);
                var enrolment = relatedContrib?.JewelleryEnrolment;

                resolvedPlanId = enrolment?.JewelleryPlanId;
                resolvedPlanName = enrolment?.PlanNameSnapshot ?? enrolment?.JewelleryPlan?.Name ?? "Jewellery Plan";
                resolvedPlanCode = enrolment != null ? $"ENR-{enrolment.Id.ToString("N")[..8].ToUpperInvariant()}" : string.Empty;

                summaryDesc = relatedContrib != null
                    ? $"Jewellery contribution: {relatedContrib.GoldGrams:F3}g credited"
                    : $"Jewellery payment: {resolvedPlanName}";
            }

            // If user supplied planId filter, apply it now
            if (planId.HasValue && resolvedPlanId != planId.Value)
            {
                continue;
            }

            summaryItems.Add(new CustomerPaymentSummaryDto
            {
                PaymentId = p.Id,
                PlanType = planTypeStr,
                PlanId = resolvedPlanId,
                PlanName = resolvedPlanName,
                PlanCode = resolvedPlanCode,
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
                SlotsCoveredCount = slotsCovered,
                HasDetails = true,
                SummaryDescription = summaryDesc
            });
        }

        return new CustomerPaymentsPagedResponse
        {
            Items = summaryItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomerPaymentDetailDto> GetPaymentDetailAsync(
        Guid customerId,
        Guid paymentId,
        CancellationToken ct = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new UnauthorizedAccessException("Customer record not found.");

        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new KeyNotFoundException($"Payment '{paymentId}' was not found.");

        if (payment.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You do not own this payment record.");
        }

        var isSimulated = payment.ProviderReference.StartsWith("SIM-") || (payment.Remarks != null && payment.Remarks.Contains("[SIMULATED]"));
        var planTypeStr = payment.Module == BusinessModule.Chitu ? "Chitu" : "Jewellery";

        CustomerChituPaymentDetailBreakdownDto? chituDetails = null;
        CustomerJewelleryPaymentDetailBreakdownDto? jewelleryDetails = null;

        Guid? planId = null;
        string planName = string.Empty;
        string planCode = string.Empty;
        int? slotsCovered = null;
        string summaryDesc;

        if (payment.Module == BusinessModule.Chitu)
        {
            var relatedInstalments = await _db.ChituInstalments
                .AsNoTracking()
                .Include(i => i.ChituSlot).ThenInclude(s => s.ChituPlan)
                .Where(i => i.PaymentId == payment.Id)
                .ToListAsync(ct);

            var firstSlot = relatedInstalments.FirstOrDefault()?.ChituSlot;
            var plan = firstSlot?.ChituPlan;

            planId = plan?.Id;
            planName = plan?.Name ?? "Chitu Plan";
            planCode = plan?.Code ?? string.Empty;
            var isJoining = payment.Remarks != null && payment.Remarks.Contains("Initial joining payment");

            var slotAllocations = relatedInstalments
                .GroupBy(i => i.ChituSlotId)
                .Select(g =>
                {
                    var slot = g.First().ChituSlot!;
                    var clearedRounds = g.Select(i => i.MonthIndex).OrderBy(m => m).ToList();
                    var slotAllocatedAmount = g.Sum(i => i.AmountDue);

                    return new CustomerChituPaymentSlotAllocationDto
                    {
                        SlotId = slot.Id,
                        SlotNumber = slot.SlotNumber,
                        SlotDisplayCode = slot.SlotDisplayCode,
                        AllocatedAmount = slotAllocatedAmount,
                        InstalmentRoundsCovered = clearedRounds,
                        IsJoiningPayment = isJoining
                    };
                })
                .ToList();

            slotsCovered = slotAllocations.Count;
            summaryDesc = isJoining
                ? $"Initial Chitu join: {slotsCovered} slot(s) in {planName}"
                : $"Chitu payment: {slotsCovered} slot(s) in {planName}";

            chituDetails = new CustomerChituPaymentDetailBreakdownDto
            {
                PlanId = planId ?? Guid.Empty,
                PlanName = planName,
                PlanCode = planCode,
                MonthlySlotPrice = plan?.MonthlyInstalmentAmount ?? 0m,
                IsJoiningPayment = isJoining,
                SlotAllocations = slotAllocations
            };
        }
        else
        {
            var contribution = await _db.JewelleryContributions
                .AsNoTracking()
                .Include(c => c.JewelleryEnrolment).ThenInclude(e => e.JewelleryPlan)
                .FirstOrDefaultAsync(c => c.PaymentId == payment.Id, ct);

            var enrolment = contribution?.JewelleryEnrolment;
            planId = enrolment?.JewelleryPlanId;
            planName = enrolment?.PlanNameSnapshot ?? enrolment?.JewelleryPlan?.Name ?? "Jewellery Plan";
            var enrDisplayNumber = enrolment != null ? $"ENR-{enrolment.Id.ToString("N")[..8].ToUpperInvariant()}" : string.Empty;
            planCode = enrDisplayNumber;

            summaryDesc = contribution != null
                ? $"Jewellery contribution: {contribution.GoldGrams:F3}g credited"
                : $"Jewellery payment: {planName}";

            if (contribution != null && enrolment != null)
            {
                jewelleryDetails = new CustomerJewelleryPaymentDetailBreakdownDto
                {
                    EnrolmentId = enrolment.Id,
                    EnrolmentDisplayNumber = enrDisplayNumber,
                    AppliedGoldRatePerGram = contribution.AppliedRatePerGram,
                    AppliedPremiumPercentage = enrolment.PremiumPercentage,
                    CreditedGrams = contribution.GoldGrams,
                    InstalmentMonthIndex = null,
                    PaymentType = "JewelleryContribution"
                };
            }
        }

        return new CustomerPaymentDetailDto
        {
            PaymentId = payment.Id,
            PlanType = planTypeStr,
            PlanId = planId,
            PlanName = planName,
            PlanCode = planCode,
            Currency = payment.Currency,
            Amount = payment.Amount,
            Status = payment.Status.ToString(),
            PaymentMethod = payment.Method.ToString(),
            Provider = isSimulated ? "Simulated" : "Cash/Bank",
            ProviderReference = payment.ProviderReference,
            CreatedAtUtc = payment.CreatedAtUtc,
            ConfirmedAtUtc = payment.CompletedAtUtc,
            IsSimulated = isSimulated,
            AllocationsCount = payment.Allocations.Count,
            SlotsCoveredCount = slotsCovered,
            HasDetails = true,
            SummaryDescription = summaryDesc,
            Remarks = payment.Remarks,
            ChituDetails = chituDetails,
            JewelleryDetails = jewelleryDetails
        };
    }
}
