using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class Staff : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public string? DeactivationReason { get; set; }
    public Guid? DeactivatedByStaffId { get; set; }


    // Password reset & session security
    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetExpiresAtUtc { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    // Navigation properties
    public ICollection<StaffRole> Roles { get; set; } = new List<StaffRole>();
    public ICollection<StaffBranch> AssignedBranches { get; set; } = new List<StaffBranch>();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public StaffRoleType RoleType { get; set; }
    public string Description { get; set; } = string.Empty;
    public ICollection<StaffRole> StaffMembers { get; set; } = new List<StaffRole>();
}

public class StaffRole : BaseEntity
{
    public Guid StaffId { get; set; }
    public Staff? Staff { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

public class StaffBranch : BaseEntity
{
    public Guid StaffId { get; set; }
    public Staff? Staff { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
}
