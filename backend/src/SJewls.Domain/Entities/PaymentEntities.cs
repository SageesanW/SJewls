using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public BusinessModule Module { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "LKR";

    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string ProviderReference { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid? RecordedByStaffId { get; set; }
    public Staff? RecordedByStaff { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? Remarks { get; set; }

    // Navigation properties
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}

public class PaymentAllocation : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public string AllocationType { get; set; } = string.Empty; // ChituInstalment, JewelleryContribution, ChituSlotInitial
    public Guid TargetEntityId { get; set; } // SlotId, InstalmentId, EnrolmentId
    public decimal AllocatedAmount { get; set; }
}

public class Refund : BaseEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetRefundAmount { get; set; }

    public string Reason { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public Guid? ApprovedByStaffId { get; set; }
    public Staff? ApprovedByStaff { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
