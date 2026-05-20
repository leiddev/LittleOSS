namespace LittleOSS.Services;

public interface IQuotaService
{
    Task<long> GetRegionUsedBytesAsync(string region, CancellationToken ct = default);
    Task<long> GetRegionQuotaBytesAsync(string region, CancellationToken ct = default);
    Task<bool> CanUploadAsync(string region, long fileSize, CancellationToken ct = default);
    Task<QuotaInfo> GetQuotaInfoAsync(string region, CancellationToken ct = default);
}

public record QuotaInfo(string Region, long UsedBytes, long QuotaBytes)
{
    public double UsedPercentage => QuotaBytes > 0 ? (double)UsedBytes / QuotaBytes * 100 : 0;
}
