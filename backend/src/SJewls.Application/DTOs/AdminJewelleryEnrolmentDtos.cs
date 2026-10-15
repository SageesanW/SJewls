using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.DTOs;

public class AdminAssignPlanRequest
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid PlanId { get; set; }

    [Required]
    public int SelectedDurationMonths { get; set; }

    public bool ConfirmRepeatEnrolment { get; set; } = false;

    public string? IdempotencyKey { get; set; }
}

public class AdminEnrolmentPreviewRequest
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid PlanId { get; set; }
}

public class AdminEnrolmentPreviewResponse
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid CustomerBranchId { get; set; }
    public string CustomerBranchName { get; set; } = string.Empty;

    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal TargetProductGoldWeightGrams { get; set; }
    public decimal PremiumPercentage { get; set; }
    public List<int> AllowedDurationsMonths { get; set; } = new();
    public DateOnly PlanStartDate { get; set; }
    public bool IsPlanActive { get; set; }

    public int TotalExistingEnrolments { get; set; }
    public int ActiveExistingEnrolments { get; set; }
    public bool RequiresRepeatConfirmation => TotalExistingEnrolments > 0;
    public bool IsEligible { get; set; }
    public string? IneligibilityReason { get; set; }
}

public class AdminRecordPaymentPreviewRequest
{
    [Required]
    public string Mode { get; set; } = "BuyAmount"; // "BuyAmount" or "BuyGram"

    public decimal? EnteredAmount { get; set; }
    public decimal? RequestedGrams { get; set; }
}

public class AdminRecordPaymentPreviewResponse
{
    public Guid EnrolmentId { get; set; }
    public string EnrolmentReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;

    public string Mode { get; set; } = "BuyAmount";
    public decimal RatePerGram { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateTimeOffset RateAsOf { get; set; }
    public string RateSource { get; set; } = "GoldAPI.io";
    public decimal PremiumPercentage { get; set; }

    // Calculation Breakdown
    public decimal BaseGoldGrams { get; set; }
    public decimal PremiumGrams { get; set; }
    public decimal? ChargeableGoldGrams { get; set; }
    public decimal NetCreditedGrams { get; set; }
    public decimal AmountPayable { get; set; }

    public decimal CurrentSavedGrams { get; set; }
    public decimal TargetGrams { get; set; }
    public decimal RemainingGramsBeforePayment { get; set; }
    public decimal RemainingGramsAfterPayment { get; set; }
    public bool WillReachCompletion { get; set; }
    public bool WillExceedTarget { get; set; }
    public DateOnly PaymentDate { get; set; } // Today in branch timezone
}

public class AdminRecordPaymentRequest
{
    [Required]
    public string Mode { get; set; } = "BuyAmount"; // "BuyAmount" or "BuyGram"

    public decimal? EnteredAmount { get; set; }
    public decimal? RequestedGrams { get; set; }

    public string? Notes { get; set; }
    public bool ConfirmedReceived { get; set; } = false;
    public string? IdempotencyKey { get; set; }
}

public class AdminRecordPaymentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal NetCreditedGrams { get; set; }
    public decimal AppliedRatePerGram { get; set; }
    public decimal AppliedPremiumPercentage { get; set; }
    public decimal NewTotalSavedGrams { get; set; }
    public decimal RemainingGrams { get; set; }
    public decimal ProgressPercentage { get; set; }
    public bool PlanCompleted { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}
