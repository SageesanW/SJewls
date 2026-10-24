namespace SJewls.Application.Exceptions;

/// <summary>
/// Thrown when a customer or staff attempts to purchase or assign slots in a Chitu plan,
/// but insufficient capacity remains (e.g. during concurrent assignment race conditions).
/// Maps to HTTP 409 Conflict with stable code "ChituCapacityUnavailable".
/// </summary>
public class ChituCapacityUnavailableException : Exception
{
    public const string ErrorCode = "ChituCapacityUnavailable";

    public string Code => ErrorCode;

    public ChituCapacityUnavailableException(string message = "The requested slot capacity is no longer available.")
        : base(message)
    {
    }
}
