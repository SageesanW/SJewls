using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class JewelleryPlan : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public decimal TargetProductGoldWeightGrams { get; set; }
    public int Karat { get; set; } = 22; // 22 or 24
    public decimal TargetMakingCharges { get; set; }
    public decimal TargetVat { get; set; }
    public decimal TotalTargetAmount { get; set; }

    public int[] AllowedDurationsMonths { get; set; } = new[] { 6, 12, 18 };

    public decimal PremiumPercentage { get; set; } = 5.0m; // Default 5.0%, configurable per plan

    public CancellationFeeType CancellationFeeType { get; set; } = CancellationFeeType.Percentage;
    public decimal CancellationFeeValue { get; set; } = 5.0m; // e.g. 5%

    public Guid? CategoryId { get; set; }
    public JewelleryPlanCategory? Category { get; set; }

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public Guid? DeactivatedByStaffId { get; set; }
    public Staff? DeactivatedByStaff { get; set; }

    public DateTimeOffset? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByStaffId { get; set; }
    public Staff? ReopenedByStaff { get; set; }

    // Navigation properties
    public ICollection<JewelleryEnrolment> Enrolments { get; set; } = new List<JewelleryEnrolment>();
}

public class JewelleryPlanCategory : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<JewelleryPlan> JewelleryPlans { get; set; } = new List<JewelleryPlan>();
}

public class JewelleryEnrolment : BaseEntity
{
    public Guid JewelleryPlanId { get; set; }
    public JewelleryPlan? JewelleryPlan { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string? IdempotencyKey { get; set; }
    public string Currency { get; set; } = "LKR";

    // Snapshot of plan details at joining
    public string PlanNameSnapshot { get; set; } = string.Empty;
    public string CategoryNameSnapshot { get; set; } = string.Empty;
    public string? ImageUrlSnapshot { get; set; }
    public decimal PremiumPercentage { get; set; } = 5.0m;

    public int SelectedDurationMonths { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly TargetEndDate { get; set; }

    // Snapshot of targets at enrolment
    public decimal TargetProductWeightGrams { get; set; }
    public decimal TotalTargetCurrency { get; set; }

    // Accumulated balances
    public decimal TotalSavedGrams { get; set; }
    public decimal TotalContributedCurrency { get; set; }

    public JewelleryEnrolmentStatus Status { get; set; } = JewelleryEnrolmentStatus.Active;

    // Navigation properties
    public ICollection<JewelleryContribution> Contributions { get; set; } = new List<JewelleryContribution>();
    public ICollection<JewelleryPaymentQuote> Quotes { get; set; } = new List<JewelleryPaymentQuote>();
    public ICollection<ExtensionRequest> ExtensionRequests { get; set; } = new List<ExtensionRequest>();
    public ICollection<ClosureRequest> ClosureRequests { get; set; } = new List<ClosureRequest>();
}

public class JewelleryContribution : BaseEntity
{
    public Guid JewelleryEnrolmentId { get; set; }
    public JewelleryEnrolment? JewelleryEnrolment { get; set; }

    public decimal CurrencyAmount { get; set; }
    public decimal GoldGrams { get; set; }
    public decimal AppliedRatePerGram { get; set; }
    public int Karat { get; set; }

    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public DateTimeOffset ConfirmedAtUtc { get; set; }
}

public class JewelleryPaymentQuote : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid JewelleryEnrolmentId { get; set; }
    public JewelleryEnrolment? JewelleryEnrolment { get; set; }

    public JewelleryQuoteInputMode InputMode { get; set; }
    public decimal EnteredValue { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal RatePerGram { get; set; }
    public string RateSource { get; set; } = "GoldAPI.io";
    public DateTimeOffset RateAsOf { get; set; }

    public decimal PremiumPercentage { get; set; }
    public decimal BaseGoldGrams { get; set; }
    public decimal PremiumGrams { get; set; }
    public decimal? ChargeableGoldGrams { get; set; }
    public decimal TotalGoldCredit { get; set; }

    public decimal? BaseAmount { get; set; }
    public decimal? PremiumAmount { get; set; }
    public decimal AmountToPay { get; set; }

    public decimal RemainingGramsAfterPayment { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public JewelleryQuoteStatus Status { get; set; } = JewelleryQuoteStatus.Active;

    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
}
