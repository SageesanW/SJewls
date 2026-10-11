using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SJewls.Application.DTOs;
using SJewls.Application.Exceptions;
using SJewls.Application.Interfaces;
using SJewls.Infrastructure.Data;

namespace SJewls.Infrastructure.Services;

public class GoldRateService : IGoldRateService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly SJewlsDbContext _db;
    private readonly ILogger<GoldRateService> _logger;

    private const string CacheKeyPrefix = "gold_rate_24k_lkr_";

    public GoldRateService(
        HttpClient httpClient,
        IConfiguration config,
        IMemoryCache cache,
        SJewlsDbContext db,
        ILogger<GoldRateService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    public async Task<ApplicableGoldRateDto> GetApplicable24kRateAsync(Guid branchId, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{branchId}";
        if (_cache.TryGetValue(cacheKey, out ApplicableGoldRateDto? cachedRate) && cachedRate != null)
        {
            return cachedRate;
        }

        var apiKey = _config["GoldApi:ApiKey"]?.Trim();
        var baseUrl = (_config["GoldApi:BaseUrl"] ?? "https://www.goldapi.io/api").TrimEnd('/');
        var cacheMinutes = int.TryParse(_config["GoldApi:CacheDurationMinutes"], out var cm) ? cm : 15;
        var maxStalenessMinutes = int.TryParse(_config["GoldApi:MaxStalenessMinutes"], out var msm) ? msm : 1440;
        var timeoutSeconds = int.TryParse(_config["GoldApi:TimeoutSeconds"], out var ts) ? ts : 10;

        ApplicableGoldRateDto? liveRate = null;

        // 1. Attempt live GoldAPI.io retrieval if an API key is configured
        if (!string.IsNullOrEmpty(apiKey))
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/XAU/LKR");
                request.Headers.Add("x-access-token", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    var payload = JsonSerializer.Deserialize<GoldApiPayload>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (payload != null)
                    {
                        // Strict currency, metal, and positive unit rate validations
                        var isLkr = string.Equals(payload.Currency, "LKR", StringComparison.OrdinalIgnoreCase);
                        var isGold = string.Equals(payload.Metal, "XAU", StringComparison.OrdinalIgnoreCase);
                        var hasValid24kGramRate = payload.PriceGram24k.HasValue && payload.PriceGram24k.Value > 0;

                        if (isLkr && isGold && hasValid24kGramRate)
                        {
                            var sourceTimestamp = payload.Timestamp > 0
                                ? DateTimeOffset.FromUnixTimeSeconds(payload.Timestamp)
                                : DateTimeOffset.UtcNow;

                            var ageMinutes = (DateTimeOffset.UtcNow - sourceTimestamp).TotalMinutes;
                            var isFresh = ageMinutes <= maxStalenessMinutes;

                            if (isFresh)
                            {
                                liveRate = new ApplicableGoldRateDto
                                {
                                    RatePerGram = Math.Round(payload.PriceGram24k!.Value, 4),
                                    Currency = "LKR",
                                    Source = "GoldAPI.io",
                                    SourceTimestampUtc = sourceTimestamp,
                                    RetrievedAtUtc = DateTimeOffset.UtcNow,
                                    IsFresh = true
                                };

                                _cache.Set(cacheKey, liveRate, TimeSpan.FromMinutes(cacheMinutes));
                                _logger.LogInformation("Fetched fresh 24K gold rate from GoldAPI.io: LKR {Rate}/g (timestamp {Ts:u})",
                                    liveRate.RatePerGram, liveRate.SourceTimestampUtc);

                                return liveRate;
                            }
                            else
                            {
                                _logger.LogWarning("GoldAPI.io rate timestamp is stale ({Age:F1} mins old). Proceeding to manual fallback.", ageMinutes);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("GoldAPI.io response validation failed: isLkr={IsLkr}, isGold={IsGold}, price_gram_24k={Price24k}",
                                isLkr, isGold, payload.PriceGram24k);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("GoldAPI.io returned HTTP {StatusCode}. Proceeding to branch manual rate fallback.", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve gold rate from GoldAPI.io. Proceeding to branch manual rate fallback.");
            }
        }
        else
        {
            _logger.LogDebug("GoldAPI.io API key is not configured. Falling back to audited branch manual rates.");
        }

        // 2. Fallback to audited branch manual rate recorded in database
        var manualRate = await _db.GoldRates
            .Where(r => r.BranchId == branchId && r.Karat == 24 && r.EffectiveFromUtc <= DateTimeOffset.UtcNow)
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        if (manualRate == null || manualRate.RatePerGram <= 0)
        {
            // If branch has no specific rate, check system-wide latest 24K rate
            manualRate = await _db.GoldRates
                .Where(r => r.Karat == 24 && r.EffectiveFromUtc <= DateTimeOffset.UtcNow)
                .OrderByDescending(r => r.EffectiveFromUtc)
                .FirstOrDefaultAsync(ct);
        }

        if (manualRate == null || manualRate.RatePerGram <= 0)
        {
            throw new GoldRateUnavailableException(
                "No valid 24K gold rate in LKR is currently available for this branch. " +
                "GoldAPI.io was unreachable or unconfigured, and no positive manual rate has been recorded.");
        }

        var manualAgeMinutes = (DateTimeOffset.UtcNow - manualRate.EffectiveFromUtc).TotalMinutes;
        if (manualAgeMinutes > maxStalenessMinutes)
        {
            throw new GoldRateUnavailableException(
                $"The available 24K gold rate for this branch is stale (effective from {manualRate.EffectiveFromUtc:u}, " +
                $"which is {manualAgeMinutes:F0} minutes old, exceeding maximum permitted staleness of {maxStalenessMinutes} minutes). " +
                "A fresh manual gold rate must be recorded before creating customer quotes.");
        }

        var result = new ApplicableGoldRateDto
        {
            RatePerGram = Math.Round(manualRate.RatePerGram, 4),
            Currency = "LKR",
            Source = "BranchManual",
            SourceTimestampUtc = manualRate.EffectiveFromUtc,
            RetrievedAtUtc = DateTimeOffset.UtcNow,
            IsFresh = true
        };

        // Cache manual rate for a shorter period (e.g. 5 minutes)
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Min(5, cacheMinutes)));
        _logger.LogInformation("Using audited manual 24K gold rate: LKR {Rate}/g (source {Source}, effective {Ts:u})",
            result.RatePerGram, result.Source, result.SourceTimestampUtc);

        return result;
    }

    private class GoldApiPayload
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("metal")]
        public string? Metal { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("price_gram_24k")]
        public decimal? PriceGram24k { get; set; }

        [JsonPropertyName("price_gram_22k")]
        public decimal? PriceGram22k { get; set; }
    }
}
