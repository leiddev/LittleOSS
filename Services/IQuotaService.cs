namespace LittleOSS.Services;

/// <summary>
/// 配额服务接口，提供区域存储配额查询和上传前的配额检查功能
/// </summary>
public interface IQuotaService
{
    /// <summary>获取指定区域已使用的存储字节数</summary>
    Task<long> GetRegionUsedBytesAsync(string region, CancellationToken ct = default);

    /// <summary>获取指定区域的总配额字节数</summary>
    Task<long> GetRegionQuotaBytesAsync(string region, CancellationToken ct = default);

    /// <summary>判断指定文件大小是否可以在该区域上传（配额是否足够）</summary>
    Task<bool> CanUploadAsync(string region, long fileSize, CancellationToken ct = default);

    /// <summary>获取指定区域的完整配额信息</summary>
    Task<QuotaInfo> GetQuotaInfoAsync(string region, CancellationToken ct = default);
}

/// <summary>
/// 区域配额信息记录
/// </summary>
/// <param name="Region">区域名称</param>
/// <param name="UsedBytes">已使用的存储字节数</param>
/// <param name="QuotaBytes">总配额字节数</param>
public record QuotaInfo(string Region, long UsedBytes, long QuotaBytes)
{
    /// <summary>配额使用百分比（0-100），配额为 0 时返回 0</summary>
    public double UsedPercentage => QuotaBytes > 0 ? (double)UsedBytes / QuotaBytes * 100 : 0;
}
