using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SJewls.Domain.Enums;

namespace SJewls.Application.DTOs;

#region Plan DTOs

public class CreateChituPlanRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChituRedemptionType RedemptionType { get; set; } = ChituRedemptionType.Cash;

    [Range(1, 120)]
    public int DurationMonths { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Range(1, 100000000)]
    public decimal MonthlyInstalmentAmount { get; set; }

    [Range(1, 10000)]
    public int TotalSlotCapacity { get; set; }

    [Required]
    public Guid BranchId { get; set; }

    public string? Description { get; set; }
}

public class UpdateChituPlanRequest
{
    [StringLength(100, MinimumLength = 2)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlanStatus? Status { get; set; }

    [StringLength(20, MinimumLength = 2)]
    public string? Code { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChituRedemptionType? RedemptionType { get; set; }

    [Range(1, 120)]
    public int? DurationMonths { get; set; }

    [Range(1, 100000000)]
    public decimal? MonthlyInstalmentAmount { get; set; }

    [Range(1, 10000)]
    public int? TotalSlotCapacity { get; set; }

    public DateOnly? StartDate { get; set; }

    public Guid? BranchId { get; set; }
}

public class ChituPlanScheduleRoundDto
{
    public int RoundIndex { get; set; }
    public DateOnly ScheduledRaffleDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? RecordedAtUtc { get; set; }
    public Guid? WinningSlotId { get; set; }
    public string? WinningSlotDisplayCode { get; set; }
    public string? WinnerCustomerName { get; set; }
}

public class ChituPlanCardDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChituRedemptionType RedemptionType { get; set; }
    public string RedemptionTypeLabel => RedemptionType == ChituRedemptionType.Gold ? "Gold" : "Cash";

    public int DurationMonths { get; set; }
    public decimal MonthlyInstalmentAmount { get; set; }
    public decimal ChituBenefitValue { get; set; }
    public int TotalSlotCapacity { get; set; }
    public int AssignedSlotsCount { get; set; }
    public int AvailableSlotsCount => Math.Max(0, TotalSlotCapacity - AssignedSlotsCount);

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PlanStatus Status { get; set; }
    public string StatusLabel => Status.ToString();

    public bool IsUpcoming { get; set; }
    public int CurrentRoundIndex { get; set; }
    public DateOnly CurrentRoundDueDate { get; set; }
    public int CurrentRoundPaidSlotsCount { get; set; }
    public int CurrentRoundUnpaidSlotsCount { get; set; }
    public int WinningSlotsCount { get; set; }
    public int ClosedSlotsCount { get; set; }

    public bool HasOverdueRaffle { get; set; }
    public int? OverdueRaffleRoundIndex { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class ChituPlanDetailDto : ChituPlanCardDto
{
    public List<ChituPlanScheduleRoundDto> Schedule { get; set; } = new();
}

public class ChituPlanListResponse
{
    public List<ChituPlanCardDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
}

#endregion

#region Slot Assignment DTOs

public class ChituSlotAssignmentPreviewRequest
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Range(1, 50)]
    public int SlotsCount { get; set; } = 1;

    [Range(0, 120)]
    public int AdditionalPrepaidMonths { get; set; } = 0;
}

public class ChituSlotAssignmentPreviewResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int SlotsCount { get; set; }
    public List<int> AssignedSlotNumbers { get; set; } = new();
    public List<string> AssignedSlotDisplayCodes { get; set; } = new();

    public int CurrentRoundIndex { get; set; }
    public int RequiredMonthsPerSlot { get; set; }
    public int AdditionalPrepaidMonths { get; set; }
    public int TotalMonthsPaidPerSlot => RequiredMonthsPerSlot + AdditionalPrepaidMonths;
    public decimal RequiredAmountPerSlot { get; set; }
    public decimal TotalPayableAmount { get; set; }

    public List<ChituSlotAllocationBreakdownDto> AllocationBreakdown { get; set; } = new();
}

public class ChituSlotAllocationBreakdownDto
{
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public List<ChituInstalmentAllocationItemDto> InstalmentsToPay { get; set; } = new();
    public decimal TotalForSlot { get; set; }
}

public class ChituInstalmentAllocationItemDto
{
    public int MonthIndex { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
}

public class AssignChituSlotsRequest
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Range(1, 50)]
    public int SlotsCount { get; set; } = 1;

    [Range(0, 120)]
    public int AdditionalPrepaidMonths { get; set; } = 0;

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class AssignChituSlotsResponse
{
    public Guid PaymentId { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public List<ChituSlotDto> CreatedSlots { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class ChituSlotDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public ChituSlotStatus Status { get; set; }
    public string StatusLabel => Status.ToString();
    public int MonthsPaidCount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
}

#endregion

#region Payment DTOs

public class CustomerPayableSlotsResponse
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public decimal MonthlyInstalmentAmount { get; set; }
    public int CurrentRoundIndex { get; set; }
    public List<ChituPayableSlotDto> Slots { get; set; } = new();
}

public class ChituPayableSlotDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public ChituSlotStatus Status { get; set; }
    public int NextUnpaidMonthIndex { get; set; }
    public int TotalUnpaidMonthsRemaining { get; set; }
    public int InstalmentsPaidCount { get; set; }
    public decimal TotalConfirmedPaid { get; set; }
    public bool IsPaidForCurrentRound { get; set; }
    public List<ChituInstalmentDto> UnpaidInstalments { get; set; } = new();
}

