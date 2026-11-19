using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class PlanAlert : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string EventType { get; set; } = string.Empty; // "ChituWinner", "JewelleryCompletion"
    public BusinessModule Module { get; set; } // Chitu, Jewellery

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public Guid PlanId { get; set; }
    public string PlanNameSnapshot { get; set; } = string.Empty;

    public Guid ReferenceId { get; set; } // PlanId for Chitu, EnrolmentId for Jewellery

    // Chitu specific
    public Guid? ChituDrawId { get; set; }
    public ChituDraw? ChituDraw { get; set; }

    public Guid? ChituSlotId { get; set; }
    public ChituSlot? ChituSlot { get; set; }

    public int? ChituSlotNumber { get; set; }
    public string? ChituSlotDisplayCode { get; set; }

    public int? ChituRoundIndex { get; set; }
    public DateOnly? ChituRoundScheduledDate { get; set; }
    public string? ChituRoundScheduledMonthYear { get; set; }

    public ChituRedemptionType? RedemptionType { get; set; }
    public decimal? PrizeAmount { get; set; }

    // Jewellery specific
    public Guid? JewelleryEnrolmentId { get; set; }
    public JewelleryEnrolment? JewelleryEnrolment { get; set; }

    public decimal? TargetGrams { get; set; }

    // Branch / collection outlet info
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? BranchAddress { get; set; }
    public string? BranchPhone { get; set; }

    // Linked Inbox Notification
    public Guid? NotificationId { get; set; }
    public Notification? Notification { get; set; }

    // Acknowledgement State
    public bool IsAcknowledged { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }

    // Instructions & Navigation
    public string NavigationTarget { get; set; } = string.Empty;
    public string CollectionInstruction { get; set; } = string.Empty;
}
