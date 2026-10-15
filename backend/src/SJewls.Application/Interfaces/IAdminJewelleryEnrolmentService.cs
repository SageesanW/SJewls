using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IAdminJewelleryEnrolmentService
{
    /// <summary>
    /// Previews eligibility and repeat enrolment status for assigning a plan to an existing customer.
    /// </summary>
    Task<AdminEnrolmentPreviewResponse> PreviewAssignmentAsync(
        Guid staffId,
        AdminEnrolmentPreviewRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns a jewellery savings plan to an authorized customer by staff.
    /// Snapshots target grams from the plan (read-only), enforces offered durations,
    /// checks branch eligibility, verifies repeat enrolment confirmation, and audits the action.
    /// </summary>
    Task<AdminCustomerEnrolmentDto> AssignPlanAsync(
        Guid staffId,
        AdminAssignPlanRequest request,
        string? ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Previews in-store payment calculation using current live 24K gold rate and enrolment premium snapshot.
    /// Calculates breakdowns without modifying balances or database state.
    /// </summary>
    Task<AdminRecordPaymentPreviewResponse> PreviewPaymentAsync(
        Guid staffId,
        Guid enrolmentId,
        AdminRecordPaymentPreviewRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Authoritatively records today's cash payment received in-store for an active enrolment.
    /// Validates today-only timestamp using branch timezone, uses live 24K rate, enforces remaining target limit,
    /// atomically creates payment + contribution, updates balances, and triggers completion notification if target reached.
    /// </summary>
    Task<AdminRecordPaymentResult> RecordPaymentAsync(
        Guid staffId,
        Guid enrolmentId,
        AdminRecordPaymentRequest request,
        string? ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single customer jewellery enrolment with its confirmed contributions.
    /// </summary>
    Task<AdminCustomerEnrolmentDto> GetEnrolmentByIdAsync(
        Guid staffId,
        Guid enrolmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the payment and contribution history for a specific enrolment.
    /// </summary>
    Task<List<EnrolmentContributionDto>> GetEnrolmentPaymentsAsync(
        Guid staffId,
        Guid enrolmentId,
        CancellationToken ct = default);
}
