using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.Common;

public class EmailOptions
{
    public const string SectionName = "Email";

    [Required(ErrorMessage = "Email:Host is required.")]
    public string Host { get; set; } = "smtp.gmail.com";

    [Range(1, 65535, ErrorMessage = "Email:Port must be a valid port number between 1 and 65535.")]
    public int Port { get; set; } = 587;

    [Required(ErrorMessage = "Email:Username is required.")]
    public string Username { get; set; } = "w.sagee5@gmail.com";

    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email:FromAddress is required.")]
    [EmailAddress(ErrorMessage = "Email:FromAddress must be a valid email address.")]
    public string FromAddress { get; set; } = "w.sagee5@gmail.com";

    [Required(ErrorMessage = "Email:FromName is required.")]
    public string FromName { get; set; } = "SJewls";

    /// <summary>
    /// Authentication mechanism: "AppPassword" (default SMTP auth) or "OAuth2" (Google OAuth2 / Gmail API XOAUTH2).
    /// </summary>
    public string AuthMechanism { get; set; } = "AppPassword";

    /// <summary>
    /// Google OAuth2 Client ID (required when AuthMechanism == "OAuth2").
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Google OAuth2 Client Secret (required when AuthMechanism == "OAuth2").
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Google OAuth2 Refresh Token (required when AuthMechanism == "OAuth2").
    /// Must belong to the configured sender (w.sagee5@gmail.com / FromAddress).
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Helper to determine whether OAuth2 authentication is configured or requested.
    /// </summary>
    public bool IsOAuth2 =>
        string.Equals(AuthMechanism, "OAuth2", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(RefreshToken) && !string.IsNullOrWhiteSpace(ClientId));
}
