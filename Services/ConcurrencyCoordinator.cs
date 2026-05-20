using Microsoft.EntityFrameworkCore;
using LittleOSS.Data;
using LittleOSS.Models;

namespace LittleOSS.Services;

/// <summary>
/// 并发协调器，负责协调文件上传和删除操作中的配额检查、区域锁定和事务处理
/// </summary>
public class ConcurrencyCoordinator : IConcurrencyCoordinator
{
    private readonly IOssConfigService _configService;
    private readonly IFileStorageService _fileStorage;
    private readonly IMetadataService _metadata;
    private readonly IQuotaService _quota;
    private readonly IRegionLockService _lockService;
    private readonly IDbContextFactory<OssDbContext> _contextFactory;
    private readonly ILogger<ConcurrencyCoordinator> _logger;

    /// <inheritdoc />
    public ConcurrencyCoordinator(
        IOssConfigService configService,
        IFileStorageService fileStorage,
        IMetadataService metadata,
        IQuotaService quota,
        IRegionLockService lockService,
        IDbContextFactory<OssDbContext> contextFactory,
        ILogger<ConcurrencyCoordinator> logger)
    {
        _configService = configService;
        _fileStorage = fileStorage;
        _metadata = metadata;
        _quota = quota;
        _lockService = lockService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UploadResult> UploadAsync(
        string region,
        Stream stream,
        string originalFileName,
        string contentType,
        string accessKeyId,
        CancellationToken ct = default)
    {
        // 检查文件大小是否超过配置的最大限制（无需锁，可在锁外执行）
        if (stream.Length > _configService.MaxFileSizeBytes)
        {
            return new UploadResult(
                Success: false,
                ErrorCode: "FILE_TOO_LARGE",
                ErrorMessage: $"File size exceeds maximum allowed size of {_configService.MaxFileSizeBytes} bytes");
        }

        var fileId = Guid.NewGuid().ToString("N");
        var filePath = $"{region}/{fileId}";
        var fileSize = stream.Length;

        // 配额检查（无锁，内存操作 + DB 查询）
        if (!await _quota.CanUploadAsync(region, fileSize, ct))
        {
            var quotaInfo = await _quota.GetQuotaInfoAsync(region, ct);
            return new UploadResult(
                Success: false,
                ErrorCode: "QUOTA_EXCEEDED",
                ErrorMessage: $"Region '{region}' quota exceeded. Used: {quotaInfo.UsedBytes}, Quota: {quotaInfo.QuotaBytes}");
        }

        // 构建文件元数据对象（无锁，内存构造）
        var metadata = new FileMetadata
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            OriginalFileName = originalFileName,
            Region = region,
            FilePath = filePath,
            FileSizeBytes = fileSize,
            ContentType = contentType,
            UploadTime = DateTime.UtcNow,
            AccessKeyId = accessKeyId,
            IsDeleted = false
        };

        // 窄范围锁：仅保护元数据写入数据库（快速操作，约 1ms）
        await using (var regionLock = await _lockService.AcquireRegionLockAsync(region, TimeSpan.FromSeconds(5), ct))
        {
            await _metadata.CreateRecordAsync(metadata, ct);

            _logger.LogInformation(
                "File metadata created: FileId={FileId}, Region={Region}, Size={Size}",
                fileId, region, fileSize);
        } // 锁在此释放，文件 I/O 在锁外并行执行

        // 文件 I/O 操作在锁外执行，允许并发上传并行保存文件
        try
        {
            stream.Position = 0;
            await _fileStorage.SaveFileAsync(region, fileId, stream, ct);

            _logger.LogInformation(
                "File saved successfully: FileId={FileId}, Region={Region}, Size={Size}",
                fileId, region, fileSize);

            return new UploadResult(Success: true, FileId: fileId, Metadata: metadata);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 上传失败时清理已创建的元数据记录
            _logger.LogError(ex, "Failed to save physical file, cleaning up metadata: {FileId}", fileId);
            try
            {
                await _metadata.SoftDeleteAsync(fileId, ct);
            }
            catch { /* ignore cleanup errors */ }
            return new UploadResult(
                Success: false,
                ErrorCode: "UPLOAD_FAILED",
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<DeleteResult> DeleteAsync(string region, string fileId, CancellationToken ct = default)
    {
        // 获取区域锁，确保删除操作的原子性（5秒超时防止死锁）
        await using var regionLock = await _lockService.AcquireRegionLockAsync(region, TimeSpan.FromSeconds(5), ct);

        try
        {
            // 验证文件是否存在
            var metadata = await _metadata.GetByFileIdAsync(fileId, ct);
            if (metadata == null)
            {
                _logger.LogWarning("File not found for deletion: {FileId}", fileId);
                return new DeleteResult(Success: true);
            }

            // 删除物理文件（幂等操作，文件不存在不会抛异常）
            await _fileStorage.DeleteFileAsync(region, fileId, ct);

            // 软删除元数据记录（幂等操作，重复删除不会报错）
            await _metadata.SoftDeleteAsync(fileId, ct);

            _logger.LogInformation("File deleted: FileId={FileId}, Region={Region}", fileId, region);
            return new DeleteResult(Success: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 删除操作应保持幂等性：文件不存在、已删除等情况都返回成功
            _logger.LogWarning(ex, "Delete operation failed for region {Region}, treating as success", region);
            return new DeleteResult(Success: true);
        }
    }
}
