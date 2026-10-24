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
    /// Data provider origin (e.g., "GoldAPI.io", "ManualOverride", or "BranchManual").
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

public class CustomerGoldRateResponse
{
    public decimal Rate24kPerGram { get; set; }
    public decimal Rate22kPerGram { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Source { get; set; } = "ManualOverride";
    public bool IsOverridden { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
}

public class GoldRateOverviewDto
{
    public EffectiveGoldRateDetailDto EffectiveRate { get; set; } = new();
    public LiveApiGoldRateDetailDto LiveApiRate { get; set; } = new();
    public ManualOverrideDetailDto? ManualOverride { get; set; }
    public List<BranchGoldRateSummaryDto> Branches { get; set; } = new();
}

public class EffectiveGoldRateDetailDto
{
    public decimal Rate24k { get; set; }
    public decimal Rate22k { get; set; }
    public string Currency { get; set; } = "LKR";
    public string ActiveSource { get; set; } = "ManualOverride"; // "ManualOverride" or "LiveApi"
    public bool IsOverridden { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
}

public class LiveApiGoldRateDetailDto
{
    public decimal? Rate24k { get; set; }
    public decimal? Rate22k { get; set; }
    public string Currency { get; set; } = "LKR";
    public string Provider { get; set; } = "GoldAPI.io";
    public bool IsConfigured { get; set; }
    public bool IsAvailable { get; set; }
    public DateTimeOffset? TimestampUtc { get; set; }
    public string? StatusMessage { get; set; }
}

public class ManualOverrideDetailDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public decimal Rate24k { get; set; }
    public decimal Rate22k { get; set; }
    public bool IsEnabled { get; set; }
    public string? Reason { get; set; }
    public Guid? UpdatedByStaffId { get; set; }
    public string? UpdatedByStaffName { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public class BranchGoldRateSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Rate24k { get; set; }
    public decimal Rate22k { get; set; }
    public bool HasCustomOverride { get; set; }
    public bool IsOverrideEnabled { get; set; }
}

public class SetGoldRateOverrideRequest
{
    public Guid? BranchId { get; set; } // Null = Global
    public decimal Rate24k { get; set; }
    public decimal Rate22k { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Reason { get; set; }
}

public class ToggleGoldRateOverrideRequest
{
    public Guid? BranchId { get; set; } // Null = Global
    public bool IsEnabled { get; set; }
}

public class GoldRateHistoryDto
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int Karat { get; set; }
    public decimal RatePerGram { get; set; }
    public string Source { get; set; } = "ManualOverride";
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public Guid? RecordedByStaffId { get; set; }
    public string? RecordedByStaffName { get; set; }
    public string? Remarks { get; set; }
}
