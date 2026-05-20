namespace LittleOSS.Services;

/// <summary>
/// 文件存储服务接口，提供本地文件的物理存储操作
/// </summary>
public interface IFileStorageService
{
    /// <summary>保存文件到本地存储</summary>
    /// <param name="region">区域名称</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="stream">文件数据流</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>文件的完整存储路径</returns>
    Task<string> SaveFileAsync(string region, string fileId, Stream stream, CancellationToken ct = default);

    /// <summary>读取指定区域的物理文件</summary>
    /// <param name="region">区域名称</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>文件内容流（内存流）</returns>
    Task<Stream> GetFileAsync(string region, string fileId, CancellationToken ct = default);

    /// <summary>删除指定区域的物理文件</summary>
    /// <param name="region">区域名称</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    Task DeleteFileAsync(string region, string fileId, CancellationToken ct = default);

    /// <summary>检查物理文件是否存在</summary>
    /// <param name="region">区域名称</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>文件存在返回 true，否则返回 false</returns>
    Task<bool> FileExistsAsync(string region, string fileId, CancellationToken ct = default);
}
