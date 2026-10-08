using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SJewls.Application.Common;
using SJewls.Application.Interfaces;

namespace SJewls.Infrastructure.Services;

public class TextLkSmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly SmsOptions _options;
    private readonly ILogger<TextLkSmsSender> _logger;

    public TextLkSmsSender(HttpClient httpClient, IOptions<SmsOptions> options, ILogger<TextLkSmsSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
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

        if (string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            _logger.LogError("SMS delivery failed: Sms:ApiToken is not configured.");
            throw new InvalidOperationException("Sms:ApiToken is not configured. Please configure your text.lk API token via User Secrets or environment variable.");
        }

        var recipient = FormatRecipientForTextLk(phoneNumber);

        var payload = new TextLkSendRequest
        {
            Recipient = recipient,
            SenderId = _options.SenderId,
            Type = "plain",
            Message = message
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = jsonContent
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP network error calling text.lk API for recipient {Recipient}.", MaskPhoneNumber(phoneNumber));
            throw new InvalidOperationException($"Network error communicating with SMS gateway: {ex.Message}", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Text.lk SMS gateway returned HTTP {StatusCode} for recipient {Recipient}. Response: {Response}",
                (int)response.StatusCode, MaskPhoneNumber(phoneNumber), responseBody);

            throw new InvalidOperationException($"SMS gateway rejected message (HTTP {(int)response.StatusCode}): {responseBody}");
        }

        // Parse Text.lk JSON response
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMsg = doc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                    _logger.LogError("Text.lk SMS gateway reported non-success status '{Status}' for recipient {Recipient}: {Message}",
                        status, MaskPhoneNumber(phoneNumber), errorMsg);

                    throw new InvalidOperationException($"SMS delivery failed from provider: {errorMsg}");
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse text.lk response JSON, but HTTP 200 was received: {Response}", responseBody);
        }

        _logger.LogInformation("Text.lk SMS gateway accepted message delivery for recipient {Recipient} with sender ID {SenderId}",
            MaskPhoneNumber(phoneNumber), _options.SenderId);
    }

    /// <summary>
    /// Normalizes phone number into text.lk format (e.g., 94759712375).
    /// </summary>
    private static string FormatRecipientForTextLk(string phoneNumber)
    {
        var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (cleaned.StartsWith("0") && cleaned.Length == 10)
        {
            cleaned = "94" + cleaned[1..];
        }
        else if (cleaned.Length == 9)
        {
            cleaned = "94" + cleaned;
        }

        return cleaned;
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
        {
            return "***";
        }

        return phone[..4] + "***" + phone[^3..];
    }

    private sealed class TextLkSendRequest
    {
        [JsonPropertyName("recipient")]
        public string Recipient { get; set; } = string.Empty;

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "plain";

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
