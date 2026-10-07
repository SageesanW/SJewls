using SJewls.Domain.Common;
using SJewls.Domain.Enums;

namespace SJewls.Domain.Entities;

public class RegistrationSession : BaseEntity
{
    public string RegistrationToken { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public ContactType ContactType { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? CustomerId { get; set; }
}

public class RefreshToken : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
}
