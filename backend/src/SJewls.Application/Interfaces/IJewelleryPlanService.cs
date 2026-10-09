using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IJewelleryPlanService
{
    Task<JewelleryPlanPagedResponse> GetPlansAsync(
        Guid staffId,
        string? search,
        Guid? categoryId,
        string? status, // "Active", "Scheduled", "Deactivated", "All"
        Guid? branchId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<JewelleryPlanDto?> GetPlanByIdAsync(
        Guid id,
        Guid staffId,
        CancellationToken ct = default);

    Task<JewelleryPlanDto> CreatePlanAsync(
        CreateJewelleryPlanRequest request,
        Guid staffId,
        string? ipAddress,
        CancellationToken ct = default);

    Task<JewelleryPlanDto> UpdatePlanAsync(
        Guid id,
        UpdateJewelleryPlanRequest request,
        Guid staffId,
        string? ipAddress,
        CancellationToken ct = default);

    Task<JewelleryPlanDto> UpdatePlanStatusAsync(
        Guid id,
        UpdateJewelleryPlanStatusRequest request,
        Guid staffId,
        string? ipAddress,
        CancellationToken ct = default);

    Task<PlanCustomersPagedResponse> GetPlanCustomersAsync(
        Guid planId,
        Guid staffId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<FinancialQuoteResponse> CalculateFinancialQuoteAsync(
        FinancialQuoteRequest request,
        Guid staffId,
        CancellationToken ct = default);
}
