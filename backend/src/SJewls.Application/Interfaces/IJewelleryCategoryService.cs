using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IJewelleryCategoryService
{
    Task<JewelleryCategoryPagedResponse> GetCategoriesAsync(
        Guid currentStaffId,
        string? search = null,
        bool? isActive = null,
        Guid? branchId = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<JewelleryCategoryDto?> GetCategoryByIdAsync(
        Guid id,
        Guid currentStaffId,
        CancellationToken cancellationToken = default);

    Task<JewelleryCategoryDto> CreateCategoryAsync(
        CreateJewelleryCategoryRequest request,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<JewelleryCategoryDto> UpdateCategoryAsync(
        Guid id,
        UpdateJewelleryCategoryRequest request,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<JewelleryCategoryDto> UpdateCategoryStatusAsync(
        Guid id,
        bool isActive,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task DeleteCategoryAsync(
        Guid id,
        Guid currentStaffId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Centralized retrieval for future customer/mobile APIs — returns active categories for an eligible branch.
    /// Prepared for future mobile browsing without speculative endpoints.
    /// </summary>
    Task<List<JewelleryCategoryDto>> GetActiveCategoriesForCustomerBrowsingAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);
}
