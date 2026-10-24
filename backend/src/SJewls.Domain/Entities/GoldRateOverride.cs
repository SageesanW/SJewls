using SJewls.Domain.Common;

namespace SJewls.Domain.Entities;

public class GoldRateOverride : BaseEntity
{
    /// <summary>
    /// Specific branch ID or null if applied globally across all branches.
    /// </summary>
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>
    /// Overridden 24K rate in LKR per gram.
    /// </summary>
    public decimal Rate24k { get; set; }

    /// <summary>
    /// Overridden 22K rate in LKR per gram.
    /// </summary>
    public decimal Rate22k { get; set; }

    /// <summary>
    /// Indicates whether the manual override is actively enforced over live API market feeds.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Business justification or remarks for setting this override.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Staff member who last configured or toggled this override.
    /// </summary>
    public Guid? UpdatedByStaffId { get; set; }
    public Staff? UpdatedByStaff { get; set; }
}
