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
