using SJewls.Application.DTOs;
using SJewls.Domain.Entities;

namespace SJewls.Application.Interfaces;

public interface IPlanAlertService
{
    Task<CustomerPlanAlertDto> AcknowledgeAlertAsync(Guid customerId, Guid alertId, CancellationToken ct = default);
    Task<List<CustomerPlanAlertDto>> GetCustomerPlanAlertsAsync(Guid customerId, Guid? planId = null, CancellationToken ct = default);
    Task<CustomerPlanAlertDto?> GetEnrolmentAlertAsync(Guid customerId, Guid enrolmentId, CancellationToken ct = default);
    Task<CustomerPlanAlertDto?> GetSlotAlertAsync(Guid customerId, Guid slotId, CancellationToken ct = default);
    CustomerPlanAlertDto MapToDto(PlanAlert alert);
}
