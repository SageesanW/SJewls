using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.DTOs;

public class AdminCustomerListItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Nic { get; set; } // Full unmasked NIC for in-store physical customer verification
    public bool IsPhoneVerified { get; set; }
    public bool IsEmailVerified { get; set; }
    public Guid PrimaryBranchId { get; set; }
    public string PrimaryBranchName { get; set; } = string.Empty;
    public string PrimaryBranchCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsProfileComplete { get; set; }
    public int ActivePlansCount { get; set; }
    public int TotalSlotsCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public string? DeactivationReason { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureReason { get; set; }
}

public class AdminCustomerDetailDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Nic { get; set; } // Full unmasked NIC for authorized detail view
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }
    public Guid PrimaryBranchId { get; set; }
    public string PrimaryBranchName { get; set; } = string.Empty;
    public string PrimaryBranchCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsProfileComplete { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public string? DeactivationReason { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureReason { get; set; }
    public List<AdminCustomerSlotDto> ChituSlots { get; set; } = new();
    public List<AdminCustomerEnrolmentDto> JewelleryEnrolments { get; set; } = new();
}

public class CreateCustomerRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "National Identity Card (NIC) is required.")]
    public string Nic { get; set; } = string.Empty;

    public Guid? BranchId { get; set; }
}

public class UpdateCustomerStatusRequest
{
    [Required]
    public bool IsActive { get; set; }

    public string? Reason { get; set; }
}

public class CustomerStatisticsDto
{
    public int TotalCustomers { get; set; }
    public int ActiveCustomers { get; set; }
    public int InactiveCustomers { get; set; }
}

public class AdminCustomerSlotDto
{
    public Guid Id { get; set; }
    public int SlotNumber { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public decimal MonthlyInstalmentAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ActivatedAtUtc { get; set; }
}

public class AdminCustomerEnrolmentDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public decimal TargetWeightGrams { get; set; }
    public decimal TotalSavedGrams { get; set; }
    public decimal TotalContributedCurrency { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly TargetEndDate { get; set; }
}

public class AdminCustomerPagedResponse
{
    public List<AdminCustomerListItemDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class AdminCustomerMetricsDto
{
    public int TotalSlots { get; set; }
    public int TotalCustomers { get; set; }
    public int ActiveInvestment { get; set; }
    public int ClosedAccounts { get; set; }
    public int InactiveCustomers { get; set; }
    public int PendingAccounts { get; set; }
}
