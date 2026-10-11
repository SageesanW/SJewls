using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IGoldRateService
{
    /// <summary>
    /// Retrieves the latest valid applicable 24K gold rate in LKR per gram for the specified branch.
    /// Attempts GoldAPI.io first (with server-side caching), falling back to audited branch manual rates if needed.
    /// Throws GoldRateUnavailableException if no valid, fresh rate is available.
    /// </summary>
    Task<ApplicableGoldRateDto> GetApplicable24kRateAsync(Guid branchId, CancellationToken ct = default);
}
