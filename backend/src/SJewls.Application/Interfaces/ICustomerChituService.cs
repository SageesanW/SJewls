using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface ICustomerChituService
{
    // Active Chitu Discovery
    Task<CustomerChituPlansPagedResponse> GetAvailablePlansAsync(Guid customerId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CustomerChituPlanDetailDto> GetPlanDetailsAsync(Guid customerId, Guid planId, CancellationToken ct = default);

    // Joining & Purchasing Slots
    Task<CustomerChituJoinPreviewResponse> PreviewJoinAsync(Guid customerId, Guid planId, CustomerChituJoinPreviewRequest request, CancellationToken ct = default);
    Task<CustomerChituJoinResponse> ConfirmAndJoinAsync(Guid customerId, Guid planId, CustomerChituJoinRequest request, string? ipAddress = null, CancellationToken ct = default);

    // My Plans (Customer Portfolio)
    Task<CustomerChituInvestmentsPagedResponse> GetInvestmentsAsync(Guid customerId, string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<CustomerChituInvestmentPlanDto> GetInvestmentPlanDetailsAsync(Guid customerId, Guid planId, CancellationToken ct = default);
    Task<CustomerChituSlotDetailDto> GetSlotDetailsAsync(Guid customerId, Guid slotId, CancellationToken ct = default);
    Task<List<CustomerPaymentSummaryDto>> GetSlotPaymentsAsync(Guid customerId, Guid slotId, CancellationToken ct = default);

    // Existing-Slot Contributions
    Task<CustomerPayableSlotsDto> GetPayableSlotsAsync(Guid customerId, Guid planId, CancellationToken ct = default);
    Task<CustomerChituPaymentPreviewResponse> PreviewExistingSlotPaymentAsync(Guid customerId, Guid planId, CustomerChituPaymentPreviewRequest request, CancellationToken ct = default);
    Task<CustomerChituPaymentConfirmResponse> ConfirmExistingSlotPaymentAsync(Guid customerId, Guid planId, CustomerChituPaymentConfirmRequest request, string? ipAddress = null, CancellationToken ct = default);
}
