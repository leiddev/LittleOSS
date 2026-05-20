using Microsoft.EntityFrameworkCore;
using LittleOSS.Data;
using LittleOSS.Models;

namespace LittleOSS.Services;

public class ConcurrencyCoordinator : IConcurrencyCoordinator
{
    private readonly IOssConfigService _configService;
    private readonly IFileStorageService _fileStorage;
    private readonly IMetadataService _metadata;
    private readonly IQuotaService _quota;
    private readonly IRegionLockService _lockService;
    private readonly IDbContextFactory<OssDbContext> _contextFactory;
    private readonly ILogger<ConcurrencyCoordinator> _logger;

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

    public async Task<UploadResult> UploadAsync(
        string region,
        Stream stream,
        string originalFileName,
        string contentType,
        string accessKeyId,
        CancellationToken ct = default)
    {
        if (stream.Length > _configService.MaxFileSizeBytes)
        {
            return new UploadResult(
                Success: false,
                ErrorCode: "FILE_TOO_LARGE",
                ErrorMessage: $"File size exceeds maximum allowed size of {_configService.MaxFileSizeBytes} bytes");
        }

        await using var regionLock = await _lockService.AcquireRegionLockAsync(region, ct);

        try
        {
            if (!await _quota.CanUploadAsync(region, stream.Length, ct))
            {
                var quotaInfo = await _quota.GetQuotaInfoAsync(region, ct);
                return new UploadResult(
                    Success: false,
                    ErrorCode: "QUOTA_EXCEEDED",
                    ErrorMessage: $"Region '{region}' quota exceeded. Used: {quotaInfo.UsedBytes}, Quota: {quotaInfo.QuotaBytes}");
            }

            var fileId = Guid.NewGuid().ToString("N");
            var filePath = $"{region}/{fileId}";

            var metadata = new FileMetadata
            {
                Id = Guid.NewGuid(),
                FileId = fileId,
                OriginalFileName = originalFileName,
                Region = region,
                FilePath = filePath,
                FileSizeBytes = stream.Length,
                ContentType = contentType,
                UploadTime = DateTime.UtcNow,
                AccessKeyId = accessKeyId,
                IsDeleted = false
            };

            try
            {
                stream.Position = 0;
                await _fileStorage.SaveFileAsync(region, fileId, stream, ct);

                await _metadata.CreateRecordAsync(metadata, ct);

                _logger.LogInformation(
                    "File uploaded successfully: FileId={FileId}, Region={Region}, Size={Size}",
                    fileId, region, metadata.FileSizeBytes);

                return new UploadResult(Success: true, FileId: fileId, Metadata: metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file: {FileId}", fileId);
                await _fileStorage.DeleteFileAsync(region, fileId, ct);
                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Upload failed for region {Region}", region);
            return new UploadResult(
                Success: false,
                ErrorCode: "UPLOAD_FAILED",
                ErrorMessage: ex.Message);
        }
    }

    public async Task DeleteAsync(string region, string fileId, CancellationToken ct = default)
    {
        await using var regionLock = await _lockService.AcquireRegionLockAsync(region, ct);

        try
        {
            var metadata = await _metadata.GetByFileIdAsync(fileId, ct);
            if (metadata == null)
            {
                _logger.LogWarning("File not found for deletion: {FileId}", fileId);
                return;
            }

            await _fileStorage.DeleteFileAsync(region, fileId, ct);
            await _metadata.SoftDeleteAsync(fileId, ct);

            _logger.LogInformation("File deleted: FileId={FileId}, Region={Region}", fileId, region);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
            throw;
        }
    }
}
