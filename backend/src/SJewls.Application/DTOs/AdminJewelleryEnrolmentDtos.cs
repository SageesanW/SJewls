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

public class AdminJewelleryPaymentsQuery
{
    public Guid? BranchId { get; set; }
    public Guid? PlanId { get; set; }
    public string? Status { get; set; } // "All", "Successful", "Pending"
    public string? Search { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class AdminJewelleryPaymentItemDto
{
    public Guid PaymentId { get; set; }
    public Guid? ContributionId { get; set; }
    public int? InstalmentNumber { get; set; }
    public string InstalmentLabel { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public Guid EnrolmentId { get; set; }
    public string EnrolmentReference { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchTimezone { get; set; } = "Asia/Colombo";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal GoldRate { get; set; }
    public decimal GoldCredit { get; set; }
    public decimal? EstimatedPendingGoldCredit { get; set; }
    public string Status { get; set; } = "Successful"; // "Successful" or "Pending"
    public string PaymentMethod { get; set; } = "Cash";
    public string PaymentReference { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool CanSendReminder { get; set; }
    public string? ReminderReason { get; set; }
    public DateTimeOffset? LastSuccessfulPaymentAt { get; set; }
    public DateTimeOffset? ReminderEligibleAt { get; set; }
}

public class AdminJewelleryPaymentsPagedResponse
{
    public List<AdminJewelleryPaymentItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public decimal TotalConfirmedAmount { get; set; }
    public decimal TotalConfirmedGrams { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public int TotalPendingCount { get; set; }
    public int TotalSuccessfulCount { get; set; }
    public string Currency { get; set; } = "LKR";
}

public class AdminIncompleteEnrolmentsQuery
{
    public Guid? BranchId { get; set; }
    public Guid? PlanId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class AdminIncompleteEnrolmentItemDto
{
    public Guid EnrolmentId { get; set; }
    public string EnrolmentReference { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateOnly JoiningDate { get; set; }
    public int SelectedDurationMonths { get; set; }
    public DateOnly Deadline { get; set; }
    public decimal TargetGrams { get; set; }
    public decimal NetSavedGrams { get; set; }
    public decimal RemainingGrams { get; set; }
    public decimal TotalConfirmedMoneyPaid { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal ProgressPercentage { get; set; }
    public string Status { get; set; } = "DurationEnded - Below Target";

    public bool CanSendReminder { get; set; }
    public string? ReminderReason { get; set; }
    public DateTimeOffset? LastSuccessfulPaymentAt { get; set; }
}

public class AdminIncompleteEnrolmentsPagedResponse
{
    public List<AdminIncompleteEnrolmentItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public int TotalIncompleteEnrolments { get; set; }
    public decimal TotalRemainingGrams { get; set; }
    public decimal TotalConfirmedMoneyPaid { get; set; }
    public string Currency { get; set; } = "LKR";
}

public class ChannelStatusDto
{
    public bool Available { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AdminReminderContextDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsEmailVerified { get; set; }

    public Guid EnrolmentId { get; set; }
    public string EnrolmentReference { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal TargetGrams { get; set; }
    public decimal SavedGrams { get; set; }
    public decimal RemainingGrams { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly Deadline { get; set; }
    public DateTimeOffset? LastSuccessfulPaymentAt { get; set; }
    public decimal TotalContributedCurrency { get; set; }
    public string Currency { get; set; } = "LKR";

    public string ReminderReason { get; set; } = string.Empty;
    public bool CanSendReminder { get; set; }
    public DateTimeOffset? ReminderEligibleAt { get; set; }
    public string SuggestedTitle { get; set; } = string.Empty;
    public string SuggestedMessage { get; set; } = string.Empty;

    public ChannelStatusDto InApp { get; set; } = new();
    public ChannelStatusDto Email { get; set; } = new();
    public ChannelStatusDto Sms { get; set; } = new();
    public ChannelStatusDto Push { get; set; } = new();
}

public class AdminSendReminderRequest
{
    [Required]
    public List<string> Channels { get; set; } = new();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}

public class AdminSendReminderResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> DeliveredChannels { get; set; } = new();
    public List<string> FailedChannels { get; set; } = new();
    public DateTimeOffset SentAtUtc { get; set; }
}
