using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface ICustomerJewelleryPlanService
{
    // Browsing
    Task<List<CustomerJewelleryCategoryDto>> GetCategoriesAsync(Guid customerId, CancellationToken ct = default);
    Task<List<CustomerJewelleryPlanDto>> GetPlansAsync(Guid customerId, Guid? categoryId = null, CancellationToken ct = default);
    Task<List<CustomerJewelleryPlanDto>> GetPlansByCategoryAsync(Guid customerId, Guid categoryId, CancellationToken ct = default);
    Task<CustomerJewelleryPlanDto> GetPlanDetailsAsync(Guid customerId, Guid planId, CancellationToken ct = default);

    // Enrolment
    Task<EnrolmentPreviewResponse> PreviewEnrolmentAsync(Guid customerId, EnrolmentPreviewRequest request, CancellationToken ct = default);
    Task<CustomerEnrolmentDto> CreateEnrolmentAsync(Guid customerId, CreateCustomerEnrolmentRequest request, CancellationToken ct = default);

    // My Investments
    Task<CustomerEnrolmentsPagedResponse> GetEnrolmentsAsync(Guid customerId, int page, int pageSize, CancellationToken ct = default);
    Task<CustomerEnrolmentDto> GetEnrolmentDetailsAsync(Guid customerId, Guid enrolmentId, CancellationToken ct = default);
    Task<List<CustomerEnrolmentPaymentDto>> GetEnrolmentPaymentsAsync(Guid customerId, Guid enrolmentId, CancellationToken ct = default);

    // Calculations & Quotes
    Task<JewelleryCalculationPreviewResponse> PreviewCalculationAsync(Guid customerId, Guid enrolmentId, JewelleryCalculationRequest request, CancellationToken ct = default);
    Task<CustomerPaymentQuoteDto> CreateLockedQuoteAsync(Guid customerId, Guid enrolmentId, JewelleryCalculationRequest request, CancellationToken ct = default);
    Task<CustomerPaymentQuoteDto> GetQuoteAsync(Guid customerId, Guid quoteId, CancellationToken ct = default);
    Task<PaymentInitiationResponse> InitiatePaymentAsync(Guid customerId, Guid quoteId, CancellationToken ct = default);
}
