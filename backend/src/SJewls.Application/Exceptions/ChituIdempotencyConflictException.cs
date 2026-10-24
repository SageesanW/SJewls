namespace SJewls.Application.Exceptions;

/// <summary>
/// Thrown when an idempotency key has already been used for a transaction with different parameters
/// (e.g. different customer, plan, or slot quantity). Maps to HTTP 409 Conflict.
/// </summary>
public class ChituIdempotencyConflictException : Exception
{
    public const string ErrorCode = "IdempotencyConflict";

    public string Code => ErrorCode;

    public ChituIdempotencyConflictException(string message = "Idempotency key has already been used for a different request.")
        : base(message)
    {
    }
}
