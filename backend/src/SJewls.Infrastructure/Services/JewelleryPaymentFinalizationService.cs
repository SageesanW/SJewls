using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Domain.Enums;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class JewelleryPaymentFinalizationService : IJewelleryPaymentFinalizationService
{
    private readonly SJewlsDbContext _db;
    private readonly ICustomerNotificationService _notificationService;
    private readonly ILogger<JewelleryPaymentFinalizationService> _logger;

    public JewelleryPaymentFinalizationService(
        SJewlsDbContext db,
        ICustomerNotificationService notificationService,
        ILogger<JewelleryPaymentFinalizationService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PaymentFinalizationResult> FinalizePaymentAsync(
        PaymentVerificationPayload payload,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(payload.ProviderReference))
        {
            throw new ArgumentException("ProviderReference is required.", nameof(payload));
        }

        // 1. Check for duplicate webhook / callback retry (Idempotency)
        var existingPayment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ProviderReference == payload.ProviderReference.Trim(), ct);

        if (existingPayment != null && existingPayment.Status == PaymentStatus.Succeeded)
        {
            _logger.LogInformation("Payment {Ref} already finalized. Returning idempotent success.", payload.ProviderReference);
            return new PaymentFinalizationResult
            {
                Success = true,
                Message = "Payment has already been processed successfully.",
                PaymentId = existingPayment.Id
            };
        }

        // 2. Fetch locked quote and associated enrolment
        var quote = await _db.JewelleryPaymentQuotes
            .Include(q => q.JewelleryEnrolment)
                .ThenInclude(e => e!.Customer)
            .FirstOrDefaultAsync(q => q.Id == payload.QuoteId, ct)
            ?? throw new KeyNotFoundException($"Quote '{payload.QuoteId}' not found.");

        if (quote.JewelleryEnrolment == null)
        {
            throw new InvalidOperationException("Associated enrolment for this quote was not found.");
        }

        var enrolment = quote.JewelleryEnrolment;

        // 3. Verify currency and payable amount
        if (!string.Equals(payload.Currency, "LKR", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(quote.Currency, payload.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Currency mismatch: expected {quote.Currency}, received {payload.Currency}.");
        }

        if (Math.Round(payload.AmountPaid, 2) != Math.Round(quote.AmountToPay, 2))
        {
            throw new InvalidOperationException($"Amount mismatch: quote requires LKR {quote.AmountToPay:N2}, verified payment is LKR {payload.AmountPaid:N2}.");
        }

        // 4. Concurrency & target check
        var remainingTarget = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
        var creditingGrams = quote.TotalGoldCredit;

        var isRelational = _db.Database.IsRelational();
        await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Create payment entity
            var payment = new Payment
            {
                BranchId = enrolment.BranchId,
                CustomerId = enrolment.CustomerId,
                Module = BusinessModule.Jewellery,
                Amount = quote.AmountToPay,
                Currency = "LKR",
                Method = PaymentMethod.MockOnline,
                Status = PaymentStatus.Succeeded,
                ProviderReference = payload.ProviderReference.Trim(),
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Remarks = $"Finalized via {payload.ProviderName} for quote {quote.Id}"
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(ct);

            // Record payment allocation
            var allocation = new PaymentAllocation
            {
                PaymentId = payment.Id,
                AllocationType = "JewelleryContribution",
                TargetEntityId = enrolment.Id,
                AllocatedAmount = quote.AmountToPay
            };
            _db.PaymentAllocations.Add(allocation);

            // Reconciliation check: If concurrent activity caused target to be exceeded
            if (creditingGrams > remainingTarget)
            {
                _logger.LogWarning("Payment {PaymentId} collected LKR {Amount}, but crediting {Grams}g exceeds remaining target {Remaining}g. Initiating manual reconciliation audit.",
                    payment.Id, quote.AmountToPay, creditingGrams, remainingTarget);

                payment.Remarks = $"COLLECTED BUT OVERCREDIT: Crediting {creditingGrams}g exceeds remaining {remainingTarget}g. Requires store reconciliation.";
                quote.Status = JewelleryQuoteStatus.Used;
                quote.PaymentId = payment.Id;

                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);

                return new PaymentFinalizationResult
                {
                    Success = false,
                    Message = "Payment collected, but crediting gold exceeds remaining target. Transaction flagged for store reconciliation.",
                    PaymentId = payment.Id,
                    GoldGramsCredited = 0m,
                    NewTotalSavedGrams = enrolment.TotalSavedGrams,
                    PlanCompleted = false
                };
            }

            // Record contribution
            var contribution = new JewelleryContribution
            {
                JewelleryEnrolmentId = enrolment.Id,
                CurrencyAmount = quote.AmountToPay,
                GoldGrams = creditingGrams, // strictly TotalGoldCredit
                AppliedRatePerGram = quote.RatePerGram,
                Karat = 24, // internal standard
                PaymentId = payment.Id,
                ConfirmedAtUtc = DateTimeOffset.UtcNow
            };
            _db.JewelleryContributions.Add(contribution);

            // Update enrolment balances
            enrolment.TotalSavedGrams += creditingGrams;
            enrolment.TotalContributedCurrency += quote.AmountToPay;

            // Mark quote used
            quote.Status = JewelleryQuoteStatus.Used;
            quote.PaymentId = payment.Id;

            // Check completion
            var planCompleted = false;
            if (enrolment.TotalSavedGrams >= enrolment.TargetProductWeightGrams)
            {
                enrolment.Status = JewelleryEnrolmentStatus.Completed;
                planCompleted = true;

                // Send completion notification (once, deduplicated)
                await _notificationService.CreateNotificationAsync(
                    enrolment.CustomerId,
                    "Jewellery Plan Completed!",
                    $"Congratulations! You have reached your gold target of {enrolment.TargetProductWeightGrams}g for {enrolment.PlanNameSnapshot}.",
                    "completion",
                    ct);

                _logger.LogInformation("Enrolment {EnrolmentId} completed target of {Target}g", enrolment.Id, enrolment.TargetProductWeightGrams);
            }

            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);

            _logger.LogInformation("Finalized payment {PaymentId} for enrolment {EnrolmentId}: credited {Grams}g, new total={Total}g",
                payment.Id, enrolment.Id, creditingGrams, enrolment.TotalSavedGrams);

            return new PaymentFinalizationResult
            {
                Success = true,
                Message = planCompleted ? "Payment processed and plan target completed!" : "Payment processed and gold credited.",
                PaymentId = payment.Id,
                GoldGramsCredited = creditingGrams,
                NewTotalSavedGrams = enrolment.TotalSavedGrams,
                PlanCompleted = planCompleted
            };
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Transaction rolled back during payment finalization for quote {QuoteId}", payload.QuoteId);
            throw;
        }
    }
}
