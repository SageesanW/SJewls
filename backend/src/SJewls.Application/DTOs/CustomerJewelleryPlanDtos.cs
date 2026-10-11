using System.ComponentModel.DataAnnotations;
using SJewls.Domain.Enums;

namespace SJewls.Application.DTOs;

#region Category & Plan Browsing DTOs

public class CustomerJewelleryCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int AvailablePlansCount { get; set; }
}

public class CustomerJewelleryPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal TargetGrams { get; set; }
    public int[] OfferedDurations { get; set; } = Array.Empty<int>();
    public decimal PremiumPercentage { get; set; }
    public DateOnly StartDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

#endregion

#region Enrolment Confirmation & Creation DTOs

public class EnrolmentPreviewRequest
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [Range(1, 120, ErrorMessage = "Selected duration must be a valid month count.")]
    public int SelectedDurationMonths { get; set; }
}

public class EnrolmentPreviewResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal TargetGrams { get; set; }
    public int SelectedDurationMonths { get; set; }
    public decimal PremiumPercentage { get; set; }
    public DateOnly ProposedJoiningDate { get; set; }
    public DateOnly ProposedDeadline { get; set; }
    public string Currency { get; set; } = "LKR";
}

public class CreateCustomerEnrolmentRequest
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [Range(1, 120, ErrorMessage = "Selected duration must be a valid month count.")]
    public int SelectedDurationMonths { get; set; }

    /// <summary>
    /// Idempotency key supplied by the client to guarantee single enrolment creation across retries.
    /// </summary>
    public string? IdempotencyKey { get; set; }
}

public class CustomerEnrolmentDto
{
    public Guid EnrolmentId { get; set; }
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal TargetGrams { get; set; }
    public int SelectedDurationMonths { get; set; }
    public DateOnly JoiningDate { get; set; }
    public DateOnly Deadline { get; set; }
    public decimal PremiumPercentage { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal TotalConfirmedMoneyPaid { get; set; }
    public decimal NetAccumulatedGoldGrams { get; set; }
    public decimal RemainingGrams { get; set; }
    public decimal IndividualProgressPercentage { get; set; }

    public string EnrolmentStatus { get; set; } = "Active";
    public bool IsContributionAllowed { get; set; }
    public string? ContributionBlockedReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class CustomerEnrolmentsPagedResponse
{
    public List<CustomerEnrolmentDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class CustomerEnrolmentPaymentDto
{
    public Guid PaymentId { get; set; }
    public Guid ContributionId { get; set; }
    public decimal CurrencyAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal GoldGramsCredited { get; set; }
    public decimal AppliedRatePerGram { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public string Status { get; set; } = "Succeeded";
    public string PaymentMethod { get; set; } = "Online";
}

#endregion

#region Calculation Preview & Locked Quotes

public class JewelleryCalculationRequest
{
    [Required(ErrorMessage = "InputMode is required ('BuyGram' or 'BuyAmount').")]
    public string InputMode { get; set; } = "BuyGram"; // "BuyGram" or "BuyAmount"

    /// <summary>
    /// For BuyGram mode: the desired credited gold grams.
    /// </summary>
    public decimal? RequestedGrams { get; set; }

    /// <summary>
    /// For BuyAmount mode: the total money amount to pay in LKR.
    /// </summary>
    public decimal? EnteredAmount { get; set; }
}

public class JewelleryCalculationPreviewResponse
{
    public string InputMode { get; set; } = "BuyGram";
    public decimal? EnteredAmount { get; set; }
    public decimal? RequestedGrams { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal RatePerGram { get; set; }
    public decimal PremiumPercentage { get; set; }
    public DateTimeOffset RateAsOf { get; set; }
    public string RateSource { get; set; } = "GoldAPI.io";

    public decimal BaseGoldGrams { get; set; }
    public decimal PremiumGrams { get; set; }
    public decimal? ChargeableGoldGrams { get; set; } // Buy Gram only: breakdown only; never credited
    public decimal TotalGoldCredit { get; set; } // Authoritative credited grams
    public decimal? BaseAmount { get; set; } // Buy Gram only
    public decimal? PremiumAmount { get; set; } // Buy Gram only
    public decimal AmountToPay { get; set; } // Payable money in LKR

    public decimal RemainingGramsAfterPayment { get; set; }
    public decimal RemainingTargetGrams { get; set; }
    public bool WillExceedTarget { get; set; }
    public decimal? MaximumPayableAmountForTarget { get; set; }
}

public class CustomerPaymentQuoteDto
{
    public Guid QuoteId { get; set; }
    public Guid EnrolmentId { get; set; }
    public string InputMode { get; set; } = "BuyGram";
    public decimal? EnteredAmount { get; set; }
    public decimal? RequestedGrams { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal RatePerGram { get; set; }
    public decimal PremiumPercentage { get; set; }
    public DateTimeOffset RateAsOf { get; set; }
    public string RateSource { get; set; } = "GoldAPI.io";

    public decimal BaseGoldGrams { get; set; }
    public decimal PremiumGrams { get; set; }
    public decimal? ChargeableGoldGrams { get; set; } // Buy Gram only: breakdown only; never credited
    public decimal TotalGoldCredit { get; set; } // Authoritative credited grams
    public decimal? BaseAmount { get; set; } // Buy Gram only
    public decimal? PremiumAmount { get; set; } // Buy Gram only
    public decimal AmountToPay { get; set; } // Payable money in LKR

    public decimal RemainingGramsAfterPayment { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsExpired { get; set; }
}

#endregion

#region Payment Gateway Deferred & Foundation DTOs

public class PaymentInitiationResponse
{
    public string Code { get; set; } = "PaymentProviderNotConfigured";
    public string Message { get; set; } = "Payment gateway integration is currently deferred. No online payment provider is configured.";
    public Guid QuoteId { get; set; }
    public decimal AmountToPay { get; set; }
    public string Currency { get; set; } = "LKR";
    public bool IsConfigured { get; set; } = false;
}

public class PaymentVerificationPayload
{
    public Guid QuoteId { get; set; }
    public string ProviderReference { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "LKR";
    public string ProviderName { get; set; } = "FutureGateway";
    public string? RawPayload { get; set; }
}

public class PaymentFinalizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public decimal GoldGramsCredited { get; set; }
    public decimal NewTotalSavedGrams { get; set; }
    public bool PlanCompleted { get; set; }
}

#endregion

#region Customer Notification DTOs

public class CustomerNotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "enrolment";
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class CustomerNotificationsPagedResponse
{
    public List<CustomerNotificationDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

#endregion
