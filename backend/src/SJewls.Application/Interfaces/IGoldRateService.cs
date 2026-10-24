using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IGoldRateService
{
    /// <summary>
    /// Retrieves the authoritative applicable 24K gold rate in LKR per gram for the specified branch.
    /// Checks active Admin Manual Overrides first, then live GoldAPI.io market feeds, and lastly audited database rates.
    /// Throws GoldRateUnavailableException if no valid rate is available.
    /// </summary>
    Task<ApplicableGoldRateDto> GetApplicable24kRateAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the customer-facing gold rates (24K and 22K) for display in the mobile app and customer portal.
    /// </summary>
    Task<CustomerGoldRateResponse> GetCustomerRateAsync(Guid? branchId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the comprehensive gold rate overview for the admin portal, showing effective rates, live API status, and override settings.
    /// </summary>
    Task<GoldRateOverviewDto> GetOverviewAsync(Guid? branchId, CancellationToken ct = default);

    /// <summary>
    /// Saves or updates the manual gold rate override (24K & 22K per gram) for a branch or globally.
    /// </summary>
    Task<GoldRateOverviewDto> SetOverrideAsync(SetGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default);

    /// <summary>
    /// Toggles the manual gold rate override enabled/disabled status.
    /// </summary>
    Task<GoldRateOverviewDto> ToggleOverrideAsync(ToggleGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the historical audit trail of gold rates recorded in the system.
    /// </summary>
    Task<List<GoldRateHistoryDto>> GetHistoryAsync(Guid? branchId, int limit = 50, CancellationToken ct = default);
}
