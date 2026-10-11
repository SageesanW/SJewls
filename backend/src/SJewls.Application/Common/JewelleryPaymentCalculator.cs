using SJewls.Domain.Enums;

namespace SJewls.Application.Common;

public class JewelleryCalculationResult
{
    public JewelleryQuoteInputMode InputMode { get; set; }
    public decimal EnteredAmount { get; set; }
    public decimal RequestedGrams { get; set; }
    public string Currency { get; set; } = "LKR";

    public decimal RatePerGram { get; set; }
    public decimal PremiumPercentage { get; set; }
    public DateTimeOffset RateAsOf { get; set; }

    public decimal BaseGoldGrams { get; set; }
    public decimal PremiumGrams { get; set; }
    public decimal? ChargeableGoldGrams { get; set; } // Buy Gram only: calculation breakdown only; never credited

    public decimal TotalGoldCredit { get; set; } // Authoritative credited grams for balances, ledger, and target
    public decimal? BaseAmount { get; set; } // Buy Gram only
    public decimal? PremiumAmount { get; set; } // Buy Gram only
    public decimal AmountToPay { get; set; } // Payable money in LKR

    public decimal RemainingGramsAfterPayment { get; set; }
    public bool WillExceedTarget { get; set; }
}

public static class JewelleryPaymentCalculator
{
    /// <summary>
    /// Calculates Buy Gram breakdown according to authoritative business rules:
    /// BaseGoldGrams = G
    /// PremiumGrams = G * p
    /// ChargeableGoldGrams = G + PremiumGrams
    /// BaseAmount = G * R
    /// PremiumAmount = PremiumGrams * R
    /// AmountToPay = BaseAmount + PremiumAmount
    /// TotalGoldCredit = G
    /// </summary>
    public static JewelleryCalculationResult CalculateBuyGram(
        decimal requestedGrams,
        decimal ratePerGram,
        decimal premiumPercentage,
        decimal remainingTargetGrams,
        DateTimeOffset rateAsOf)
    {
        if (requestedGrams <= 0)
        {
            throw new ArgumentException("Requested gold grams must be greater than zero.", nameof(requestedGrams));
        }

        if (ratePerGram <= 0)
        {
            throw new ArgumentException("Rate per gram must be greater than zero.", nameof(ratePerGram));
        }

        if (premiumPercentage < 0 || premiumPercentage >= 100)
        {
            throw new ArgumentException("Premium percentage must be between 0% and 99.99%.", nameof(premiumPercentage));
        }

        var p = premiumPercentage / 100m;
        var r = ratePerGram;
        var g = requestedGrams;

        var baseGoldGrams = g;
        var premiumGrams = g * p;
        var chargeableGoldGrams = g + premiumGrams;

        var baseAmount = g * r;
        var premiumAmount = premiumGrams * r;
        var rawAmountToPay = baseAmount + premiumAmount;
        var amountToPay = Math.Round(rawAmountToPay, 2, MidpointRounding.AwayFromZero);

        var totalGoldCredit = g;
        var remainingAfter = remainingTargetGrams - totalGoldCredit;
        var willExceed = totalGoldCredit > remainingTargetGrams;

        return new JewelleryCalculationResult
        {
            InputMode = JewelleryQuoteInputMode.BuyGram,
            RequestedGrams = g,
            EnteredAmount = 0m,
            Currency = "LKR",
            RatePerGram = r,
            PremiumPercentage = premiumPercentage,
            RateAsOf = rateAsOf,
            BaseGoldGrams = baseGoldGrams,
            PremiumGrams = premiumGrams,
            ChargeableGoldGrams = chargeableGoldGrams,
            TotalGoldCredit = totalGoldCredit,
            BaseAmount = Math.Round(baseAmount, 2, MidpointRounding.AwayFromZero),
            PremiumAmount = Math.Round(premiumAmount, 2, MidpointRounding.AwayFromZero),
            AmountToPay = amountToPay,
            RemainingGramsAfterPayment = Math.Max(0m, remainingAfter),
            WillExceedTarget = willExceed
        };
    }

    /// <summary>
    /// Calculates Buy Amount breakdown according to authoritative business rules:
    /// BaseGoldGrams = A / R
    /// PremiumGrams = BaseGoldGrams * p
    /// TotalGoldCredit = BaseGoldGrams - PremiumGrams
    /// AmountToPay = A
    /// </summary>
    public static JewelleryCalculationResult CalculateBuyAmount(
        decimal enteredAmount,
        decimal ratePerGram,
        decimal premiumPercentage,
        decimal remainingTargetGrams,
        DateTimeOffset rateAsOf)
    {
        if (enteredAmount <= 0)
        {
            throw new ArgumentException("Entered amount must be greater than zero.", nameof(enteredAmount));
        }

        if (ratePerGram <= 0)
        {
            throw new ArgumentException("Rate per gram must be greater than zero.", nameof(ratePerGram));
        }

        if (premiumPercentage < 0 || premiumPercentage >= 100)
        {
            throw new ArgumentException("Premium percentage must be between 0% and 99.99%.", nameof(premiumPercentage));
        }

        var p = premiumPercentage / 100m;
        var r = ratePerGram;
        var a = enteredAmount;

        var baseGoldGrams = a / r;
        var premiumGrams = baseGoldGrams * p;
        var rawTotalGoldCredit = baseGoldGrams - premiumGrams;

        // Truncate/round down to 4 decimal places to strictly prevent accidental overcrediting
        var totalGoldCredit = Math.Round(rawTotalGoldCredit, 4, MidpointRounding.ToZero);
        var amountToPay = Math.Round(a, 2, MidpointRounding.AwayFromZero);

        var remainingAfter = remainingTargetGrams - totalGoldCredit;
        var willExceed = totalGoldCredit > remainingTargetGrams;

        return new JewelleryCalculationResult
        {
            InputMode = JewelleryQuoteInputMode.BuyAmount,
            RequestedGrams = 0m,
            EnteredAmount = a,
            Currency = "LKR",
            RatePerGram = r,
            PremiumPercentage = premiumPercentage,
            RateAsOf = rateAsOf,
            BaseGoldGrams = Math.Round(baseGoldGrams, 4, MidpointRounding.AwayFromZero),
            PremiumGrams = Math.Round(premiumGrams, 4, MidpointRounding.AwayFromZero),
            ChargeableGoldGrams = null,
            TotalGoldCredit = totalGoldCredit,
            BaseAmount = null,
            PremiumAmount = null,
            AmountToPay = amountToPay,
            RemainingGramsAfterPayment = Math.Max(0m, remainingAfter),
            WillExceedTarget = willExceed
        };
    }

    /// <summary>
    /// Calculates the exact maximum LKR amount needed to reach the exact remaining target grams in Buy Amount mode.
    /// A = (RemainingGrams * R) / (1 - p)
    /// </summary>
    public static decimal CalculateMaxAmountForRemainingGrams(
        decimal remainingTargetGrams,
        decimal ratePerGram,
        decimal premiumPercentage)
    {
        if (remainingTargetGrams <= 0 || ratePerGram <= 0)
        {
            return 0m;
        }

        var p = premiumPercentage / 100m;
        if (p >= 1m) return 0m;

        var amount = (remainingTargetGrams * ratePerGram) / (1m - p);
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
