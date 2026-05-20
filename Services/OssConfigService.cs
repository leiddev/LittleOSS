using Microsoft.Extensions.Options;
using LittleOSS.Options;

namespace LittleOSS.Services;

public class OssConfigService : IOssConfigService
{
    private readonly OssOptions _options;
    private readonly HashSet<string> _regions;
    private readonly Dictionary<string, long> _quotaCache;

    public OssConfigService(IOptions<OssOptions> options)
    {
        _options = options.Value;
        _regions = new HashSet<string>(_options.Regions, StringComparer.OrdinalIgnoreCase);
        _quotaCache = ParseQuotas(_options.MaxQuotaPerRegion);
    }

    public string StorageRoot => _options.StorageRoot;
    public long MaxFileSizeBytes => _options.MaxFileSizeBytes;

    public bool IsRegionValid(string region)
    {
        return _regions.Contains(region);
    }

    public long GetRegionQuotaBytes(string region)
    {
        if (_quotaCache.TryGetValue(region, out var quota))
            return quota;

        return _quotaCache.TryGetValue("*", out var defaultQuota) ? defaultQuota : long.MaxValue;
    }

    public IReadOnlyList<string> GetAllRegions()
    {
        return _regions.ToList().AsReadOnly();
    }

    private static Dictionary<string, long> ParseQuotas(Dictionary<string, string> quotas)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in quotas)
        {
            result[kvp.Key] = ParseSizeString(kvp.Value);
        }
        return result;
    }

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
