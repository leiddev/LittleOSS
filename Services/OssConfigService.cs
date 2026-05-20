using Microsoft.Extensions.Options;
using LittleOSS.Options;

namespace LittleOSS.Services;

/// <summary>
/// OSS 配置服务实现，从配置选项中读取存储配置、区域列表及配额信息
/// </summary>
public class OssConfigService : IOssConfigService
{
    private readonly OssOptions _options;
    private readonly HashSet<string> _regions;
    private readonly Dictionary<string, long> _quotaCache;

    /// <summary>构造函数</summary>
    /// <param name="options">OSS 配置选项（从 appsettings.json 绑定）</param>
    public OssConfigService(IOptions<OssOptions> options)
    {
        _options = options.Value;
        _regions = new HashSet<string>(_options.Regions, StringComparer.OrdinalIgnoreCase);
        _quotaCache = ParseQuotas(_options.MaxQuotaPerRegion);
    }

    /// <inheritdoc />
    public string StorageRoot => _options.StorageRoot;

    /// <inheritdoc />
    public long MaxFileSizeBytes => _options.MaxFileSizeBytes;

    /// <inheritdoc />
    public bool IsRegionValid(string region)
    {
        return _regions.Contains(region);
    }

    /// <inheritdoc />
    public long GetRegionQuotaBytes(string region)
    {
        if (_quotaCache.TryGetValue(region, out var quota))
            return quota;

        return _quotaCache.TryGetValue("*", out var defaultQuota) ? defaultQuota : long.MaxValue;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllRegions()
    {
        return _regions.ToList().AsReadOnly();
    }

    /// <summary>
    /// 解析配额配置字典，将带单位的字符串（如 "10GB"）转换为字节数
    /// </summary>
    /// <param name="quotas">原始配额配置，键为区域名称，值为带单位的字符串</param>
    /// <returns>以字节为单位的配额字典</returns>
    private static Dictionary<string, long> ParseQuotas(Dictionary<string, string> quotas)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in quotas)
        {
            result[kvp.Key] = ParseSizeString(kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// 将带单位的大小字符串（如 "5MB"、"1GB"）转换为字节数
    /// </summary>
    /// <param name="sizeStr">大小字符串，支持 B、KB、MB、GB、TB 单位</param>
    /// <returns>字节数；解析失败或为空时返回 long.MaxValue</returns>
    private static long ParseSizeString(string sizeStr)
    {
        sizeStr = sizeStr.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sizeStr))
            return long.MaxValue;

        if (long.TryParse(sizeStr, out var bytes))
            return bytes;

        var unitMultipliers = new Dictionary<string, long>
        {
            ["B"] = 1,
            ["KB"] = 1024,
            ["MB"] = 1024 * 1024,
            ["GB"] = 1024 * 1024 * 1024,
            ["TB"] = 1024L * 1024 * 1024 * 1024
        };

        foreach (var unit in unitMultipliers)
        {
            if (sizeStr.EndsWith(unit.Key, StringComparison.Ordinal))
            {
                var numberPart = sizeStr[..^unit.Key.Length].Trim();
                if (double.TryParse(numberPart, out var number))
                    return (long)(number * unit.Value);
            }
        }

        return long.MaxValue;
    }
}
