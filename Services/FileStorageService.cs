using System.Security;

namespace LittleOSS.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IOssConfigService _configService;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IOssConfigService configService, ILogger<FileStorageService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(string region, string fileId, Stream stream, CancellationToken ct = default)
    {
        ValidateRegionAndFileId(region, fileId);

        var regionPath = GetRegionPath(region);
        var tempPath = Path.Combine(regionPath, $".tmp_{fileId}");
        var finalPath = Path.Combine(regionPath, fileId);

        var directory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var fileStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await stream.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
        }

        try
        {
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }

            File.Move(tempPath, finalPath, overwrite: true);

            _logger.LogInformation("File saved successfully: {FilePath}", finalPath);
            return finalPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to save file: {TempPath}", tempPath);
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* ignore cleanup errors */ }
            }
            throw;
        }
    }

    public async Task<Stream> GetFileAsync(string region, string fileId, CancellationToken ct = default)
    {
        ValidateRegionAndFileId(region, fileId);

        var filePath = GetFilePath(region, fileId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await fileStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public Task DeleteFileAsync(string region, string fileId, CancellationToken ct = default)
    {
        ValidateRegionAndFileId(region, fileId);

        var filePath = GetFilePath(region, fileId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string region, string fileId, CancellationToken ct = default)
    {
        ValidateRegionAndFileId(region, fileId);

        var filePath = GetFilePath(region, fileId);
        return Task.FromResult(File.Exists(filePath));
    }

    private string GetRegionPath(string region)
    {
        var regionPath = Path.Combine(_configService.StorageRoot, region);
        return Path.GetFullPath(regionPath);
    }

    private string GetFilePath(string region, string fileId)
    {
        var regionPath = GetRegionPath(region);
        var filePath = Path.Combine(regionPath, fileId);
        return Path.GetFullPath(filePath);
    }

    private void ValidateRegionAndFileId(string region, string fileId)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region cannot be empty", nameof(region));

        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("FileId cannot be empty", nameof(fileId));

        if (!_configService.IsRegionValid(region))
            throw new ArgumentException($"Invalid region: {region}", nameof(region));

        var regionPath = GetRegionPath(region);
        var fullPath = Path.GetFullPath(Path.Combine(regionPath, fileId));

        if (!fullPath.StartsWith(regionPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Path traversal detected");
        }
    }
}
