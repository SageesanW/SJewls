using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.Common;

public class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// SMS Provider type. e.g. "SmsLenz" (active), "TextLk" (legacy rollback), or "Mock"
    /// </summary>
    public string Provider { get; set; } = "SmsLenz";

    /// <summary>
    /// SMSlenz Gateway configuration.
    /// </summary>
    public SmsLenzOptions SmsLenz { get; set; } = new();

    // =========================================================================
    // Legacy Text.lk configuration (preserved for rollback)
    // To restore Text.lk, set Sms:Provider = "TextLk" (or Sms__Provider=TextLk)
    // =========================================================================
    public string ApiUrl { get; set; } = "https://app.text.lk/api/v3/sms/send";
    public string ApiToken { get; set; } = string.Empty;
    public string SenderId { get; set; } = "TextLKDemo";
}

public class SmsLenzOptions
{
    public const string SectionName = "Sms:SmsLenz";

    /// <summary>
    /// SMSlenz API endpoint URL. Defaults to https://smslenz.lk/api/send-sms
    /// </summary>
    public string ApiUrl { get; set; } = "https://smslenz.lk/api/send-sms";

    /// <summary>
    /// SMSlenz User ID. Configured via environment variable Sms__SmsLenz__UserId.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// SMSlenz API Key. Configured via environment variable Sms__SmsLenz__ApiKey.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// SMSlenz Sender ID. Defaults to SMSlenzDEMO.
    /// </summary>
    public string SenderId { get; set; } = "SMSlenzDEMO";
}
