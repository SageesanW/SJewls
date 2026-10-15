namespace SJewls.Application.Exceptions;

/// <summary>
/// Thrown when a customer attempts to enrol in a jewellery plan they have previously joined
/// without explicitly confirming their intention to create an additional separate enrolment.
/// </summary>
public class RepeatEnrolmentConfirmationRequiredException : Exception
{
    public const string ErrorCode = "RepeatEnrolmentConfirmationRequired";

    public string Code => ErrorCode;
    public int TotalEnrolmentCount { get; }
    public int ActiveEnrolmentCount { get; }

    public RepeatEnrolmentConfirmationRequiredException(
        int totalEnrolmentCount,
        int activeEnrolmentCount,
        string message = "You have previously enrolled in this jewellery plan. Confirmation is required to create an additional separate enrolment.")
        : base(message)
    {
        TotalEnrolmentCount = totalEnrolmentCount;
        ActiveEnrolmentCount = activeEnrolmentCount;
    }
}
