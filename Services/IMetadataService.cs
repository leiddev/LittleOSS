using LittleOSS.Models;

namespace LittleOSS.Services;

/// <summary>
/// 元数据服务接口，管理文件的数据库元数据记录
/// </summary>
public interface IMetadataService
{
    /// <summary>根据文件 ID 获取元数据记录</summary>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>元数据记录，若不存在则返回 null</returns>
    Task<FileMetadata?> GetByFileIdAsync(string fileId, CancellationToken ct = default);

    /// <summary>分页获取指定区域的文件元数据列表</summary>
    /// <param name="region">区域名称</param>
    /// <param name="skip">跳过的记录数</param>
    /// <param name="take">返回的记录数上限</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>元数据记录只读列表</returns>
    Task<IReadOnlyList<FileMetadata>> GetRegionFilesAsync(string region, int skip = 0, int take = 100, CancellationToken ct = default);

    /// <summary>获取指定区域的文件总数量</summary>
    /// <param name="region">区域名称</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>文件数量</returns>
    Task<int> GetRegionFileCountAsync(string region, CancellationToken ct = default);

    /// <summary>创建新的元数据记录</summary>
    /// <param name="metadata">元数据对象</param>
    /// <param name="ct">取消令牌</param>
    Task CreateRecordAsync(FileMetadata metadata, CancellationToken ct = default);

    /// <summary>对指定文件执行软删除（标记 IsDeleted = true）</summary>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    Task SoftDeleteAsync(string fileId, CancellationToken ct = default);
}
