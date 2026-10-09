using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.DTOs;

public class JewelleryPlanDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public decimal TargetProductGoldWeightGrams { get; set; }
    public int Karat { get; set; } = 24;

    public int[] AllowedDurationsMonths { get; set; } = Array.Empty<int>();
    public DateOnly StartDate { get; set; }

    public bool IsActive { get; set; }
    public string DisplayStatus { get; set; } = "Active"; // "Active", "Scheduled", "Deactivated"

    // Progress metrics derived from all relevant enrolments
    public int TotalEnrolments { get; set; }
    public int ActiveEnrolments { get; set; }
    public decimal TotalNetAccumulatedGrams { get; set; }
    public decimal TotalTargetGramsAcrossEnrolments { get; set; }
    public decimal OverallProgressPercentage { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public DateTimeOffset? ReopenedAtUtc { get; set; }
}

public class JewelleryPlanPagedResponse
{
    public List<JewelleryPlanDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    // Aggregates for filter counters
    public int ActiveCount { get; set; }
    public int ScheduledCount { get; set; }
    public int DeactivatedCount { get; set; }
}

public class CreateJewelleryPlanRequest
{
    public Guid? BranchId { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "Plan name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Plan name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Jewellery image is required.")]
    [Url(ErrorMessage = "A valid image URL is required.")]
    public string ImageUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "At least one duration option is required.")]
    [MinLength(1, ErrorMessage = "At least one duration option must be selected.")]
    public List<int> AllowedDurationsMonths { get; set; } = new();

    [Required(ErrorMessage = "Start date is required.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "Target gold grams is required.")]
    [Range(0.0001, 10000.0, ErrorMessage = "Target gold weight must be a positive number of grams.")]
    public decimal TargetProductGoldWeightGrams { get; set; }

    public int Karat { get; set; } = 24;
}

public class UpdateJewelleryPlanRequest
{
    [Required(ErrorMessage = "Category is required.")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "Plan name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Plan name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Jewellery image is required.")]
    [Url(ErrorMessage = "A valid image URL is required.")]
    public string ImageUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "At least one duration option is required.")]
    [MinLength(1, ErrorMessage = "At least one duration option must be selected.")]
    public List<int> AllowedDurationsMonths { get; set; } = new();

    [Required(ErrorMessage = "Start date is required.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "Target gold grams is required.")]
    [Range(0.0001, 10000.0, ErrorMessage = "Target gold weight must be a positive number of grams.")]
    public decimal TargetProductGoldWeightGrams { get; set; }

    public int Karat { get; set; } = 24;
}

public class UpdateJewelleryPlanStatusRequest
{
    [Required(ErrorMessage = "Action is required ('Deactivate' or 'Reopen').")]
    public string Action { get; set; } = string.Empty; // "Deactivate" or "Reopen"

    public string? Reason { get; set; }
}

public class PlanCustomersPagedResponse
{
    public Guid PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal PlanTargetGrams { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal OverallProgressPercentage { get; set; }

    public List<PlanCustomerEnrolmentDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class PlanCustomerEnrolmentDto
{
    public Guid EnrolmentId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public DateOnly JoiningDate { get; set; }
    public int SelectedDurationMonths { get; set; }
    public DateOnly Deadline { get; set; }

    public decimal EnrolmentTargetGrams { get; set; }
    public decimal TotalConfirmedMoneyPaid { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal NetAccumulatedGrams { get; set; }
    public decimal RemainingGrams { get; set; }
    public decimal IndividualProgressPercentage { get; set; }

    public string EnrolmentStatus { get; set; } = string.Empty;

    public List<EnrolmentContributionDto> Contributions { get; set; } = new();
}

public class EnrolmentContributionDto
{
    public Guid Id { get; set; }
    public decimal CurrencyAmount { get; set; }
    public string Currency { get; set; } = "LKR";
    public decimal GoldGrams { get; set; }
    public decimal AppliedRatePerGram { get; set; }
    public int Karat { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public string Status { get; set; } = "Succeeded";
    public string PaymentMethod { get; set; } = "Online";
}

public class FinancialQuoteRequest
{
    public Guid BranchId { get; set; }
    public Guid? EnrolmentId { get; set; }
    public string Mode { get; set; } = "ByGrams"; // "ByGrams" or "ByMoney"
    public decimal? InputGrams { get; set; }
    public decimal? InputMoney { get; set; }
}

public class FinancialQuoteResponse
{
    public Guid BranchId { get; set; }
    public decimal RatePerGram { get; set; } // 24K applicable rate
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal PayableMoney { get; set; }
    public decimal CreditedGrams { get; set; }

    public decimal? RemainingTargetGrams { get; set; }
    public bool WillExceedTarget { get; set; }
    public DateTimeOffset QuoteExpiresAtUtc { get; set; }
}
