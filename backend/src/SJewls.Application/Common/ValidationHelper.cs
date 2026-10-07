using System.Net.Mail;
using System.Text.RegularExpressions;
using SJewls.Domain.Enums;

namespace SJewls.Application.Common;

public static class ValidationHelper
{
    // Sri Lanka phone regex or generic E.164
    private static readonly Regex SriLankaPhoneRegex = new(@"^(?:\+94|0)?(7[0-9]{8})$", RegexOptions.Compiled);
    
    // Sri Lanka NIC: 9 digits + V/X (Old) OR 12 digits (New)
    private static readonly Regex NicOldRegex = new(@"^[0-9]{9}[vVxX]$", RegexOptions.Compiled);
    private static readonly Regex NicNewRegex = new(@"^[0-9]{12}$", RegexOptions.Compiled);

    public static bool TryNormalizeContact(string input, out string normalized, out ContactType type)
    {
        normalized = string.Empty;
        type = ContactType.Phone;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();

        // Email check
        if (trimmed.Contains('@'))
        {
            if (IsValidEmail(trimmed))
            {
                normalized = trimmed.ToLowerInvariant();
                type = ContactType.Email;
                return true;
            }
            return false;
        }

        // Phone check
        if (TryNormalizePhone(trimmed, out var phone))
        {
            normalized = phone;
            type = ContactType.Phone;
            return true;
        }

        return false;
    }

    public static bool TryNormalizePhone(string phone, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        // Strip spaces, dashes, parentheses
        var clean = Regex.Replace(phone.Trim(), @"[\s\-\(\)]", "");

        var match = SriLankaPhoneRegex.Match(clean);
        if (match.Success)
        {
            // Normalize to +947XXXXXXXX
            normalized = "+94" + match.Groups[1].Value;
            return true;
        }

        // Standard international E.164 check: + followed by 7-15 digits
        if (Regex.IsMatch(clean, @"^\+[1-9]\d{6,14}$"))
        {
            normalized = clean;
            return true;
        }

        return false;
    }

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new MailAddress(email.Trim());
            return addr.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryNormalizeNic(string nic, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(nic))
            return false;

        var clean = nic.Trim().ToUpperInvariant();

        if (NicOldRegex.IsMatch(clean) || NicNewRegex.IsMatch(clean))
        {
            normalized = clean;
            return true;
        }

        return false;
    }

    public static bool IsValidDateOfBirth(DateOnly dob, int minAgeYears = 18, int maxAgeYears = 120)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob >= today)
            return false;

        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
            age--;

        return age >= minAgeYears && age <= maxAgeYears;
    }
}
