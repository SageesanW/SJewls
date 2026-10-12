using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SJewls.Application.Common;
using SJewls.Application.Interfaces;

namespace SJewls.Infrastructure.Services;

public class EmailOtpProvider : IEmailOtpProvider, IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailOtpProvider> _logger;
    private readonly HttpClient? _httpClient;

    public EmailOtpProvider(
        IOptions<EmailOptions> options,
        ILogger<EmailOtpProvider> logger,
        HttpClient? httpClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email address cannot be empty.", nameof(toEmail));
        }

        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new ArgumentException("Verification code cannot be empty.", nameof(otpCode));
        }

        ValidateEmailCredentials();

        var senderAddress = GetSenderAddress();
        var senderName = GetSenderName();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderAddress));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = "Your SJewls verification code";

        var bodyBuilder = new BodyBuilder
        {
            TextBody =
$@"Hello,

Your SJewls verification code is: {otpCode}

This code is valid for {expiryMinutes} minutes.

If you did not request this verification code, please ignore this email. Never share this code with anyone.

SJewls Jaffna",

            HtmlBody =
$@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Your SJewls verification code</title>
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f7f9fa; margin: 0; padding: 24px; color: #1a1a1a;"">
    <div style=""max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e1e4e8; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.05);"">
        <div style=""text-align: center; margin-bottom: 24px;"">
            <h1 style=""color: #b8860b; margin: 0; font-size: 24px; font-weight: 700; letter-spacing: 0.5px;"">SJewls</h1>
            <p style=""color: #666; margin: 4px 0 0 0; font-size: 13px;"">Jewellery & Chitu Savings Platform — Jaffna</p>
        </div>
        
        <p style=""font-size: 15px; margin: 0 0 16px 0;"">Hello,</p>
        <p style=""font-size: 15px; margin: 0 0 20px 0; color: #444;"">Please use the following verification code to sign in or complete your registration:</p>
        
        <div style=""background-color: #fcf8e3; border: 1px dashed #b8860b; border-radius: 8px; text-align: center; padding: 18px; margin: 24px 0;"">
            <span style=""font-size: 32px; font-weight: 700; letter-spacing: 6px; color: #1a1a1a; font-family: monospace;"">{otpCode}</span>
        </div>
        
        <p style=""font-size: 14px; color: #555; margin: 0 0 12px 0;"">
            This code is valid for <strong>{expiryMinutes} minutes</strong>.
        </p>
        
        <p style=""font-size: 13px; color: #777; margin: 24px 0 0 0; border-top: 1px solid #eee; padding-top: 16px;"">
            If you did not request this verification code, please ignore this email. Never share this code with anyone.
        </p>
    </div>
