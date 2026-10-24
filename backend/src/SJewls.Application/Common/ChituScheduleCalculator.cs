using SJewls.Domain.Entities;
using SJewls.Domain.Enums;

namespace SJewls.Application.Common;

/// <summary>
/// Domain logic helper for Chitu monthly schedule, round indices, raffle eligibility, and slot financial metrics.
/// </summary>
public static class ChituScheduleCalculator
{
    /// <summary>
    /// Determines the current round index (1-based, up to DurationMonths) based on the current date and plan schedule.
    /// Before StartDate -> Round 1 (pre-enrolment period).
    /// After EndDate -> DurationMonths.
    /// </summary>
    public static int DetermineCurrentRoundIndex(ChituPlan plan, DateOnly today)
    {
        if (today < plan.StartDate)
        {
            return 1;
        }

        if (today >= plan.EndDate)
        {
            return plan.DurationMonths;
        }

        for (int r = plan.DurationMonths; r >= 1; r--)
        {
            var scheduledDate = plan.StartDate.AddMonths(r - 1);
            if (today >= scheduledDate)
            {
                return r;
            }
        }

        return 1;
    }

    /// <summary>
    /// Finds the scheduled date of the next unrecorded raffle draw, or null if all draws are recorded.
    /// </summary>
    public static (int? RoundIndex, DateOnly? ScheduledDate) DetermineNextRaffle(ChituPlan plan, IEnumerable<ChituDraw>? draws, DateOnly today)
    {
        var recordedDrawMonths = draws != null
            ? new HashSet<int>(draws.Select(d => d.DrawMonthIndex))
            : new HashSet<int>();

        for (int r = 1; r <= plan.DurationMonths; r++)
        {
            if (!recordedDrawMonths.Contains(r))
            {
                return (r, plan.StartDate.AddMonths(r - 1));
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Checks raffle eligibility for a slot in a target round (typically the next raffle or current round).
    /// Rules: Active slot, not already won, all instalments from Round 1 through target round must be fully paid.
    /// </summary>
    public static (bool IsEligible, string Reason) EvaluateRaffleEligibility(ChituSlot slot, int targetRoundIndex)
    {
        if (slot.Status == ChituSlotStatus.Won)
        {
            return (false, $"Slot has already won in round {slot.WinningRoundIndex?.ToString() ?? "a previous round"}.");
        }

        if (slot.Status != ChituSlotStatus.Active)
        {
            return (false, $"Slot is not active (Status: {slot.Status}).");
        }

        if (targetRoundIndex < 1)
        {
            targetRoundIndex = 1;
        }

        for (int m = 1; m <= targetRoundIndex; m++)
        {
            var inst = slot.Instalments.FirstOrDefault(i => i.MonthIndex == m);
            if (inst == null || inst.Status != InstalmentStatus.Paid)
            {
                return (false, $"Instalment for round {m} is unpaid. All instalments through round {targetRoundIndex} must be paid for raffle eligibility.");
            }
        }

        return (true, "Eligible for raffle draw.");
    }

    /// <summary>
    /// Calculates slot coverage metrics including paid-through round, arrears, advance payments, and current-round coverage.
    /// </summary>
    public static SlotFinancialMetrics CalculateSlotMetrics(ChituSlot slot, decimal monthlyInstalmentAmount, int durationMonths, int currentRoundIndex)
    {
        var paidInstalments = slot.Instalments.Where(i => i.Status == InstalmentStatus.Paid).ToList();
        var totalPaidMonths = paidInstalments.Count;
        var totalConfirmedContributions = totalPaidMonths * monthlyInstalmentAmount;

        // Paid-through round: maximum continuous month index from 1 that is fully paid
        int paidThroughRound = 0;
        for (int m = 1; m <= durationMonths; m++)
        {
            if (paidInstalments.Any(i => i.MonthIndex == m))
            {
                paidThroughRound = m;
            }
            else
            {
                break;
            }
        }

        // Arrears: instalments where month <= currentRoundIndex that are NOT paid
        // Winning or closed slots owe no further arrears
        var arrearsMonths = 0;
        if (slot.Status == ChituSlotStatus.Active)
        {
            for (int m = 1; m <= currentRoundIndex; m++)
            {
                if (!paidInstalments.Any(i => i.MonthIndex == m))
                {
                    arrearsMonths++;
                }
            }
        }
        var arrearsAmount = arrearsMonths * monthlyInstalmentAmount;

        // Advance paid months: paid instalments where month > currentRoundIndex
        var advancePaidMonths = paidInstalments.Count(i => i.MonthIndex > currentRoundIndex);
        var advancePaidAmount = advancePaidMonths * monthlyInstalmentAmount;

        var isCurrentRoundPaid = paidInstalments.Any(i => i.MonthIndex == currentRoundIndex);

        var (isRaffleEligible, raffleReason) = EvaluateRaffleEligibility(slot, currentRoundIndex);

        // A winning slot or closed slot no longer incurs monthly commitments
        var isStillPayable = slot.Status == ChituSlotStatus.Active && slot.Instalments.Any(i => i.Status != InstalmentStatus.Paid);
        var normalMonthlyCommitment = (slot.Status == ChituSlotStatus.Active && slot.Status != ChituSlotStatus.Won && paidThroughRound < durationMonths)
            ? monthlyInstalmentAmount
            : 0m;

        return new SlotFinancialMetrics
        {
            TotalPaidMonths = totalPaidMonths,
            PaidThroughRound = paidThroughRound,
            TotalConfirmedContributions = totalConfirmedContributions,
            ArrearsMonths = arrearsMonths,
            ArrearsAmount = arrearsAmount,
            AdvancePaidMonths = advancePaidMonths,
            AdvancePaidAmount = advancePaidAmount,
            IsCurrentRoundPaid = isCurrentRoundPaid,
            IsStillPayable = isStillPayable,
            NormalMonthlyCommitment = normalMonthlyCommitment,
            IsRaffleEligible = isRaffleEligible,
            RaffleEligibilityReason = raffleReason
        };
    }
}

public class SlotFinancialMetrics
{
    public int TotalPaidMonths { get; set; }
    public int PaidThroughRound { get; set; }
    public decimal TotalConfirmedContributions { get; set; }
    public int ArrearsMonths { get; set; }
    public decimal ArrearsAmount { get; set; }
    public int AdvancePaidMonths { get; set; }
    public decimal AdvancePaidAmount { get; set; }
    public bool IsCurrentRoundPaid { get; set; }
    public bool IsStillPayable { get; set; }
    public decimal NormalMonthlyCommitment { get; set; }
    public bool IsRaffleEligible { get; set; }
    public string RaffleEligibilityReason { get; set; } = string.Empty;
}
