using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class ChituPlan : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public decimal MonthlyInstalmentAmount { get; set; }
    public decimal ChituBenefitValue { get; set; }
    public int TotalSlotCapacity { get; set; }
    public int DurationMonths { get; set; }
    public int DueDayOfMonth { get; set; } = 10;
    public DateOnly StartDate { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Draft;

    // Navigation properties
    public ICollection<ChituSlot> Slots { get; set; } = new List<ChituSlot>();
    public ICollection<ChituDraw> Draws { get; set; } = new List<ChituDraw>();
}

public class ChituSlot : BaseEntity
{
    public Guid ChituPlanId { get; set; }
    public ChituPlan? ChituPlan { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int SlotNumber { get; set; }
    public ChituSlotStatus Status { get; set; } = ChituSlotStatus.PendingReservation;

    public DateTimeOffset? ReservedUntilUtc { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureRemarks { get; set; }

    // Navigation properties
    public ICollection<ChituInstalment> Instalments { get; set; } = new List<ChituInstalment>();
    public WinnerBenefit? WinnerBenefit { get; set; }
}

public class ChituInstalment : BaseEntity
{
    public Guid ChituSlotId { get; set; }
    public ChituSlot? ChituSlot { get; set; }

    public int MonthIndex { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public InstalmentStatus Status { get; set; } = InstalmentStatus.Pending;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public Guid? PaymentId { get; set; }
}

public class ChituDraw : BaseEntity
{
    public Guid ChituPlanId { get; set; }
    public ChituPlan? ChituPlan { get; set; }

    public int DrawMonthIndex { get; set; }
    public DateOnly DrawDate { get; set; }

    public Guid WinningSlotId { get; set; }
    public ChituSlot? WinningSlot { get; set; }

    public Guid RecordedByStaffId { get; set; }
    public Staff? RecordedByStaff { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public WinnerBenefit? Benefit { get; set; }
}

public class WinnerBenefit : BaseEntity
{
    public Guid ChituDrawId { get; set; }
    public ChituDraw? ChituDraw { get; set; }

    public Guid ChituSlotId { get; set; }
    public ChituSlot? ChituSlot { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal ChituBenefitValue { get; set; }
    public BenefitStatus Status { get; set; } = BenefitStatus.Unclaimed;

    public DateTimeOffset? ClaimedAtUtc { get; set; }
    public Guid? HandedOverByStaffId { get; set; }
    public Staff? HandedOverByStaff { get; set; }
    public string? ClaimRemarks { get; set; }
}
