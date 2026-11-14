using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SJewls.Application.Common;
using SJewls.Application.Interfaces;

namespace SJewls.Infrastructure.Services;

public class SmsLenzSmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsLenzOptions _options;
    private readonly ILogger<SmsLenzSmsSender> _logger;

    public SmsLenzSmsSender(
        HttpClient httpClient,
        IOptions<SmsLenzOptions> options,
        ILogger<SmsLenzSmsSender> logger,
        IOptions<SmsOptions>? rootSmsOptions = null,
        IConfiguration? configuration = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Resolve options: Prefer dedicated SmsLenzOptions section, fallback to nested SmsOptions.SmsLenz if populated
        var resolved = options.Value ?? new SmsLenzOptions();
        if (string.IsNullOrWhiteSpace(resolved.UserId) && rootSmsOptions?.Value?.SmsLenz != null)
        {
            var nested = rootSmsOptions.Value.SmsLenz;
            resolved.ApiUrl = !string.IsNullOrWhiteSpace(nested.ApiUrl) ? nested.ApiUrl : resolved.ApiUrl;
            resolved.UserId = nested.UserId;
            resolved.ApiKey = nested.ApiKey;
            resolved.SenderId = !string.IsNullOrWhiteSpace(nested.SenderId) ? nested.SenderId : resolved.SenderId;
        }

        // Direct fallback to environment configuration if any key is missing
        if (configuration != null)
        {
            if (string.IsNullOrWhiteSpace(resolved.UserId))
            {
                resolved.UserId = configuration["Sms:SmsLenz:UserId"]
                    ?? configuration["Sms__SmsLenz__UserId"]
                    ?? configuration["SmsLenz:UserId"]
                    ?? configuration["SMSLENZ_USER_ID"]
                    ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(resolved.ApiKey))
            {
                resolved.ApiKey = configuration["Sms:SmsLenz:ApiKey"]
                    ?? configuration["Sms__SmsLenz__ApiKey"]
                    ?? configuration["SmsLenz:ApiKey"]
                    ?? configuration["SMSLENZ_API_KEY"]
                    ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(resolved.SenderId) || resolved.SenderId == "SMSlenzDEMO")
            {
                var customSender = configuration["Sms:SmsLenz:SenderId"]
                    ?? configuration["Sms__SmsLenz__SenderId"]
                    ?? configuration["SmsLenz:SenderId"]
                    ?? configuration["SMSLENZ_SENDER_ID"];
                if (!string.IsNullOrWhiteSpace(customSender))
                {
                    resolved.SenderId = customSender;
                }
            }
            if (string.IsNullOrWhiteSpace(resolved.ApiUrl))
            {
                resolved.ApiUrl = configuration["Sms:SmsLenz:ApiUrl"]
                    ?? configuration["Sms__SmsLenz__ApiUrl"]
                    ?? configuration["SmsLenz:ApiUrl"]
                    ?? "https://smslenz.lk/api/send-sms";
            }
        }

        _options = resolved;
    }

    public async Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("Recipient phone number cannot be empty.", nameof(phoneNumber));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("SMS message content cannot be empty.", nameof(message));
        }

        ValidateConfiguration();

        var normalizedPhone = FormatRecipientForSmsLenz(phoneNumber);

        var payload = new SmsLenzSendRequest
        {
            UserId = _options.UserId.Trim(),
            ApiKey = _options.ApiKey.Trim(),
            SenderId = _options.SenderId.Trim(),
            Contact = normalizedPhone,
            Message = message
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = jsonContent
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("SMSlenz gateway request timed out for recipient {Recipient}.", MaskPhoneNumber(phoneNumber));
            throw new TimeoutException("SMSlenz gateway request timed out.", ex);
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            _logger.LogError(ex, "HTTP network error calling SMSlenz API for recipient {Recipient}.", MaskPhoneNumber(phoneNumber));
            throw new InvalidOperationException($"Network error communicating with SMSlenz gateway: {ex.Message}", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("SMSlenz gateway rejected message for recipient {Recipient} with HTTP {StatusCode}.",
                MaskPhoneNumber(phoneNumber), (int)response.StatusCode);

            // Never include raw responseBody in exceptions or logs as it may echo message/OTP
            throw new InvalidOperationException($"SMSlenz gateway rejected request with HTTP status {(int)response.StatusCode}.");
        }

        // Parse SMSlenz JSON response: delivery is accepted only if response contains "success": true
        bool isSuccess = false;
        string? errorMessage = null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successProp))
            {
                if (successProp.ValueKind == JsonValueKind.True)
                {
                    isSuccess = true;
                }
                else if (successProp.ValueKind == JsonValueKind.String &&
                         string.Equals(successProp.GetString(), "true", StringComparison.OrdinalIgnoreCase))
                {
                    isSuccess = true;
                }
            }

            if (!isSuccess)
            {
                if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                {
                    errorMessage = msgProp.GetString();
                }
                else if (root.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
                {
                    errorMessage = errProp.GetString();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse SMSlenz response JSON for recipient {Recipient}.", MaskPhoneNumber(phoneNumber));
            throw new InvalidOperationException("Malformed response received from SMSlenz gateway.", ex);
        }

        if (!isSuccess)
        {
            var sanitizedError = SanitizeErrorMessage(errorMessage);
            _logger.LogError("SMSlenz reported delivery failure for recipient {Recipient}: {Error}",
                MaskPhoneNumber(phoneNumber), sanitizedError);

            throw new InvalidOperationException($"SMSlenz delivery was not accepted: {sanitizedError}");
        }

        // Never log the raw message content or raw response body (can contain the OTP)!
        _logger.LogInformation("SMSlenz gateway accepted message delivery for recipient {Recipient} with sender ID {SenderId}",
            MaskPhoneNumber(phoneNumber), _options.SenderId);
    }

    private void ValidateConfiguration()
    {
        var missingSettings = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.ApiUrl))
            missingSettings.Add("Sms:SmsLenz:ApiUrl");
        if (string.IsNullOrWhiteSpace(_options.UserId))
            missingSettings.Add("Sms:SmsLenz:UserId");
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            missingSettings.Add("Sms:SmsLenz:ApiKey");
        if (string.IsNullOrWhiteSpace(_options.SenderId))
            missingSettings.Add("Sms:SmsLenz:SenderId");

        if (missingSettings.Count > 0)
        {
            var missing = string.Join(", ", missingSettings);
            _logger.LogError("SMSlenz delivery aborted: Missing required configuration keys: {Missing}", missing);
            throw new InvalidOperationException($"SMSlenz configuration is incomplete. Missing settings: {missing}. Please configure the corresponding environment variables.");
        }
    }

    /// <summary>
    /// Normalizes phone number into E.164 +94 format (e.g. +94759712375).
    /// </summary>
    public static string FormatRecipientForSmsLenz(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        var clean = phoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (clean.StartsWith("+94") && clean.Length == 12)
        {
            return clean;
        }

        var digits = new string(clean.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0") && digits.Length == 10)
        {
            return "+94" + digits[1..];
        }
        if (digits.StartsWith("94") && digits.Length == 11)
        {
            return "+" + digits;
        }
        if (digits.Length == 9)
        {
            return "+94" + digits;
        }

        return clean.StartsWith("+") ? clean : "+" + digits;
    }

    private static string SanitizeErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Operation unsuccessful";

        // Redact any sequence of 4-8 digits to ensure OTP codes are never logged or leaked in exceptions
        return Regex.Replace(message, @"\b\d{4,8}\b", "[REDACTED]");
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
        {
            return "***";
        }

        return phone[..4] + "***" + phone[^3..];
    }

    private sealed class SmsLenzSendRequest
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; } = string.Empty;

        [JsonPropertyName("contact")]
        public string Contact { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}

