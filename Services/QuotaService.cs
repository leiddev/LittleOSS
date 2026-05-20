using Microsoft.EntityFrameworkCore;
using LittleOSS.Data;

namespace LittleOSS.Services;

/// <summary>
/// 配额服务实现，负责查询区域存储使用量、判断上传是否超配额，以及提供配额信息聚合
/// </summary>
public class QuotaService : IQuotaService
{
    private readonly IDbContextFactory<OssDbContext> _contextFactory;
    private readonly IOssConfigService _configService;
    private readonly ILogger<QuotaService> _logger;

    /// <summary>构造函数</summary>
    /// <param name="contextFactory">数据库上下文工厂</param>
    /// <param name="configService">OSS 配置服务</param>
    /// <param name="logger">日志记录器</param>
    public QuotaService(
        IDbContextFactory<OssDbContext> contextFactory,
        IOssConfigService configService,
        ILogger<QuotaService> logger)
    {
        _contextFactory = contextFactory;
        _configService = configService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<long> GetRegionUsedBytesAsync(string region, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.FileMetadatas
            .Where(f => f.Region == region && !f.IsDeleted)
            .SumAsync(f => f.FileSizeBytes, ct);
    }

    /// <inheritdoc />
    public Task<long> GetRegionQuotaBytesAsync(string region, CancellationToken ct = default)
    {
        return Task.FromResult(_configService.GetRegionQuotaBytes(region));
    }

    /// <inheritdoc />
    public async Task<bool> CanUploadAsync(string region, long fileSize, CancellationToken ct = default)
    {
        var usedBytes = await GetRegionUsedBytesAsync(region, ct);
        var quotaBytes = await GetRegionQuotaBytesAsync(region, ct);

        return usedBytes + fileSize <= quotaBytes;
    }

    /// <inheritdoc />
    public async Task<QuotaInfo> GetQuotaInfoAsync(string region, CancellationToken ct = default)
    {
        var usedBytes = await GetRegionUsedBytesAsync(region, ct);
        var quotaBytes = await GetRegionQuotaBytesAsync(region, ct);

        return new QuotaInfo(region, usedBytes, quotaBytes);
    }
}
