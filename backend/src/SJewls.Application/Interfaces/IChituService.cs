using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IChituService
{
    // Plan Management
    Task<ChituPlanCardDto> CreatePlanAsync(Guid currentStaffId, CreateChituPlanRequest request, CancellationToken ct = default);
    Task<ChituPlanCardDto> UpdatePlanAsync(Guid currentStaffId, Guid planId, UpdateChituPlanRequest request, CancellationToken ct = default);
    Task<ChituPlanListResponse> GetPlansAsync(Guid currentStaffId, Guid? branchId = null, string? search = null, string? status = null, int? redemptionType = null, CancellationToken ct = default);
    Task<ChituPlanDetailDto> GetPlanByIdAsync(Guid currentStaffId, Guid planId, CancellationToken ct = default);

    // Slot Assignment
    Task<ChituSlotAssignmentPreviewResponse> PreviewAssignmentAsync(Guid currentStaffId, ChituSlotAssignmentPreviewRequest request, CancellationToken ct = default);
    Task<AssignChituSlotsResponse> AssignSlotsAsync(Guid currentStaffId, AssignChituSlotsRequest request, string? ipAddress = null, CancellationToken ct = default);

    // Payments
    Task<CustomerPayableSlotsResponse> GetCustomerPayableSlotsAsync(Guid currentStaffId, Guid planId, Guid customerId, CancellationToken ct = default);
    Task<ChituPaymentPreviewResponse> PreviewPaymentAsync(Guid currentStaffId, ChituPaymentPreviewRequest request, CancellationToken ct = default);
    Task<RecordChituPaymentResponse> RecordPaymentAsync(Guid currentStaffId, RecordChituPaymentRequest request, string? ipAddress = null, CancellationToken ct = default);

    // Physical Raffle Draws
    Task<ChituEligibleSlotsResponse> GetEligibleRaffleSlotsAsync(Guid currentStaffId, Guid planId, int roundIndex, string? search = null, CancellationToken ct = default);
    Task<RecordChituRaffleResponse> RecordRaffleDrawAsync(Guid currentStaffId, Guid planId, int roundIndex, RecordChituRaffleRequest request, string? ipAddress = null, CancellationToken ct = default);

    // Customer / Slot Roster
    Task<ChituRosterResponse> GetRosterAsync(Guid currentStaffId, Guid planId, ChituRosterQueryParameters query, CancellationToken ct = default);
}
