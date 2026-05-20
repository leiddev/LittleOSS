namespace LittleOSS.Services;

public interface IOssConfigService
{
    string StorageRoot { get; }
    long MaxFileSizeBytes { get; }
    bool IsRegionValid(string region);
    long GetRegionQuotaBytes(string region);
    IReadOnlyList<string> GetAllRegions();
}
