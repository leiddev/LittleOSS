namespace LittleOSS.Services;

/// <summary>
/// OSS 配置服务接口，提供存储根路径、文件大小限制、区域配置和配额查询
/// </summary>
public interface IOssConfigService
{
    /// <summary>存储根目录的绝对路径</summary>
    string StorageRoot { get; }

    /// <summary>允许上传的最大文件大小（字节）</summary>
    long MaxFileSizeBytes { get; }

    /// <summary>检查指定的区域名称是否有效</summary>
    /// <param name="region">区域名称</param>
    /// <returns>区域有效返回 true，否则返回 false</returns>
    bool IsRegionValid(string region);

    /// <summary>获取指定区域的总配额（字节）</summary>
    /// <param name="region">区域名称</param>
    /// <returns>配额字节数，若区域无效则返回 0</returns>
    long GetRegionQuotaBytes(string region);

    /// <summary>获取所有已配置的区域名称列表</summary>
    /// <returns>区域名称只读列表</returns>
    IReadOnlyList<string> GetAllRegions();
}
