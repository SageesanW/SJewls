namespace SJewls.Application.Exceptions;

/// <summary>
/// Thrown when a jewellery plan enrolment operation encounters an authoritative business conflict
/// (such as plan inactive, not yet open for enrolments, customer inactive, or idempotency key payload mismatch).
/// Maps to HTTP 409 Conflict.
/// </summary>
public class JewelleryPlanConflictException : InvalidOperationException
{
    public const string DefaultErrorCode = "JewelleryPlanConflict";

    public string Code { get; }

    public JewelleryPlanConflictException(string message, string code = DefaultErrorCode)
        : base(message)
    {
        Code = code;
    }
}

