using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class PhysicalClaim : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public BusinessModule Module { get; set; }
    public Guid ReferenceId { get; set; } // WinnerBenefitId or JewelleryEnrolmentId

    public DateTimeOffset ClaimedAtUtc { get; set; }
    public Guid HandedOverByStaffId { get; set; }
    public Staff? HandedOverByStaff { get; set; }

    public string Remarks { get; set; } = string.Empty;
}

public class ExtensionRequest : BaseEntity
{
    public Guid JewelleryEnrolmentId { get; set; }
    public JewelleryEnrolment? JewelleryEnrolment { get; set; }

    public int RequestedExtensionMonths { get; set; }
    public string Reason { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public Guid? DecidedByStaffId { get; set; }
    public Staff? DecidedByStaff { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? DecisionRemarks { get; set; }
}

public class ClosureRequest : BaseEntity
{
    public Guid JewelleryEnrolmentId { get; set; }
    public JewelleryEnrolment? JewelleryEnrolment { get; set; }

    public string Reason { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public decimal ApprovedRefundAmount { get; set; }
    public decimal CancellationFeeCharged { get; set; }

    public Guid? DecidedByStaffId { get; set; }
    public Staff? DecidedByStaff { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? DecisionRemarks { get; set; }
}

public class Notification : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "general"; // draw, payment, reminder, announcement

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}

public class AuditLog : BaseEntity
{
    public string ActorType { get; set; } = string.Empty; // Staff, Customer, System
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;

    public Guid? BranchId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
}
