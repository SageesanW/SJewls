using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface ICustomerPaymentService
{
    Task<CustomerPaymentsPagedResponse> GetPaymentHistoryAsync(
        Guid customerId,
        string? planType = null,
        Guid? planId = null,
        string? status = null,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<CustomerPaymentDetailDto> GetPaymentDetailAsync(
        Guid customerId,
        Guid paymentId,
        CancellationToken ct = default);
}
