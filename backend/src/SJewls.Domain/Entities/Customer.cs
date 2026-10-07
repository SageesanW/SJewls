using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public Guid PrimaryBranchId { get; set; }
    public Branch? PrimaryBranch { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? MergedIntoCustomerId { get; set; }

    // Navigation properties
    public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
    public ICollection<ChituSlot> ChituSlots { get; set; } = new List<ChituSlot>();
    public ICollection<JewelleryEnrolment> JewelleryEnrolments { get; set; } = new List<JewelleryEnrolment>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class CustomerContact : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ContactType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public bool IsPrimary { get; set; }
}
