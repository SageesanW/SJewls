using SJewls.Application.DTOs;

namespace SJewls.Application.Interfaces;

public interface IJewelleryPaymentFinalizationService
{
    /// <summary>
    /// Atomically and idempotently finalizes an external payment using verified server-side evidence.
    /// Deduplicates duplicate gateway callbacks, locks the quote, increments the gold balance,
    /// logs financial audit records, and triggers completion notifications when the plan target is reached.
    /// </summary>
    Task<PaymentFinalizationResult> FinalizePaymentAsync(PaymentVerificationPayload payload, CancellationToken ct = default);
}