public class ChituInstalmentDto
{
    public Guid Id { get; set; }
    public int MonthIndex { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public InstalmentStatus Status { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
}

public class ChituSlotAllocationInputDto
{
    [Required]
    public Guid SlotId { get; set; }

    [Range(1, 100000000)]
    public decimal Amount { get; set; }

    public int? ExpectedStartingMonthIndex { get; set; }
}

public class ChituPaymentPreviewRequest
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public List<ChituSlotAllocationInputDto> SlotAllocations { get; set; } = new();
}

public class ChituPaymentPreviewResponse
{
    public decimal TotalAmount { get; set; }
    public List<ChituSlotPaymentPreviewBreakdownDto> Slots { get; set; } = new();
}

public class ChituSlotPaymentPreviewBreakdownDto
{
    public Guid SlotId { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int MonthsCovered { get; set; }
    public List<ChituInstalmentDto> ClearedInstalments { get; set; } = new();
}

public class RecordChituPaymentRequest
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public List<ChituSlotAllocationInputDto> SlotAllocations { get; set; } = new();

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class RecordChituPaymentResponse
{
    public Guid PaymentId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset PaidAtUtc { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public List<ChituSlotPaymentReceiptItemDto> SlotReceipts { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class ChituSlotPaymentReceiptItemDto
{
    public Guid SlotId { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public decimal AmountAllocated { get; set; }
    public int MonthsCovered { get; set; }
    public List<int> ClearedMonthIndices { get; set; } = new();
}

#endregion

#region Physical Raffle Draw DTOs

public class ChituEligibleSlotsResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int RoundIndex { get; set; }
    public DateOnly ScheduledRaffleDate { get; set; }
    public int EligibleCount { get; set; }
    public List<ChituEligibleSlotDto> EligibleSlots { get; set; } = new();
}

public class ChituEligibleSlotDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerNic { get; set; }
    public decimal TotalConfirmedPaid { get; set; }
    public int MonthsPaidCount { get; set; }
}

public class RecordChituRaffleRequest
{
    [Required]
    public Guid WinningSlotId { get; set; }

    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
}

public class RecordChituRaffleResponse
{
    public Guid DrawId { get; set; }
    public Guid PlanId { get; set; }
    public int RoundIndex { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid WinningSlotId { get; set; }
    public string WinningSlotDisplayCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal WinningEntitlementValue { get; set; }
    public decimal AdvanceExcessPrepaidAmount { get; set; }
    public bool AdvanceExcessSettlementDecisionRequired => AdvanceExcessPrepaidAmount > 0;
    public BenefitStatus BenefitStatus { get; set; }
    public string Message { get; set; } = string.Empty;
}

#endregion

#region Customer / Slot Roster DTOs

public class ChituRosterQueryParameters
{
    public int? RoundIndex { get; set; }
    public string? Search { get; set; }
    public string? StatusFilter { get; set; } // All, Active, Won, Closed
    public string? PaymentFilter { get; set; } // All, Paid, Unpaid
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ChituRosterResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public int SelectedRoundIndex { get; set; }
    public int MaxSlots { get; set; }
    public int AssignedSlots { get; set; }
    public int AvailableSlots => Math.Max(0, MaxSlots - AssignedSlots);
    public int ActiveSlotsCount { get; set; }
    public int WinningSlotsCount { get; set; }
    public int ClosedSlotsCount { get; set; }
    public int RoundPaidSlotsCount { get; set; }
    public int RoundUnpaidSlotsCount { get; set; }

    public List<ChituRosterRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ChituRosterRowDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public ChituSlotStatus SlotStatus { get; set; }
    public string SlotStatusLabel => SlotStatus.ToString();

    public bool IsPaidForSelectedRound { get; set; }
    public bool IsFullyPaidThroughSelectedRound { get; set; }
    public int InstalmentsPaidCount { get; set; }
    public int? PaidThroughMonthIndex { get; set; }
    public int OutstandingDueMonthsCount { get; set; }
    public decimal TotalConfirmedPaid { get; set; }

    public bool IsWinner { get; set; }
    public int? WinningRoundIndex { get; set; }
    public decimal? EntitlementAmount { get; set; }
    public decimal? AdvanceExcessAmount { get; set; }
}

#endregion
