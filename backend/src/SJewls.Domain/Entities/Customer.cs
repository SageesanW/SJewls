using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Nic { get; set; }
    
    // Direct unified contacts
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsPhoneVerified { get; set; } = false;
    public bool IsEmailVerified { get; set; } = false;
    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    public Guid PrimaryBranchId { get; set; }
    public Branch? PrimaryBranch { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsProfileComplete { get; set; } = false;
    public Guid? MergedIntoCustomerId { get; set; }

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<ChituSlot> ChituSlots { get; set; } = new List<ChituSlot>();
    public ICollection<JewelleryEnrolment> JewelleryEnrolments { get; set; } = new List<JewelleryEnrolment>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