</body>
</html>"
        };

        message.Body = bodyBuilder.ToMessageBody();

        await SendMessageInternalAsync(toEmail, message, cancellationToken);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("Recipient email address cannot be empty.", nameof(toEmail));
        }

        ValidateEmailCredentials();

        var senderAddress = GetSenderAddress();
        var senderName = GetSenderName();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderAddress));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        await SendMessageInternalAsync(toEmail, message, cancellationToken);
    }

    private string GetSenderAddress()
    {
        return !string.IsNullOrWhiteSpace(_options.FromAddress)
            ? _options.FromAddress
            : (!string.IsNullOrWhiteSpace(_options.Username) ? _options.Username : "w.sagee5@gmail.com");
    }

    private string GetSenderName()
    {
        return !string.IsNullOrWhiteSpace(_options.FromName) ? _options.FromName : "SJewls";
    }

    private void ValidateEmailCredentials()
    {
        if (_options.IsGmailApi)
        {
            if (string.IsNullOrWhiteSpace(_options.EffectiveClientId) ||
                string.IsNullOrWhiteSpace(_options.EffectiveClientSecret) ||
                string.IsNullOrWhiteSpace(_options.EffectiveRefreshToken))
            {
                throw new InvalidOperationException(
                    "Email OAuth2 credentials are incomplete. Please configure Email:GmailClientId (or Email:ClientId), " +
                    "Email:GmailClientSecret (or Email:ClientSecret), and Email:GmailRefreshToken (or Email:RefreshToken) via User Secrets or environment variables.");
            }
        }
        else
        {
            var password = _options.Password?.Replace(" ", "").Trim();
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(_options.Host))
            {
                throw new InvalidOperationException(
                    "Email credentials are not configured. Please configure Email:Password (Gmail App Password) " +
                    "and Email:Host, or configure Email:Provider = GmailApi with Email:GmailRefreshToken, Email:GmailClientId, and Email:GmailClientSecret via User Secrets or environment variables.");
            }
        }
    }

    private async Task SendMessageInternalAsync(string toEmail, MimeMessage message, CancellationToken cancellationToken)
    {
        if (_options.IsGmailApi)
        {
            await SendViaGmailApiAsync(toEmail, message, cancellationToken);
            return;
        }

        await SendViaSmtpAsync(toEmail, message, cancellationToken);
    }

    private async Task SendViaGmailApiAsync(string toEmail, MimeMessage message, CancellationToken cancellationToken)
    {
        var senderEmail = GetSenderAddress();
        var http = _httpClient ?? new HttpClient();

        // 1. Refresh access token from Google OAuth2 token endpoint
        var accessToken = await GetOAuthAccessTokenAsync(http, senderEmail, cancellationToken);

        // 2. Validate token ownership / audience
        await VerifyOAuthTokenOwnerAsync(http, accessToken, senderEmail, cancellationToken);

        // 3. Serialize MimeMessage to RFC 822 format and Base64Url encode
        using var memoryStream = new MemoryStream();
        await message.WriteToAsync(memoryStream, cancellationToken);
        var rawBase64Url = Convert.ToBase64String(memoryStream.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // 4. Send via Gmail REST API: POST https://gmail.googleapis.com/gmail/v1/users/me/messages/send
        var sendUrl = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";
        using var sendRequest = new HttpRequestMessage(HttpMethod.Post, sendUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { raw = rawBase64Url }),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        sendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var sendResponse = await http.SendAsync(sendRequest, cancellationToken);
        var sendJson = await sendResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!sendResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Gmail API delivery failed for recipient {Recipient} from {Sender}. Status: {StatusCode}, Body: {Body}",
                MaskEmail(toEmail), MaskEmail(senderEmail), sendResponse.StatusCode, sendJson);
            throw new InvalidOperationException(
                $"Gmail API delivery failed for '{senderEmail}'. Status: {sendResponse.StatusCode}, Details: {sendJson}");
        }

        _logger.LogInformation("Gmail API accepted email delivery for recipient {Recipient} from {Sender}",
            MaskEmail(toEmail), MaskEmail(senderEmail));
    }

    private async Task SendViaSmtpAsync(string toEmail, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();

        try
        {
            // Preserve full certificate validation while tolerating macOS-specific revocation check timeouts
            client.CheckCertificateRevocation = false;
            client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain != null)
                {
                    return chain.ChainStatus.All(status =>
                        status.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                        status.Status == X509ChainStatusFlags.OfflineRevocation);
                }

                return false;
            };

            // Connect with STARTTLS
            await client.ConnectAsync(_options.Host ?? "smtp.gmail.com", _options.Port, SecureSocketOptions.StartTls, cancellationToken);

            var password = _options.Password?.Replace(" ", "").Trim() ?? string.Empty;
            var username = GetSenderAddress();
            await client.AuthenticateAsync(username, password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("SMTP accepted email delivery for recipient {Recipient} from {Sender} via {Host}:{Port}",
                MaskEmail(toEmail), MaskEmail(GetSenderAddress()), _options.Host, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP delivery failed for recipient {Recipient} from {Sender} via {Host}:{Port}. Error: {ErrorMessage}",
                MaskEmail(toEmail), MaskEmail(GetSenderAddress()), _options.Host, _options.Port, ex.Message);

            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, CancellationToken.None);
                }
                catch
                {
                    // Ignore disconnect error on failure
                }
            }

            throw;
        }
    }

    private async Task<string> GetOAuthAccessTokenAsync(HttpClient http, string senderEmail, CancellationToken cancellationToken)
    {
        var tokenEndpoint = "https://oauth2.googleapis.com/token";
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.EffectiveClientId!,
                ["client_secret"] = _options.EffectiveClientSecret!,
                ["refresh_token"] = _options.EffectiveRefreshToken!,
                ["grant_type"] = "refresh_token"
            })
        };

        var tokenResponse = await http.SendAsync(tokenRequest, cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to refresh Google OAuth2 access token for {Sender}. Status: {StatusCode}, Body: {Body}",
                MaskEmail(senderEmail), tokenResponse.StatusCode, tokenJson);
            throw new InvalidOperationException(
                $"Failed to refresh Google OAuth2 access token for '{senderEmail}'. Google responded with {tokenResponse.StatusCode}: {tokenJson}");
        }

        using var doc = JsonDocument.Parse(tokenJson);
        if (!doc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
        {
            throw new InvalidOperationException("Google OAuth2 token endpoint did not return an 'access_token'.");
        }

        var accessToken = accessTokenProp.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Retrieved Google OAuth2 access token is empty.");
        }

        return accessToken;
    }

    public async Task VerifyOAuthTokenOwnerAsync(HttpClient http, string accessToken, string expectedSenderEmail, CancellationToken cancellationToken = default)
    {
        string? authenticatedEmail = null;
        string? audience = null;

        // Try 1: Google tokeninfo endpoint (fast, validates client ID and token status without requiring extra scopes)
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var tokenInfoRequest = new HttpRequestMessage(HttpMethod.Get, $"https://oauth2.googleapis.com/tokeninfo?access_token={Uri.EscapeDataString(accessToken)}");
            var tokenInfoResponse = await http.SendAsync(tokenInfoRequest, cts.Token);
            if (tokenInfoResponse.IsSuccessStatusCode)
            {
                var tokenInfoJson = await tokenInfoResponse.Content.ReadAsStringAsync(cts.Token);
                using var tokenInfoDoc = JsonDocument.Parse(tokenInfoJson);
                if (tokenInfoDoc.RootElement.TryGetProperty("email", out var emailProp))
                {
                    authenticatedEmail = emailProp.GetString();
                }
                if (tokenInfoDoc.RootElement.TryGetProperty("aud", out var audProp))
                {
                    audience = audProp.GetString();
                }
                else if (tokenInfoDoc.RootElement.TryGetProperty("azp", out var azpProp))
                {
                    audience = azpProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Google tokeninfo lookup skipped or failed during token validation.");
        }

        // Try 2: Gmail API profile endpoint (used if token has gmail profile scope or in mock tests)
        if (string.IsNullOrWhiteSpace(authenticatedEmail))
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/profile");
                profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var profileResponse = await http.SendAsync(profileRequest, cts.Token);
                if (profileResponse.IsSuccessStatusCode)
                {
                    var profileJson = await profileResponse.Content.ReadAsStringAsync(cts.Token);
                    using var profileDoc = JsonDocument.Parse(profileJson);
                    if (profileDoc.RootElement.TryGetProperty("emailAddress", out var emailProp))
                    {
                        authenticatedEmail = emailProp.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Gmail profile lookup skipped or failed during token validation.");
            }
        }

        // Validate client / audience matches configured client ID
        if (!string.IsNullOrWhiteSpace(audience) && !string.IsNullOrWhiteSpace(_options.EffectiveClientId))
        {
            if (!string.Equals(audience, _options.EffectiveClientId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("OAuth token audience mismatch: Token was issued for client {Audience}, but configured client is {ConfiguredClient}",
                    audience, _options.EffectiveClientId);
                throw new InvalidOperationException($"OAuth token was issued for client '{audience}', but configured client is '{_options.EffectiveClientId}'.");
            }
        }

        // Ensure the token owner strictly matches the expected sender email if email claim was retrieved
               // Check the sender against the token owner when available.
        if (!string.IsNullOrWhiteSpace(authenticatedEmail))
        {
            if (!string.Equals(
                authenticatedEmail,
                expectedSenderEmail,
                StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "OAuth account mismatch: Token owner {TokenOwner}, configured sender {Sender}",
                    MaskEmail(authenticatedEmail),
                    MaskEmail(expectedSenderEmail));

                throw new InvalidOperationException(
                    "The OAuth account does not match Email:FromAddress. " +
                    "Generate a refresh token using the configured sender account.");
            }

            _logger.LogInformation(
                "Google OAuth sender ownership verified for {Sender}",
                MaskEmail(expectedSenderEmail));
        }
        else
        {
            _logger.LogInformation(
                "Token owner email unavailable; sender ownership was not verified for {Sender}",
                MaskEmail(expectedSenderEmail));
        }
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "***";
        }

        var parts = email.Split('@');
        var name = parts[0];
        var maskedName = name.Length <= 2
            ? "***"
            : $"{name[0]}***{name[^1]}";

        return $"{maskedName}@{parts[1]}";
    }
}