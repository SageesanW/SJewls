namespace SJewls.Application.Exceptions;

public class GoldRateUnavailableException : Exception
{
    public GoldRateUnavailableException(string message) : base(message)
    {
    }

    public GoldRateUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
