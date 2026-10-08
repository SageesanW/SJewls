namespace SJewls.Application.DTOs;

public class BranchDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = "Jaffna";
    public string Country { get; set; } = "Sri Lanka";
    public string Currency { get; set; } = "LKR";
    public string Timezone { get; set; } = "Asia/Colombo";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public int AssignedStaffCount { get; set; }
    public int CustomerCount { get; set; }
}

public class CreateBranchRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = "Jaffna";
    public string Country { get; set; } = "Sri Lanka";
    public string Currency { get; set; } = "LKR";
    public string Timezone { get; set; } = "Asia/Colombo";
    public bool IsActive { get; set; } = true;
}

public class UpdateBranchRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "Sri Lanka";
    public string Currency { get; set; } = "LKR";
    public string Timezone { get; set; } = "Asia/Colombo";
    public bool IsActive { get; set; } = true;
}

public class UpdateBranchStatusRequest
{
    public bool IsActive { get; set; }
}
