using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class PlanAlertService : IPlanAlertService
{
    private readonly SJewlsDbContext _db;
    private readonly ILogger<PlanAlertService> _logger;

    public PlanAlertService(
        SJewlsDbContext db,
        ILogger<PlanAlertService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerPlanAlertDto> AcknowledgeAlertAsync(Guid customerId, Guid alertId, CancellationToken ct = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer != null && !customer.IsActive)
        {
            throw new InvalidOperationException("Customer account is inactive.");
        }

        var alert = await _db.PlanAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId, ct)
            ?? throw new KeyNotFoundException($"Plan alert '{alertId}' was not found.");

        if (alert.CustomerId != customerId)
        {
            throw new UnauthorizedAccessException("You do not own this plan alert.");
        }

        var now = DateTimeOffset.UtcNow;
        if (_db.Database.IsRelational())
        {
            // Atomic conditional update in database:
            // Sets IsAcknowledged = TRUE and AcknowledgedAtUtc = COALESCE(AcknowledgedAtUtc, {now}).
            // This strictly preserves the first acknowledgement timestamp under concurrent executions.
            await _db.Database.ExecuteSqlRawAsync(
                @"UPDATE ""PlanAlerts"" 
                  SET ""IsAcknowledged"" = TRUE, 
                      ""AcknowledgedAtUtc"" = COALESCE(""AcknowledgedAtUtc"", {0}),
                      ""UpdatedAtUtc"" = {0}
                  WHERE ""Id"" = {1} AND ""CustomerId"" = {2}",
                new object[] { now, alertId, customerId },
                ct);

            // Reload the persisted result to return the canonical, first-persisted timestamp
            await _db.Entry(alert).ReloadAsync(ct);
        }
        else
        {
            // Non-relational / in-memory fallback
            if (!alert.IsAcknowledged)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedAtUtc ??= now;
                await _db.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("Plan alert {AlertId} acknowledged by customer {CustomerId} at {Timestamp}",
            alert.Id, customerId, alert.AcknowledgedAtUtc);

        return MapToDto(alert);
    }

    public async Task<List<CustomerPlanAlertDto>> GetCustomerPlanAlertsAsync(Guid customerId, Guid? planId = null, CancellationToken ct = default)
    {
        var query = _db.PlanAlerts
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId);

        if (planId.HasValue)
        {
            query = query.Where(a => a.PlanId == planId.Value);
        }

        var alerts = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.ChituRoundIndex)
            .ToListAsync(ct);

        return alerts.Select(MapToDto).ToList();
    }

    public async Task<CustomerPlanAlertDto?> GetEnrolmentAlertAsync(Guid customerId, Guid enrolmentId, CancellationToken ct = default)
    {
        var alert = await _db.PlanAlerts
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId &&
                        a.JewelleryEnrolmentId == enrolmentId &&
                        (a.EventType == "JewelleryCompletion" || a.EventType == "JewelleryPlanCompletion"))
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return alert != null ? MapToDto(alert) : null;
    }

    public async Task<CustomerPlanAlertDto?> GetSlotAlertAsync(Guid customerId, Guid slotId, CancellationToken ct = default)
    {
        var alert = await _db.PlanAlerts
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId &&
                        a.ChituSlotId == slotId &&
                        a.EventType == "ChituWinner")
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return alert != null ? MapToDto(alert) : null;
    }

    public CustomerPlanAlertDto MapToDto(PlanAlert alert)
    {
        return new CustomerPlanAlertDto
        {
            AlertId = alert.Id,
            EventType = alert.EventType == "JewelleryPlanCompletion" ? "JewelleryCompletion" : alert.EventType,
            Title = alert.Title,
            Message = alert.Message,
            PlanId = alert.PlanId,
            PlanName = alert.PlanNameSnapshot,
            ReferenceId = alert.ReferenceId,
            SlotId = alert.ChituSlotId,
            SlotNumber = alert.ChituSlotNumber,
            SlotDisplayCode = alert.ChituSlotDisplayCode,
            RoundIndex = alert.ChituRoundIndex,
            ScheduledMonthYear = alert.ChituRoundScheduledMonthYear,
            RedemptionType = alert.RedemptionType?.ToString(),
            PrizeAmount = alert.PrizeAmount,
            EnrolmentId = alert.JewelleryEnrolmentId,
            TargetGrams = alert.TargetGrams,
            BranchId = alert.BranchId,
            BranchName = alert.BranchName,
            OutletAddress = alert.BranchAddress,
            OutletPhone = alert.BranchPhone,
            EventTimestampUtc = alert.CreatedAtUtc,
            AcknowledgedAtUtc = alert.AcknowledgedAtUtc,
            IsAcknowledged = alert.IsAcknowledged,
            ShouldShowPopup = !alert.IsAcknowledged,
            NavigationTarget = alert.NavigationTarget,
            CollectionInstruction = alert.CollectionInstruction
        };
    }
}
