using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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

    public EmailOtpProvider(IOptions<EmailOptions> options, ILogger<EmailOtpProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
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

        var password = _options.Password?.Replace(" ", "").Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Email:Password is not configured. Please configure your Gmail App Password via User Secrets or environment variable.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
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

        var password = _options.Password?.Replace(" ", "").Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Email:Password is not configured. Please configure your Gmail App Password via User Secrets or environment variable.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toEmail, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        await SendMessageInternalAsync(toEmail, message, cancellationToken);
    }

    private async Task SendMessageInternalAsync(string toEmail, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();

        try
        {
            var password = _options.Password?.Replace(" ", "").Trim() ?? string.Empty;

            // Preserve full certificate validation (CA, expiration, hostname) while tolerating
            // macOS-specific network issues where online OCSP/CRL revocation checks time out or return unknown.
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
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_options.Username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("SMTP accepted email delivery for recipient {Recipient} via {Host}:{Port}",
                MaskEmail(toEmail), _options.Host, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP delivery failed for recipient {Recipient} via {Host}:{Port}. Error: {ErrorMessage}",
                MaskEmail(toEmail), _options.Host, _options.Port, ex.Message);

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

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "***";
        }

        var parts = email.Split('@');
        var name = parts[0];
        var maskedName = name.Length <= 2 ? name : $"{name[0]}***{name[^1]}";
        return $"{maskedName}@{parts[1]}";
    }
}
