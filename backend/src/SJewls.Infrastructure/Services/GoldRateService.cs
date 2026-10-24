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
using SJewls.Domain.Entities;
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
    private const string LiveApiCacheKey = "gold_rate_live_api_lkr";

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

        var cacheMinutes = int.TryParse(_config["GoldApi:CacheDurationMinutes"], out var cm) ? cm : 15;
        var maxStalenessMinutes = int.TryParse(_config["GoldApi:MaxStalenessMinutes"], out var msm) ? msm : 1440;

        // 1. Check for Active Admin Manual Override (Store Policy Override takes precedence)
        var manualOverride = await _db.GoldRateOverrides
            .AsNoTracking()
            .Where(o => (o.BranchId == branchId || o.BranchId == null) && o.IsEnabled && o.Rate24k > 0)
            .OrderByDescending(o => o.BranchId != null) // Branch-specific overrides take priority over global overrides
            .ThenByDescending(o => o.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (manualOverride != null && manualOverride.IsEnabled && manualOverride.Rate24k > 0)
        {
            var overrideRate = new ApplicableGoldRateDto
            {
                RatePerGram = Math.Round(manualOverride.Rate24k, 4),
                Currency = "LKR",
                Source = "ManualOverride",
                SourceTimestampUtc = manualOverride.UpdatedAtUtc ?? manualOverride.CreatedAtUtc,
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                IsFresh = true
            };

            _cache.Set(cacheKey, overrideRate, TimeSpan.FromMinutes(cacheMinutes));
            _logger.LogInformation("Using active Admin Manual Override 24K gold rate: LKR {Rate}/g (branch {Branch}, updated {Ts:u})",
                overrideRate.RatePerGram, manualOverride.BranchId?.ToString() ?? "Global", overrideRate.SourceTimestampUtc);

            return overrideRate;
        }

        // 2. Attempt live GoldAPI.io market retrieval if override is not active
        var liveApi = await FetchLiveGoldApiRateAsync(ct);
        if (liveApi.IsAvailable && liveApi.Rate24k.HasValue && liveApi.Rate24k.Value > 0)
        {
            var liveRate = new ApplicableGoldRateDto
            {
                RatePerGram = Math.Round(liveApi.Rate24k.Value, 4),
                Currency = "LKR",
                Source = "GoldAPI.io",
                SourceTimestampUtc = liveApi.TimestampUtc ?? DateTimeOffset.UtcNow,
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                IsFresh = true
            };

            _cache.Set(cacheKey, liveRate, TimeSpan.FromMinutes(cacheMinutes));
            return liveRate;
        }

        // 3. Fallback to audited branch manual rate recorded in database
        var manualRate = await _db.GoldRates
            .AsNoTracking()
            .Where(r => r.BranchId == branchId && r.Karat == 24 && r.EffectiveFromUtc <= DateTimeOffset.UtcNow)
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        if (manualRate == null || manualRate.RatePerGram <= 0)
        {
            manualRate = await _db.GoldRates
                .AsNoTracking()
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

        var fallbackRate = new ApplicableGoldRateDto
        {
            RatePerGram = Math.Round(manualRate.RatePerGram, 4),
            Currency = "LKR",
            Source = "BranchManual",
            SourceTimestampUtc = manualRate.EffectiveFromUtc,
            RetrievedAtUtc = DateTimeOffset.UtcNow,
            IsFresh = true
        };

        _cache.Set(cacheKey, fallbackRate, TimeSpan.FromMinutes(Math.Min(5, cacheMinutes)));
        return fallbackRate;
    }

    public async Task<CustomerGoldRateResponse> GetCustomerRateAsync(Guid? branchId, CancellationToken ct = default)
    {
        var targetBranchId = branchId;
        string? branchName = null;

        if (targetBranchId.HasValue)
        {
            var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == targetBranchId.Value, ct);
            branchName = branch?.Name;
        }

        // 1. Check for active manual override
        var customerOverrideQuery = _db.GoldRateOverrides
            .AsNoTracking()
            .Where(o => o.IsEnabled && o.Rate24k > 0);

        if (targetBranchId.HasValue)
        {
            customerOverrideQuery = customerOverrideQuery.Where(o => o.BranchId == targetBranchId.Value || o.BranchId == null);
        }

        var manualOverride = await customerOverrideQuery
            .OrderByDescending(o => o.BranchId != null)
            .ThenByDescending(o => o.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (manualOverride != null && manualOverride.IsEnabled && manualOverride.Rate24k > 0)
        {
            return new CustomerGoldRateResponse
            {
                Rate24kPerGram = Math.Round(manualOverride.Rate24k, 2),
                Rate22kPerGram = Math.Round(manualOverride.Rate22k > 0 ? manualOverride.Rate22k : (manualOverride.Rate24k * 22m / 24m), 2),
                Currency = "LKR",
                Source = "ManualOverride",
                IsOverridden = true,
                EffectiveFromUtc = manualOverride.UpdatedAtUtc ?? manualOverride.CreatedAtUtc,
                BranchId = targetBranchId,
                BranchName = branchName
            };
        }

        // 2. Check live market API
        var liveApi = await FetchLiveGoldApiRateAsync(ct);
        if (liveApi.IsAvailable && liveApi.Rate24k.HasValue && liveApi.Rate24k.Value > 0)
        {
            var r24 = liveApi.Rate24k.Value;
            var r22 = liveApi.Rate22k.HasValue && liveApi.Rate22k.Value > 0 ? liveApi.Rate22k.Value : (r24 * 22m / 24m);
            return new CustomerGoldRateResponse
            {
                Rate24kPerGram = Math.Round(r24, 2),
                Rate22kPerGram = Math.Round(r22, 2),
                Currency = "LKR",
                Source = "GoldAPI.io",
                IsOverridden = false,
                EffectiveFromUtc = liveApi.TimestampUtc ?? DateTimeOffset.UtcNow,
                BranchId = targetBranchId,
                BranchName = branchName
            };
        }

        // 3. Fallback to latest database recorded rates
        var r24Db = await _db.GoldRates.AsNoTracking()
            .Where(r => (targetBranchId == null || r.BranchId == targetBranchId) && r.Karat == 24)
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        var r22Db = await _db.GoldRates.AsNoTracking()
            .Where(r => (targetBranchId == null || r.BranchId == targetBranchId) && r.Karat == 22)
            .OrderByDescending(r => r.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        var final24k = r24Db?.RatePerGram ?? 33500m;
        var final22k = r22Db?.RatePerGram ?? (final24k * 22m / 24m);

        return new CustomerGoldRateResponse
        {
            Rate24kPerGram = Math.Round(final24k, 2),
            Rate22kPerGram = Math.Round(final22k, 2),
            Currency = "LKR",
            Source = "BranchManual",
            IsOverridden = false,
            EffectiveFromUtc = r24Db?.EffectiveFromUtc ?? DateTimeOffset.UtcNow,
            BranchId = targetBranchId,
            BranchName = branchName
        };
    }

    public async Task<GoldRateOverviewDto> GetOverviewAsync(Guid? branchId, CancellationToken ct = default)
    {
        // 1. Live API Rate
        var liveApi = await FetchLiveGoldApiRateAsync(ct);

        // 2. Active Override for target branch (or global)
        var overrideQuery = _db.GoldRateOverrides
            .Include(o => o.Branch)
            .Include(o => o.UpdatedByStaff)
            .AsNoTracking();

        if (branchId.HasValue)
        {
            overrideQuery = overrideQuery.Where(o => o.BranchId == branchId.Value || o.BranchId == null);
        }
        else
        {
            overrideQuery = overrideQuery.Where(o => o.BranchId == null);
        }

        var manualOverride = await overrideQuery
            .OrderByDescending(o => o.BranchId != null)
            .ThenByDescending(o => o.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

        // 3. All Branches summary
        var branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        var allOverrides = await _db.GoldRateOverrides.AsNoTracking().ToListAsync(ct);

        var branchSummaries = new List<BranchGoldRateSummaryDto>();
        foreach (var b in branches)
        {
            var bOverride = allOverrides.FirstOrDefault(o => o.BranchId == b.Id);
            var globalOverride = allOverrides.FirstOrDefault(o => o.BranchId == null);

            var active = bOverride != null && bOverride.IsEnabled ? bOverride :
                         globalOverride != null && globalOverride.IsEnabled ? globalOverride : null;

            decimal b24k = active?.Rate24k ?? liveApi.Rate24k ?? 33500m;
            decimal b22k = active?.Rate22k ?? liveApi.Rate22k ?? (b24k * 22m / 24m);

            branchSummaries.Add(new BranchGoldRateSummaryDto
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.Code,
                City = b.City,
                Rate24k = Math.Round(b24k, 2),
                Rate22k = Math.Round(b22k, 2),
                HasCustomOverride = bOverride != null,
                IsOverrideEnabled = active != null && active.IsEnabled
            });
        }

        // Determine Effective Rate
        var effective = await GetCustomerRateAsync(branchId, ct);

        return new GoldRateOverviewDto
        {
            EffectiveRate = new EffectiveGoldRateDetailDto
            {
                Rate24k = effective.Rate24kPerGram,
                Rate22k = effective.Rate22kPerGram,
                Currency = effective.Currency,
                ActiveSource = effective.Source,
                IsOverridden = effective.IsOverridden,
                EffectiveFromUtc = effective.EffectiveFromUtc,
                BranchId = effective.BranchId,
                BranchName = effective.BranchName
            },
            LiveApiRate = liveApi,
            ManualOverride = manualOverride == null ? null : new ManualOverrideDetailDto
            {
                Id = manualOverride.Id,
                BranchId = manualOverride.BranchId,
                BranchName = manualOverride.Branch?.Name ?? "Global (All Branches)",
                Rate24k = manualOverride.Rate24k,
                Rate22k = manualOverride.Rate22k,
                IsEnabled = manualOverride.IsEnabled,
                Reason = manualOverride.Reason,
                UpdatedByStaffId = manualOverride.UpdatedByStaffId,
                UpdatedByStaffName = manualOverride.UpdatedByStaff?.FullName ?? "Staff Member",
                UpdatedAtUtc = manualOverride.UpdatedAtUtc ?? manualOverride.CreatedAtUtc
            },
            Branches = branchSummaries
        };
    }

    public async Task<GoldRateOverviewDto> SetOverrideAsync(SetGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default)
    {
        if (request.Rate24k <= 0)
        {
            throw new ArgumentException("24K gold rate must be greater than zero.");
        }

        var rate22k = request.Rate22k > 0 ? request.Rate22k : Math.Round(request.Rate24k * 22m / 24m, 4);

        // Find existing override record for this branch scope
        var existing = await _db.GoldRateOverrides
            .FirstOrDefaultAsync(o => o.BranchId == request.BranchId, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing != null)
        {
            existing.Rate24k = request.Rate24k;
            existing.Rate22k = rate22k;
            existing.IsEnabled = request.IsEnabled;
            existing.Reason = request.Reason;
            existing.UpdatedByStaffId = staffId;
            existing.UpdatedAtUtc = now;
        }
        else
        {
            existing = new GoldRateOverride
            {
                Id = Guid.NewGuid(),
                BranchId = request.BranchId,
                Rate24k = request.Rate24k,
                Rate22k = rate22k,
                IsEnabled = request.IsEnabled,
                Reason = request.Reason,
                UpdatedByStaffId = staffId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.GoldRateOverrides.Add(existing);
        }

        // Also record in historical GoldRates table for full audit continuity
        var targetBranches = new List<Guid>();
        if (request.BranchId.HasValue)
        {
            targetBranches.Add(request.BranchId.Value);
        }
        else
        {
            targetBranches = await _db.Branches.Select(b => b.Id).ToListAsync(ct);
        }

        foreach (var bId in targetBranches)
        {
            _db.GoldRates.Add(new GoldRate
            {
                BranchId = bId,
                Karat = 24,
                RatePerGram = request.Rate24k,
                EffectiveFromUtc = now,
                RecordedByStaffId = staffId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            _db.GoldRates.Add(new GoldRate
            {
                BranchId = bId,
                Karat = 22,
                RatePerGram = rate22k,
                EffectiveFromUtc = now,
                RecordedByStaffId = staffId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            // Evict in-memory cache for this branch
            _cache.Remove($"{CacheKeyPrefix}{bId}");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Staff {StaffId} set manual gold rate override: 24K={R24}, 22K={R22}, Enabled={Enabled}, Scope={Scope}",
            staffId, request.Rate24k, rate22k, request.IsEnabled, request.BranchId?.ToString() ?? "Global");

        return await GetOverviewAsync(request.BranchId, ct);
    }

    public async Task<GoldRateOverviewDto> ToggleOverrideAsync(ToggleGoldRateOverrideRequest request, Guid staffId, CancellationToken ct = default)
    {
        var existing = await _db.GoldRateOverrides
            .FirstOrDefaultAsync(o => o.BranchId == request.BranchId, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing == null)
        {
            existing = new GoldRateOverride
            {
                Id = Guid.NewGuid(),
                BranchId = request.BranchId,
                Rate24k = 33500m,
                Rate22k = 30700m,
                IsEnabled = request.IsEnabled,
                Reason = "Toggled via Gold Rate Control Panel",
                UpdatedByStaffId = staffId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _db.GoldRateOverrides.Add(existing);
        }
        else
        {
            existing.IsEnabled = request.IsEnabled;
            existing.UpdatedByStaffId = staffId;
            existing.UpdatedAtUtc = now;
        }

        // Invalidate cache
        if (request.BranchId.HasValue)
        {
            _cache.Remove($"{CacheKeyPrefix}{request.BranchId.Value}");
        }
        else
        {
            var branchIds = await _db.Branches.Select(b => b.Id).ToListAsync(ct);
            foreach (var bId in branchIds)
            {
                _cache.Remove($"{CacheKeyPrefix}{bId}");
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetOverviewAsync(request.BranchId, ct);
    }

    public async Task<List<GoldRateHistoryDto>> GetHistoryAsync(Guid? branchId, int limit = 50, CancellationToken ct = default)
    {
        var query = _db.GoldRates
            .Include(r => r.Branch)
            .Include(r => r.RecordedByStaff)
            .AsNoTracking();

        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        var list = await query
            .OrderByDescending(r => r.EffectiveFromUtc)
            .Take(limit)
            .Select(r => new GoldRateHistoryDto
            {
                Id = r.Id,
                BranchId = r.BranchId,
                BranchName = r.Branch != null ? r.Branch.Name : "Unknown",
                Karat = r.Karat,
                RatePerGram = Math.Round(r.RatePerGram, 2),
                Source = "BranchManual",
                EffectiveFromUtc = r.EffectiveFromUtc,
                RecordedByStaffId = r.RecordedByStaffId,
                RecordedByStaffName = r.RecordedByStaff != null ? r.RecordedByStaff.FullName : "System Admin",
                Remarks = $"Audited {r.Karat}K rate"
            })
            .ToListAsync(ct);

        return list;
    }

    private async Task<LiveApiGoldRateDetailDto> FetchLiveGoldApiRateAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(LiveApiCacheKey, out LiveApiGoldRateDetailDto? cached) && cached != null)
        {
            return cached;
        }

        var apiKey = _config["GoldApi:ApiKey"]?.Trim();
        var baseUrl = (_config["GoldApi:BaseUrl"] ?? "https://www.goldapi.io/api").TrimEnd('/');
        var timeoutSeconds = int.TryParse(_config["GoldApi:TimeoutSeconds"], out var ts) ? ts : 10;

        if (string.IsNullOrEmpty(apiKey))
        {
            return new LiveApiGoldRateDetailDto
            {
                IsConfigured = false,
                IsAvailable = false,
                StatusMessage = "GoldAPI.io API key is not configured in backend settings."
            };
        }

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

                if (payload != null && payload.PriceGram24k.HasValue && payload.PriceGram24k.Value > 0)
                {
                    var r24 = payload.PriceGram24k.Value;
                    var r22 = payload.PriceGram22k.HasValue && payload.PriceGram22k.Value > 0
                        ? payload.PriceGram22k.Value
                        : (r24 * 22m / 24m);

                    var timestamp = payload.Timestamp > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(payload.Timestamp)
                        : DateTimeOffset.UtcNow;

                    var liveDetail = new LiveApiGoldRateDetailDto
                    {
                        Rate24k = Math.Round(r24, 2),
                        Rate22k = Math.Round(r22, 2),
                        Currency = "LKR",
                        Provider = "GoldAPI.io",
                        IsConfigured = true,
                        IsAvailable = true,
                        TimestampUtc = timestamp,
                        StatusMessage = "Live feed active from GoldAPI.io"
                    };

                    _cache.Set(LiveApiCacheKey, liveDetail, TimeSpan.FromMinutes(15));
                    return liveDetail;
                }
            }

            return new LiveApiGoldRateDetailDto
            {
                IsConfigured = true,
                IsAvailable = false,
                StatusMessage = $"GoldAPI.io returned HTTP {response.StatusCode}."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to GoldAPI.io.");
            return new LiveApiGoldRateDetailDto
            {
                IsConfigured = true,
                IsAvailable = false,
                StatusMessage = $"Connection failed: {ex.Message}"
            };
        }
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
