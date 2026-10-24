namespace SJewls.Application.DTOs;

public class CustomerPaymentSummaryDto
{
    public Guid PaymentId { get; set; }
    public string PlanType { get; set; } = "Chitu"; // "Chitu" or "Jewellery"
    public Guid? PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Succeeded";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public bool IsSimulated { get; set; }
    public int AllocationsCount { get; set; }
    public int? SlotsCoveredCount { get; set; }
    public bool HasDetails { get; set; } = true;
    public string SummaryDescription { get; set; } = string.Empty;
}

public class CustomerChituPaymentSlotAllocationDto
{
    public Guid SlotId { get; set; }
    public int SlotNumber { get; set; }
    public string SlotDisplayCode { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public List<int> InstalmentRoundsCovered { get; set; } = new();
    public bool IsJoiningPayment { get; set; }
}

public class CustomerChituPaymentDetailBreakdownDto
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public decimal MonthlySlotPrice { get; set; }
    public bool IsJoiningPayment { get; set; }
    public List<CustomerChituPaymentSlotAllocationDto> SlotAllocations { get; set; } = new();
}

public class CustomerJewelleryPaymentDetailBreakdownDto
{
    public Guid EnrolmentId { get; set; }
    public string EnrolmentDisplayNumber { get; set; } = string.Empty;
    public decimal AppliedGoldRatePerGram { get; set; }
    public decimal AppliedPremiumPercentage { get; set; }
    public decimal CreditedGrams { get; set; }
    public int? InstalmentMonthIndex { get; set; }
    public string PaymentType { get; set; } = "MonthlyInstalment";
}

public class CustomerPaymentDetailDto : CustomerPaymentSummaryDto
{
    public string? Remarks { get; set; }
    public CustomerChituPaymentDetailBreakdownDto? ChituDetails { get; set; }
    public CustomerJewelleryPaymentDetailBreakdownDto? JewelleryDetails { get; set; }
}

public class CustomerPaymentsPagedResponse
{
    public List<CustomerPaymentSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
