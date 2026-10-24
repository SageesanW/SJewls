namespace SJewls.Application.Common;

public class ChituPaymentOptions
{
    public const string SectionName = "ChituPayments";

    public string Provider { get; set; } = "None";

    public bool EnableSimulation { get; set; } = false;
}
