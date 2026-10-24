namespace SJewls.Application.Exceptions;

/// <summary>
/// Thrown when no real payment provider is integrated and simulation mode is disabled in configuration.
/// </summary>
public class PaymentProviderNotConfiguredException : Exception
{
    public const string ErrorCode = "PaymentProviderNotConfigured";

    public string Code => ErrorCode;

    public PaymentProviderNotConfiguredException(string message = "Payment provider is not configured. Real payment gateway integration is pending, and simulation mode is disabled.")
        : base(message)
    {
    }
}
