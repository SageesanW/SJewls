using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class OtpChallenge : BaseEntity
{
    public string ContactValue { get; set; } = string.Empty;
    public ContactType ContactType { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public bool IsConsumed { get; set; }
    public Guid? CustomerId { get; set; }
}
