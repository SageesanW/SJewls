namespace SJewls.Domain.Enums;

public enum ContactType
{
    Phone = 1,
    Email = 2
}

public enum StaffRoleType
{
    SuperAdmin = 1,
    BranchAdmin = 2,
    Staff = 3
}

public enum PlanStatus
{
    Draft = 1,
    Active = 2,
    Closed = 3
}

public enum ChituSlotStatus
{
    PendingReservation = 1,
    Active = 2,
    Won = 3,
    Closed = 4,
    Cancelled = 5
}

public enum InstalmentStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3
}

public enum BenefitStatus
{
    Unclaimed = 1,
    Claimed = 2
}

public enum JewelleryEnrolmentStatus
{
    Active = 1,
    Completed = 2,
    Extended = 3,
    Cancelled = 4,
    Claimed = 5
}

public enum CancellationFeeType
{
    Fixed = 1,
    Percentage = 2
}

public enum BusinessModule
{
    Chitu = 1,
    Jewellery = 2
}

public enum PaymentMethod
{
    MockOnline = 1,
    Cash = 2
}

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    PartiallyRefunded = 5,
    Refunded = 6
}

public enum RequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
