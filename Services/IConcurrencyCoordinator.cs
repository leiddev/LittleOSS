using LittleOSS.Models;

namespace LittleOSS.Services;

/// <summary>
/// 并发协调器接口，协调文件上传和删除操作中的区域锁、配额检查和事务处理
/// </summary>
public interface IConcurrencyCoordinator
{
    /// <summary>
    /// 上传文件，协调配额检查、区域锁和文件存储
    /// </summary>
    /// <param name="region">目标区域</param>
    /// <param name="stream">文件数据流</param>
    /// <param name="originalFileName">原始文件名</param>
    /// <param name="contentType">文件 MIME 类型</param>
    /// <param name="accessKeyId">上传者的 AccessKey ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>上传结果，包含文件 ID 或错误信息</returns>
    Task<UploadResult> UploadAsync(
        string region,
        Stream stream,
        string originalFileName,
        string contentType,
        string accessKeyId,
        CancellationToken ct = default);

    /// <summary>
    /// 删除文件，协调区域锁、物理文件删除和元数据软删除
    /// </summary>
    /// <param name="region">文件所属区域</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>删除操作的结果</returns>
    Task<DeleteResult> DeleteAsync(string region, string fileId, CancellationToken ct = default);
}

/// <summary>
/// 上传操作的结果记录
/// </summary>
/// <param name="Success">上传是否成功</param>
/// <param name="FileId">上传成功时返回的文件唯一标识</param>
/// <param name="Metadata">上传成功时返回的文件元数据</param>
/// <param name="ErrorCode">上传失败时的错误码</param>
/// <param name="ErrorMessage">上传失败时的错误信息</param>
public record UploadResult(
    bool Success,
    string? FileId = null,
    FileMetadata? Metadata = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// 删除操作的结果记录
/// </summary>
public record DeleteResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
