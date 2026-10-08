using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.Common;

public class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// SMS Provider type. e.g. "TextLk" or "Mock"
    /// </summary>
    public string Provider { get; set; } = "TextLk";

    /// <summary>
    /// Text.lk API endpoint URL. Defaults to https://app.text.lk/api/v3/sms/send
    /// </summary>
    [Required]
    public string ApiUrl { get; set; } = "https://app.text.lk/api/v3/sms/send";

    /// <summary>
    /// Text.lk API Bearer Token. Read from configuration, User Secrets, or environment variable.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Sender ID registered with Text.lk. Defaults to TextLKDemo.
    /// </summary>
    [Required]
    public string SenderId { get; set; } = "TextLKDemo";
}
