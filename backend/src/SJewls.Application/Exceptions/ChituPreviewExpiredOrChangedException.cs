namespace SJewls.Application.Exceptions;

/// <summary>
/// Thrown when material terms, schedule, required instalments, or payable amounts changed between preview and confirmation.
/// A fresh preview is required.
/// </summary>
public class ChituPreviewExpiredOrChangedException : Exception
{
    public const string ErrorCode = "PreviewChanged";

    public string Code => ErrorCode;

    public decimal? ExpectedAmount { get; }
    public decimal? ActualAmount { get; }

    public ChituPreviewExpiredOrChangedException(
        string message = "The terms, required instalments, or slot states have changed since your preview. Please generate a fresh preview and confirm again.",
        decimal? expectedAmount = null,
        decimal? actualAmount = null)
        : base(message)
    {
        ExpectedAmount = expectedAmount;
        ActualAmount = actualAmount;
    }
}
