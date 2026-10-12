using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.Common;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Email delivery transport/provider: "GmailApi" (default HTTPS Gmail API via OAuth2) or "Smtp" (SMTP with STARTTLS).
    /// </summary>
    public string Provider { get; set; } = "GmailApi";

    [Required(ErrorMessage = "Email:FromAddress is required.")]
    [EmailAddress(ErrorMessage = "Email:FromAddress must be a valid email address.")]
    public string FromAddress { get; set; } = "w.sagee5@gmail.com";

    [Required(ErrorMessage = "Email:FromName is required.")]
    public string FromName { get; set; } = "SJewls";

    // --- Gmail API Configuration (Railway Keys: Email:GmailClientId, Email:GmailClientSecret, Email:GmailRefreshToken) ---
    private string? _gmailClientId;
    public string? GmailClientId
    {
        get => _gmailClientId ?? _clientId;
        set => _gmailClientId = value;
    }

    private string? _gmailClientSecret;
    public string? GmailClientSecret
    {
        get => _gmailClientSecret ?? _clientSecret;
        set => _gmailClientSecret = value;
    }

    private string? _gmailRefreshToken;
    public string? GmailRefreshToken
    {
        get => _gmailRefreshToken ?? _refreshToken;
        set => _gmailRefreshToken = value;
    }

    // --- Backwards-compatible aliases (ClientId, ClientSecret, RefreshToken) ---
    private string? _clientId;
    public string? ClientId
    {
        get => _clientId ?? _gmailClientId;
        set => _clientId = value;
    }

    private string? _clientSecret;
    public string? ClientSecret
    {
        get => _clientSecret ?? _gmailClientSecret;
        set => _clientSecret = value;
    }

    private string? _refreshToken;
    public string? RefreshToken
    {
        get => _refreshToken ?? _gmailRefreshToken;
        set => _refreshToken = value;
    }

    public string? EffectiveClientId => !string.IsNullOrWhiteSpace(GmailClientId) ? GmailClientId : ClientId;
    public string? EffectiveClientSecret => !string.IsNullOrWhiteSpace(GmailClientSecret) ? GmailClientSecret : ClientSecret;
    public string? EffectiveRefreshToken => !string.IsNullOrWhiteSpace(GmailRefreshToken) ? GmailRefreshToken : RefreshToken;

    // --- SMTP Configuration (only used and required when Provider == "Smtp") ---
    public string? Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string? Username { get; set; } = "w.sagee5@gmail.com";
    public string? Password { get; set; }

    // --- Backwards-compatible AuthMechanism alias ---
    private string? _authMechanism;
    public string? AuthMechanism
    {
        get => _authMechanism ?? (IsGmailApi ? "OAuth2" : "AppPassword");
        set
        {
            _authMechanism = value;
            if (string.Equals(value, "OAuth2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "GmailApi", StringComparison.OrdinalIgnoreCase))
            {
                Provider = "GmailApi";
            }
            else if (string.Equals(value, "AppPassword", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(value, "Smtp", StringComparison.OrdinalIgnoreCase))
            {
                Provider = "Smtp";
            }
        }
    }

    /// <summary>
    /// Indicates whether the Gmail API HTTPS transport is active.
    /// </summary>
    public bool IsGmailApi =>
        string.Equals(Provider, "GmailApi", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Provider, "OAuth2", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(_authMechanism, "OAuth2", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(EffectiveRefreshToken) && !string.IsNullOrWhiteSpace(EffectiveClientId));

    public bool IsOAuth2 => IsGmailApi;
}
