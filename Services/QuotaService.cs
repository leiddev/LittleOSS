using Microsoft.EntityFrameworkCore;
using LittleOSS.Data;

namespace LittleOSS.Services;

public class QuotaService : IQuotaService
{
    private readonly IDbContextFactory<OssDbContext> _contextFactory;
    private readonly IOssConfigService _configService;
    private readonly ILogger<QuotaService> _logger;

    public QuotaService(
        IDbContextFactory<OssDbContext> contextFactory,
        IOssConfigService configService,
        ILogger<QuotaService> logger)
    {
        _contextFactory = contextFactory;
        _configService = configService;
        _logger = logger;
    }

    public async Task<long> GetRegionUsedBytesAsync(string region, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.FileMetadatas
            .Where(f => f.Region == region && !f.IsDeleted)
            .SumAsync(f => f.FileSizeBytes, ct);
    }

    public Task<long> GetRegionQuotaBytesAsync(string region, CancellationToken ct = default)
    {
        return Task.FromResult(_configService.GetRegionQuotaBytes(region));
    }

    public async Task<bool> CanUploadAsync(string region, long fileSize, CancellationToken ct = default)
    {
        var usedBytes = await GetRegionUsedBytesAsync(region, ct);
        var quotaBytes = await GetRegionQuotaBytesAsync(region, ct);

        return usedBytes + fileSize <= quotaBytes;
    }

    public async Task<QuotaInfo> GetQuotaInfoAsync(string region, CancellationToken ct = default)
    {
        var usedBytes = await GetRegionUsedBytesAsync(region, ct);
        var quotaBytes = await GetRegionQuotaBytesAsync(region, ct);

        return new QuotaInfo(region, usedBytes, quotaBytes);
    }
}
