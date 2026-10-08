using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IAdminCustomerService
{
    Task<AdminCustomerPagedResponse> GetCustomersAsync(
        Guid currentStaffId,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        Guid? branchId = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerMetricsDto> GetCustomerMetricsAsync(
        Guid currentStaffId,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerStatisticsDto> GetCustomerStatisticsAsync(
        Guid currentStaffId,
        string? search = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerDetailDto> GetCustomerByIdAsync(
        Guid currentStaffId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerDetailDto> CreateCustomerAsync(
        Guid currentStaffId,
        CreateCustomerRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AdminCustomerDetailDto> UpdateCustomerStatusAsync(
        Guid currentStaffId,
        Guid customerId,
        UpdateCustomerStatusRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
