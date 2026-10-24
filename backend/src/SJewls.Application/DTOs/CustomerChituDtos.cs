using System.ComponentModel.DataAnnotations;
using SJewls.Domain.Enums;

namespace SJewls.Application.DTOs;

#region Active Chitu Discovery DTOs

public class CustomerChituPlanSummaryDto
{
    public Guid PlanId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChituRedemptionType RedemptionType { get; set; }
    public string Currency { get; set; } = "LKR";
    public int DurationMonths { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlySlotPrice { get; set; }
    public decimal WinningEntitlementPerSlot { get; set; }
    public int TotalSlotCapacity { get; set; }
    public int IssuedSlotsCount { get; set; }
    public int AvailableSlotsCount { get; set; }
    public decimal CapacityPercentage { get; set; }
    public int CurrentRoundIndex { get; set; }
    public int? NextRaffleRoundIndex { get; set; }
    public DateOnly? NextRaffleDate { get; set; }
    public int CustomerOwnedSlotsCount { get; set; }
    public bool IsAvailableForJoining { get; set; }
    public string JoiningAvailabilityReason { get; set; } = string.Empty;
}

public class CustomerChituRoundScheduleDto
{
    public int RoundIndex { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public string Status { get; set; } = "Upcoming"; // "Completed", "Current", "Upcoming"
    public bool HasRecordedDraw { get; set; }
}

public class CustomerChituPlanDetailDto : CustomerChituPlanSummaryDto
{
    public List<CustomerChituRoundScheduleDto> Schedule { get; set; } = new();
    public List<string> Terms { get; set; } = new();
}

public class CustomerChituPlansPagedResponse
{
    public List<CustomerChituPlanSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

#endregion

#region Join Preview & Confirm DTOs

public class CustomerChituJoinPreviewRequest
{
    [Required]
    [Range(1, 1000, ErrorMessage = "Requested slot quantity must be a positive whole number.")]
    public int Quantity { get; set; }
}

public class CustomerChituJoinPreviewResponse
{
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
    public decimal MonthlySlotPrice { get; set; }
    public int RequestedQuantity { get; set; }
    public decimal TotalOrdinaryMonthlyPayment { get; set; }
    public int RequiredInitialInstalmentsPerSlot { get; set; }
    public decimal RequiredInitialAmountPerSlot { get; set; }
    public decimal TotalInitialAmountPayable { get; set; }
    public int AvailableSlotsCount { get; set; }
    public decimal WinningEntitlementPerSlot { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int CurrentRoundIndex { get; set; }
    public int? NextRaffleRoundIndex { get; set; }
    public DateOnly? NextRaffleDate { get; set; }
    public int ExistingOwnedSlotsCount { get; set; }
    public string PreviewReference { get; set; } = string.Empty;
    public DateTimeOffset PreviewCreatedAtUtc { get; set; }
    public DateTimeOffset PreviewExpiresAtUtc { get; set; }
}

public class CustomerChituJoinRequest
{
    [Required]
    [Range(1, 1000, ErrorMessage = "Slot quantity must be a positive whole number.")]
    public int Quantity { get; set; }

    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? PreviewReference { get; set; }

