using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.Common;

public class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>
    /// Explicitly enables development OTP mode. Defaults to false (real delivery is default).
    /// MUST NEVER be enabled in Production.
    /// </summary>
    public bool EnableDevMode { get; set; } = false;

    /// <summary>
    /// Fixed verification code for allowlisted contacts when DevMode is enabled.
    /// Must be configured in User Secrets or environment variables, never source control.
    /// </summary>
    public string? DevCode { get; set; }

    /// <summary>
    /// Allowlist of test contacts (phone numbers or emails) authorized to use DevMode.
    /// Matched after canonical contact normalization.
    /// </summary>
    public List<string> AllowedTestContacts { get; set; } = new();

    /// <summary>
    /// Determines whether a canonically normalized contact is present in AllowedTestContacts.
    /// Handles both list entries and comma-separated tokens, normalizing each entry before matching.
    /// </summary>
    public bool IsContactAllowlisted(string normalizedContact)
    {
        if (string.IsNullOrWhiteSpace(normalizedContact) || AllowedTestContacts == null || AllowedTestContacts.Count == 0)
        {
            return false;
        }

        foreach (var entry in AllowedTestContacts)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var tokens = entry.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (ValidationHelper.TryNormalizeContact(token, out var normalizedAllowed, out _))
                {
                    if (string.Equals(normalizedContact, normalizedAllowed, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                else if (string.Equals(normalizedContact, token.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
