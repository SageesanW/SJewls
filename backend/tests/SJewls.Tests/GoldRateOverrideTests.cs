using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SJewls.Application.DTOs;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;
using Xunit;

namespace SJewls.Tests;

public class GoldRateOverrideTests
{
    private static SJewlsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SJewlsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SJewlsDbContext(options);
    }

    [Fact]
    public async Task GetApplicable24kRateAsync_WhenManualOverrideActive_ReturnsOverrideRate()
    {
        using var db = CreateInMemoryDb();
        var branchId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        db.GoldRateOverrides.Add(new GoldRateOverride
        {
            Id = Guid.NewGuid(),
            BranchId = null, // Global
            Rate24k = 34500m,
            Rate22k = 31625m,
            IsEnabled = true,
            Reason = "Admin test override",
            UpdatedByStaffId = staffId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var service = new GoldRateService(httpClient, config, cache, db, NullLogger<GoldRateService>.Instance);

        var rate = await service.GetApplicable24kRateAsync(branchId);

        Assert.Equal(34500m, rate.RatePerGram);
        Assert.Equal("ManualOverride", rate.Source);
        Assert.True(rate.IsFresh);
    }

    [Fact]
    public async Task GetApplicable24kRateAsync_BranchSpecificOverride_TakesPrecedenceOverGlobal()
    {
        using var db = CreateInMemoryDb();
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        // Global override
        db.GoldRateOverrides.Add(new GoldRateOverride
        {
            Id = Guid.NewGuid(),
            BranchId = null,
            Rate24k = 33000m,
            Rate22k = 30250m,
            IsEnabled = true,
            UpdatedByStaffId = staffId
        });

        // Branch 1 specific override
        db.GoldRateOverrides.Add(new GoldRateOverride
        {
            Id = Guid.NewGuid(),
            BranchId = branch1,
            Rate24k = 35000m,
            Rate22k = 32083m,
            IsEnabled = true,
            UpdatedByStaffId = staffId
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var service = new GoldRateService(httpClient, config, cache, db, NullLogger<GoldRateService>.Instance);

        // Branch 1 gets branch-specific rate
        var rate1 = await service.GetApplicable24kRateAsync(branch1);
        Assert.Equal(35000m, rate1.RatePerGram);

        // Branch 2 falls back to global override
        var rate2 = await service.GetApplicable24kRateAsync(branch2);
        Assert.Equal(33000m, rate2.RatePerGram);
    }

    [Fact]
    public async Task SetOverrideAsync_UpdatesDatabaseAndReturnsFreshOverview()
    {
        using var db = CreateInMemoryDb();
        var staffId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        db.Branches.Add(new Branch
        {
            Id = branchId,
            Name = "Jaffna Branch",
            Code = "JAF-01",
            City = "Jaffna",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var service = new GoldRateService(httpClient, config, cache, db, NullLogger<GoldRateService>.Instance);

        var overview = await service.SetOverrideAsync(new SetGoldRateOverrideRequest
        {
            BranchId = branchId,
            Rate24k = 34000m,
            Rate22k = 31166m,
            IsEnabled = true,
            Reason = "Festival season adjustment"
        }, staffId);

        Assert.NotNull(overview.ManualOverride);
        Assert.Equal(34000m, overview.ManualOverride.Rate24k);
        Assert.True(overview.ManualOverride.IsEnabled);

        var applicable = await service.GetApplicable24kRateAsync(branchId);
        Assert.Equal(34000m, applicable.RatePerGram);
    }

    [Fact]
    public async Task GetCustomerRateAsync_ReturnsFormatted24kAnd22kRates()
    {
        using var db = CreateInMemoryDb();
        var staffId = Guid.NewGuid();

        db.GoldRateOverrides.Add(new GoldRateOverride
        {
            Id = Guid.NewGuid(),
            BranchId = null,
            Rate24k = 33500m,
            Rate22k = 30700m,
            IsEnabled = true,
            UpdatedByStaffId = staffId
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var httpClient = new HttpClient();
        var service = new GoldRateService(httpClient, config, cache, db, NullLogger<GoldRateService>.Instance);

        var customerRate = await service.GetCustomerRateAsync(null);

        Assert.Equal(33500m, customerRate.Rate24kPerGram);
        Assert.Equal(30700m, customerRate.Rate22kPerGram);
        Assert.Equal("LKR", customerRate.Currency);
        Assert.Equal("ManualOverride", customerRate.Source);
        Assert.True(customerRate.IsOverridden);
    }
}
