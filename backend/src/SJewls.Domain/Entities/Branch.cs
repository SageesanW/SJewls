using SJewls.Domain.Common;

namespace SJewls.Domain.Entities;

public class Branch : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = "Jaffna";
    public string Country { get; set; } = "Sri Lanka";
    public string Currency { get; set; } = "LKR";
    public string Timezone { get; set; } = "Asia/Colombo";
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<StaffBranch> StaffAssignments { get; set; } = new List<StaffBranch>();
    public ICollection<ChituPlan> ChituPlans { get; set; } = new List<ChituPlan>();
    public ICollection<JewelleryPlan> JewelleryPlans { get; set; } = new List<JewelleryPlan>();
    public ICollection<GoldRate> GoldRates { get; set; } = new List<GoldRate>();
}
