namespace SJewls.Application.DTOs;

public class ApplicableGoldRateDto
{
    /// <summary>
    /// Applicable 24K gold rate in LKR per gram.
    /// Internal financial models preserve the 24K basis; customer-facing presentation omits the karat label.
    /// </summary>
    public decimal RatePerGram { get; set; }

    /// <summary>
    /// Currency code (strictly LKR).
    /// </summary>
    public string Currency { get; set; } = "LKR";

    /// <summary>
    /// Data provider origin (e.g., "GoldAPI.io" or "BranchManual").
    /// </summary>
    public string Source { get; set; } = "GoldAPI.io";

    /// <summary>
    /// Timestamp when this rate was published by the source.
    /// </summary>
    public DateTimeOffset SourceTimestampUtc { get; set; }

    /// <summary>
    /// Timestamp when this rate was retrieved/cached by the server.
    /// </summary>
    public DateTimeOffset RetrievedAtUtc { get; set; }

    /// <summary>
    /// True if the rate is within the configured freshness window.
    /// </summary>
    public bool IsFresh { get; set; } = true;
}