    public decimal? ExpectedTotalInitialAmount { get; set; }
}

public class CustomerAssignedSlotDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public int InitialMonthsPaid { get; set; }
    public DateTimeOffset ActivatedAtUtc { get; set; }
}

public class CustomerChituJoinResponse
{
    public Guid PaymentId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int SlotsCount { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public string Currency { get; set; } = "LKR";
    public bool IsSimulated { get; set; }
    public List<CustomerAssignedSlotDto> AssignedSlots { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

#endregion

#region My Plans (Portfolio) DTOs

public class CustomerChituSlotSummaryDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; set; }
    public decimal MonthlySlotPrice { get; set; }
    public int DurationMonths { get; set; }
    public int InstalmentsPaidCount { get; set; }
    public int PaidThroughRound { get; set; }
    public int ArrearsMonths { get; set; }
    public decimal ArrearsAmount { get; set; }
    public int AdvancePaidMonths { get; set; }
    public decimal AdvancePaidAmount { get; set; }
    public bool IsCurrentRoundPaid { get; set; }
    public decimal TotalConfirmedContributions { get; set; }
    public ChituSlotStatus SlotStatus { get; set; }
    public bool IsWinner { get; set; }
    public int? WinningRoundIndex { get; set; }
    public DateOnly? WinningDrawDate { get; set; }
    public decimal? WinningPrizeAmount { get; set; }
    public bool WinningEntitlementClaimed { get; set; }
    public bool IsRaffleEligible { get; set; }
    public string RaffleEligibilityReason { get; set; } = string.Empty;
}

public class CustomerChituInvestmentPlanDto
{
    public Guid PlanId { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChituRedemptionType RedemptionType { get; set; }
    public string Currency { get; set; } = "LKR";
    public int DurationMonths { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int CurrentRoundIndex { get; set; }
    public int? NextRaffleRoundIndex { get; set; }
    public DateOnly? NextRaffleDate { get; set; }
    public PlanStatus PlanStatus { get; set; }
    public int TotalOwnedSlotsCount { get; set; }
    public int ActiveSlotsCount { get; set; }
    public int WinningSlotsCount { get; set; }
    public decimal MonthlySlotPrice { get; set; }
    public decimal NormalMonthlyCommitment { get; set; }
    public decimal ActualOutstandingDueThroughCurrentRound { get; set; }
    public int CurrentRoundPaidSlotsCount { get; set; }
    public int CurrentRoundUnpaidSlotsCount { get; set; }
    public decimal TotalConfirmedContributions { get; set; }
    public bool CanPurchaseAdditionalSlots { get; set; }
    public bool CanPayExistingSlots { get; set; }
    public bool IsSimulated { get; set; }
    public List<CustomerChituSlotSummaryDto> Slots { get; set; } = new();
}

public class CustomerChituSlotInstalmentDto
{
    public Guid Id { get; set; }
    public int MonthIndex { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public InstalmentStatus Status { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public Guid? PaymentId { get; set; }
}

public class CustomerWinnerBenefitDto
{
    public Guid Id { get; set; }
    public Guid DrawId { get; set; }
    public Guid SlotId { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public decimal ChituBenefitValue { get; set; }
    public decimal AdvanceExcessAmount { get; set; }
    public ChituRedemptionType RedemptionType { get; set; }
    public BenefitStatus Status { get; set; }
    public DateTimeOffset? ClaimedAtUtc { get; set; }
    public string? ClaimRemarks { get; set; }
}

public class CustomerChituSlotDetailDto : CustomerChituSlotSummaryDto
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
    public List<CustomerChituSlotInstalmentDto> Instalments { get; set; } = new();
    public CustomerWinnerBenefitDto? EntitlementClaimInfo { get; set; }
}

public class CustomerChituInvestmentsPagedResponse
{
    public List<CustomerChituInvestmentPlanDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

#endregion

#region Existing Slot Payments DTOs

public class CustomerPayableSlotItemDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public ChituSlotStatus Status { get; set; }
    public bool IsEligibleForPayment { get; set; }
    public string? IneligibilityReason { get; set; }
    public decimal MonthlyInstalmentPrice { get; set; }
    public List<int> PaidInstalmentRounds { get; set; } = new();
    public List<int> UnpaidInstalmentRounds { get; set; } = new();
    public decimal OutstandingDueAmount { get; set; }
    public int RemainingPayableMonths { get; set; }
    public bool IsCurrentRoundPaid { get; set; }
    public int InstalmentsPaidCount { get; set; }
    public decimal TotalConfirmedPaid { get; set; }
}

public class CustomerPayableSlotsDto
{
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal MonthlySlotPrice { get; set; }
    public string Currency { get; set; } = "LKR";
    public int CurrentRoundIndex { get; set; }
    public List<CustomerPayableSlotItemDto> Slots { get; set; } = new();
}

public class CustomerSlotPaymentSelectionDto
{
    [Required]
    public Guid SlotId { get; set; }

    [Required]
    [Range(1, 120, ErrorMessage = "Instalments to pay per slot must be at least 1.")]
    public int InstalmentsToPayCount { get; set; }

    public int? ExpectedStartingMonthIndex { get; set; }
}

public class CustomerChituPaymentPreviewRequest
{
    [Required]
    public List<CustomerSlotPaymentSelectionDto> SlotSelections { get; set; } = new();
}

public class CustomerSlotPaymentPreviewItemDto
{
    public Guid SlotId { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public int InstalmentsToPayCount { get; set; }
    public decimal AmountAllocated { get; set; }
    public List<int> ClearedRounds { get; set; } = new();
    public decimal RemainingArrearsAfterPayment { get; set; }
    public int AdvanceCoverageMonthsAfterPayment { get; set; }
    public bool ResultingCurrentRoundCoverage { get; set; }
    public bool ResultingRaffleEligible { get; set; }
}

public class CustomerChituPaymentPreviewResponse
{
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
    public decimal MonthlySlotPrice { get; set; }
    public decimal TotalPayableAmount { get; set; }
    public List<CustomerSlotPaymentPreviewItemDto> Slots { get; set; } = new();
    public string PreviewReference { get; set; } = string.Empty;
    public DateTimeOffset PreviewCreatedAtUtc { get; set; }
    public DateTimeOffset PreviewExpiresAtUtc { get; set; }
}

public class CustomerChituPaymentConfirmRequest
{
    [Required]
    public List<CustomerSlotPaymentSelectionDto> SlotSelections { get; set; } = new();

    [Required]
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? PreviewReference { get; set; }

    public decimal? ExpectedTotalAmount { get; set; }
}

public class CustomerSlotPaymentReceiptDto
{
    public Guid SlotId { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public decimal AmountAllocated { get; set; }
    public int MonthsCovered { get; set; }
    public List<int> ClearedMonthIndices { get; set; } = new();
}

public class CustomerChituPaymentConfirmResponse
{
    public Guid PaymentId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateTimeOffset PaidAtUtc { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public bool IsSimulated { get; set; }
    public List<CustomerSlotPaymentReceiptDto> Receipts { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

#endregion
