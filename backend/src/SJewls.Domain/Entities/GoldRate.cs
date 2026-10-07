using SJewls.Domain.Common;

namespace SJewls.Domain.Entities;

public class GoldRate : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int Karat { get; set; } // 22, 24
    public decimal RatePerGram { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }

    public Guid RecordedByStaffId { get; set; }
    public Staff? RecordedByStaff { get; set; }
}

public class PaymentQuote : BaseEntity
{
    public Guid BranchId { get; set; }
    public Guid CustomerId { get; set; }

    public int Karat { get; set; }
    public decimal AppliedRatePerGram { get; set; }
    public decimal CurrencyAmount { get; set; }
    public decimal GoldGrams { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
}
