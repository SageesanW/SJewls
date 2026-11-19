using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
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

    /// <summary>
    /// Test hook executed inside the transaction after checking existingAlert == null,
    /// right before attempting to save the new PlanAlert.
    /// Allows tests to inject a concurrent commit to verify savepoint rollback.
    /// </summary>
    public Func<Task>? BeforeAlertSaveHook { get; set; }
    public Func<int, Task>? OnAttemptHook { get; set; }
    private int _attemptCount = 0;

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

        var strategy = _db.Database.CreateExecutionStrategy();
        var executionAttempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            executionAttempt++;
            _attemptCount++;
            if (OnAttemptHook != null)
            {
                await OnAttemptHook(_attemptCount);
            }

            // Clear any detached/dirty entities from previous failed attempts only
            if (executionAttempt > 1)
            {
                _db.ChangeTracker.Clear();
            }

            var isRelational = _db.Database.IsRelational();
            await using var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                // 1. Fetch fresh tracked quote and associated enrolment INSIDE this attempt
                var quote = await _db.JewelleryPaymentQuotes
                    .Include(q => q.JewelleryEnrolment)
                        .ThenInclude(e => e!.Customer)
                    .Include(q => q.JewelleryEnrolment)
                        .ThenInclude(e => e!.Branch)
                    .FirstOrDefaultAsync(q => q.Id == payload.QuoteId, ct)
                    ?? throw new KeyNotFoundException($"Quote '{payload.QuoteId}' not found.");

                if (quote.JewelleryEnrolment == null)
                {
                    throw new InvalidOperationException("Associated enrolment for this quote was not found.");
                }

                var enrolment = quote.JewelleryEnrolment;

                // 2. Lock enrolment in database if relational
                if (isRelational)
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"JewelleryEnrolments\" WHERE \"Id\" = {0} FOR NO KEY UPDATE",
                        new object[] { enrolment.Id },
                        ct);

                    await _db.Entry(enrolment).ReloadAsync(ct);
                    await _db.Entry(quote).ReloadAsync(ct);
                }

                // 3. Inspect existing payment by ProviderReference: validate quote ownership and handle idempotency
                var existingPayment = await _db.Payments
                    .Include(p => p.Allocations)
                    .FirstOrDefaultAsync(p => p.ProviderReference == payload.ProviderReference.Trim(), ct);

                if (existingPayment != null)
                {
                    // Validate that repeated provider references belong to the exact same payment/quote
                    var belongsToQuote = quote.PaymentId.HasValue && quote.PaymentId.Value == existingPayment.Id;

                    if (!belongsToQuote)
                    {
                        throw new InvalidOperationException($"Provider reference '{payload.ProviderReference}' has already been used for another payment.");
                    }

                    if (existingPayment.Status == PaymentStatus.RequiresReconciliation)
                    {
                        _logger.LogInformation("Payment {Ref} already finalized in RequiresReconciliation status. Returning existing reconciliation outcome.", payload.ProviderReference);
                        return new PaymentFinalizationResult
                        {
                            Success = false,
                            RequiresReconciliation = true,
                            Message = "Payment collected, but crediting gold exceeds remaining target. Transaction flagged for store reconciliation.",
                            PaymentId = existingPayment.Id,
                            GoldGramsCredited = 0m,
                            QuotedGoldGrams = quote.TotalGoldCredit,
                            AmountCollected = existingPayment.Amount,
                            NewTotalSavedGrams = enrolment.TotalSavedGrams,
                            PlanCompleted = false
                        };
                    }

                    if (existingPayment.Status == PaymentStatus.Succeeded)
                    {
                        _logger.LogInformation("Payment {Ref} already finalized. Returning idempotent success.", payload.ProviderReference);
                        return new PaymentFinalizationResult
                        {
                            Success = true,
                            Message = "Payment has already been processed successfully.",
                            PaymentId = existingPayment.Id,
                            PlanCompleted = enrolment.Status == JewelleryEnrolmentStatus.Completed,
                            NewTotalSavedGrams = enrolment.TotalSavedGrams
                        };
                    }
                }

                // 4. Replay check: If quote is already used, inspect linked payment and validate provider reference
                if (quote.Status == JewelleryQuoteStatus.Used)
                {
                    Payment? linkedPayment = null;
                    if (quote.PaymentId.HasValue)
                    {
                        linkedPayment = await _db.Payments.FindAsync(new object[] { quote.PaymentId.Value }, ct);
                    }

                    if (linkedPayment != null)
                    {
                        if (!string.Equals(linkedPayment.ProviderReference.Trim(), payload.ProviderReference.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Quote '{quote.Id}' has already been finalized with provider reference '{linkedPayment.ProviderReference}'. Replay attempts must supply the original provider reference.");
                        }

                        if (linkedPayment.Status == PaymentStatus.RequiresReconciliation)
                        {
                            _logger.LogInformation("Quote {QuoteId} was already finalized in RequiresReconciliation status. Returning existing reconciliation outcome.", quote.Id);
                            return new PaymentFinalizationResult
                            {
                                Success = false,
                                RequiresReconciliation = true,
                                Message = "Payment collected, but crediting gold exceeds remaining target. Transaction flagged for store reconciliation.",
                                PaymentId = linkedPayment.Id,
                                GoldGramsCredited = 0m,
                                QuotedGoldGrams = quote.TotalGoldCredit,
                                AmountCollected = linkedPayment.Amount,
                                NewTotalSavedGrams = enrolment.TotalSavedGrams,
                                PlanCompleted = false
                            };
                        }

                        if (linkedPayment.Status == PaymentStatus.Succeeded)
                        {
                            return new PaymentFinalizationResult
                            {
                                Success = true,
                                Message = "Payment has already been processed successfully for this quote.",
                                PaymentId = quote.PaymentId ?? Guid.Empty,
                                PlanCompleted = enrolment.Status == JewelleryEnrolmentStatus.Completed,
                                NewTotalSavedGrams = enrolment.TotalSavedGrams
                            };
                        }
                    }

                    return new PaymentFinalizationResult
                    {
                        Success = true,
                        Message = "Payment has already been processed successfully for this quote.",
                        PaymentId = quote.PaymentId ?? Guid.Empty,
                        PlanCompleted = enrolment.Status == JewelleryEnrolmentStatus.Completed,
                        NewTotalSavedGrams = enrolment.TotalSavedGrams
                    };
                }

                // 5. Validate quote status and expiry
                if (quote.Status == JewelleryQuoteStatus.Expired || (quote.Status == JewelleryQuoteStatus.Active && quote.ExpiresAtUtc < DateTimeOffset.UtcNow))
                {
                    throw new InvalidOperationException("This quote has expired. Please request a fresh rate calculation quote.");
                }

                if (quote.Status == JewelleryQuoteStatus.Cancelled)
                {
                    throw new InvalidOperationException("This quote has been cancelled.");
                }

                // 6. Verify currency and payable amount
                if (!string.Equals(payload.Currency, "LKR", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(quote.Currency, payload.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Currency mismatch: expected {quote.Currency}, received {payload.Currency}.");
                }

                if (Math.Round(payload.AmountPaid, 2) != Math.Round(quote.AmountToPay, 2))
                {
                    throw new InvalidOperationException($"Amount mismatch: quote requires LKR {quote.AmountToPay:N2}, verified payment is LKR {payload.AmountPaid:N2}.");
                }

                // 6. Concurrency & target check based on fresh reloaded state
                var currentRemainingTarget = Math.Max(0m, enrolment.TargetProductWeightGrams - enrolment.TotalSavedGrams);
                var creditingGrams = quote.TotalGoldCredit;

                // Reconciliation check: If concurrent activity caused target to be exceeded
                if (creditingGrams > currentRemainingTarget)
                {
                    _logger.LogWarning("Payment collected LKR {Amount}, but crediting {Grams}g exceeds remaining target {Remaining}g. Placing in explicit reconciliation state.",
                        quote.AmountToPay, creditingGrams, currentRemainingTarget);

                    // Create payment in explicit reconciliation state (preserving full money received, zero gold credited to prevent overrun)
                    var reconciliationPayment = new Payment
                    {
                        BranchId = enrolment.BranchId,
                        CustomerId = enrolment.CustomerId,
                        Module = BusinessModule.Jewellery,
                        Amount = quote.AmountToPay,
                        Currency = "LKR",
                        Method = PaymentMethod.MockOnline,
                        Status = PaymentStatus.RequiresReconciliation,
                        ProviderReference = payload.ProviderReference.Trim(),
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        Remarks = $"REQUIRES RECONCILIATION: Payment collected LKR {quote.AmountToPay:F2} for quoted {creditingGrams:F4}g, but crediting exceeds remaining target {currentRemainingTarget:F4}g. Zero gold credited to prevent target overrun. Flagged for store reconciliation."
                    };

                    _db.Payments.Add(reconciliationPayment);
                    await _db.SaveChangesAsync(ct);

                    var reconciliationAllocation = new PaymentAllocation
                    {
                        PaymentId = reconciliationPayment.Id,
                        AllocationType = "JewelleryContribution",
                        TargetEntityId = enrolment.Id,
                        AllocatedAmount = quote.AmountToPay
                    };
                    _db.PaymentAllocations.Add(reconciliationAllocation);

                    // Quote retains original quoted grams and locked financial values
                    quote.Status = JewelleryQuoteStatus.Used;
                    quote.PaymentId = reconciliationPayment.Id;

                    await _db.SaveChangesAsync(ct);
                    if (transaction != null) await transaction.CommitAsync(ct);

                    return new PaymentFinalizationResult
                    {
                        Success = false,
                        RequiresReconciliation = true,
                        Message = $"Payment collected (LKR {quote.AmountToPay:F2}), but crediting {creditingGrams:F4}g exceeds remaining target ({currentRemainingTarget:F4}g). Transaction placed in explicit reconciliation state.",
                        PaymentId = reconciliationPayment.Id,
                        GoldGramsCredited = 0m,
                        QuotedGoldGrams = creditingGrams,
                        AmountCollected = quote.AmountToPay,
                        NewTotalSavedGrams = enrolment.TotalSavedGrams,
                        PlanCompleted = false
                    };
                }

                // Normal payment flow: target can accommodate full quoted grams
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
                }

                // 1. Persist financial changes first within the database transaction
                await _db.SaveChangesAsync(ct);

                // 2. Persist completion alert and notification if plan completed
                if (planCompleted)
                {
                    var branchName = enrolment.Branch?.Name ?? "our branch";
                    var completionTitle = "Jewellery Plan Completed!";
                    var completionMessage = $"Congratulations! You have completed {enrolment.PlanNameSnapshot} and reached your target of {enrolment.TargetProductWeightGrams:0.##} g. Visit {branchName} to collect your jewellery.";

                    // Check if completion alert already exists to prevent duplicate (reconciling both JewelleryCompletion and JewelleryPlanCompletion)
                    var existingAlert = await _db.PlanAlerts
                        .FirstOrDefaultAsync(a => a.JewelleryEnrolmentId == enrolment.Id &&
                            (a.EventType == "JewelleryCompletion" || a.EventType == "JewelleryPlanCompletion"), ct);

                    if (existingAlert == null)
                    {
                        var savepointName = "BeforeCompletionAlert";
                        var hasSavepoint = false;

                        if (transaction != null && _db.Database.IsRelational())
                        {
                            await transaction.CreateSavepointAsync(savepointName, ct);
                            hasSavepoint = true;
                        }

                        var notification = new Notification
                        {
                            CustomerId = enrolment.CustomerId,
                            Title = completionTitle,
                            Message = completionMessage,
                            Type = "completion",
                            IsRead = false,
                            ReferenceId = enrolment.Id,
                            ReferenceType = "JewelleryEnrolment",
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        };
                        _db.Notifications.Add(notification);

                        var alert = new PlanAlert
                        {
                            CustomerId = enrolment.CustomerId,
                            EventType = "JewelleryCompletion",
                            Module = BusinessModule.Jewellery,
                            Title = completionTitle,
                            Message = completionMessage,
                            PlanId = enrolment.JewelleryPlanId,
                            PlanNameSnapshot = enrolment.PlanNameSnapshot,
                            ReferenceId = enrolment.Id,
                            JewelleryEnrolmentId = enrolment.Id,
                            TargetGrams = enrolment.TargetProductWeightGrams,
                            BranchId = enrolment.BranchId,
                            BranchName = branchName,
                            BranchAddress = enrolment.Branch?.Address,
                            BranchPhone = null,
                            NotificationId = notification.Id,
                            IsAcknowledged = false,
                            NavigationTarget = $"jewellery-enrolments/{enrolment.Id}",
                            CollectionInstruction = $"Visit {branchName} to collect your jewellery.",
                            CreatedAtUtc = DateTimeOffset.UtcNow
                        };
                        _db.PlanAlerts.Add(alert);
                        notification.PlanAlertId = alert.Id;

                        // Controlled test instrumentation: allow tests to execute a concurrent commit here
                        if (BeforeAlertSaveHook != null)
                        {
                            await BeforeAlertSaveHook.Invoke();
                        }

                        try
                        {
                            await _db.SaveChangesAsync(ct);
                        }
                        catch (DbUpdateException dbEx) when (IsAlertUniquenessViolation(dbEx))
                        {
                            // A concurrent transaction already committed the completion alert;
                            // Roll back to savepoint to ensure no orphan notification exists in PostgreSQL,
                            // detach duplicate entities from change tracker, and preserve intact financial updates.
                            _logger.LogInformation("Concurrent transaction already created alert for enrolment {EnrolmentId}. Safely rolling back to savepoint and detaching duplicate alert and notification.", enrolment.Id);

                            if (hasSavepoint)
                            {
                                await transaction.RollbackToSavepointAsync(savepointName, ct);
                            }

                            _db.Entry(alert).State = EntityState.Detached;
                            _db.Entry(notification).State = EntityState.Detached;

                            // Explicit verification: Ensure canonical alert and linked notification exist
                            var canonicalAlert = await _db.PlanAlerts
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.JewelleryEnrolmentId == enrolment.Id &&
                                    (a.EventType == "JewelleryCompletion" || a.EventType == "JewelleryPlanCompletion"), ct);

                            if (canonicalAlert == null || !canonicalAlert.NotificationId.HasValue)
                            {
                                _logger.LogError("Duplicate alert constraint hit for enrolment {EnrolmentId}, but canonical alert/notification could not be verified. Rolling back entire transaction.", enrolment.Id);
                                throw new InvalidOperationException($"Failed to verify existing canonical completion alert for enrolment {enrolment.Id}. Transaction aborted.");
                            }

                            var canonicalNotification = await _db.Notifications
                                .AsNoTracking()
                                .FirstOrDefaultAsync(n => n.Id == canonicalAlert.NotificationId.Value, ct);

                            if (canonicalNotification == null)
                            {
                                _logger.LogError("Canonical alert {AlertId} exists for enrolment {EnrolmentId}, but linked notification {NotificationId} is missing. Rolling back entire transaction.",
                                    canonicalAlert.Id, enrolment.Id, canonicalAlert.NotificationId);
                                throw new InvalidOperationException($"Linked notification {canonicalAlert.NotificationId} missing for canonical alert {canonicalAlert.Id}. Transaction aborted.");
                            }
                        }
                    }

                    _logger.LogInformation("Enrolment {EnrolmentId} completed target of {Target}g", enrolment.Id, enrolment.TargetProductWeightGrams);
                }

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
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Transaction rolled back during payment finalization for quote {QuoteId}", payload.QuoteId);
                throw;
            }
        });
    }

    private static bool IsAlertUniquenessViolation(DbUpdateException dbEx)
    {
        if (dbEx.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
                   (string.Equals(pgEx.ConstraintName, "IX_PlanAlerts_JewelleryEnrolmentId_EventType", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pgEx.ConstraintName, "IX_PlanAlerts_JewelleryEnrolmentId", StringComparison.OrdinalIgnoreCase));
        }

        // Fallback for in-memory or mock testing where PostgresException is simulated
        return dbEx.InnerException?.Message.Contains("IX_PlanAlerts") == true;
    }
}
