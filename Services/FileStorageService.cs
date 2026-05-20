using System.Security;

namespace LittleOSS.Services;

/// <summary>
/// 本地文件存储服务，负责文件的保存、读取、删除等物理文件操作
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly IOssConfigService _configService;
    private readonly ILogger<FileStorageService> _logger;

    /// <inheritdoc />
    public FileStorageService(IOssConfigService configService, ILogger<FileStorageService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SaveFileAsync(string region, string fileId, Stream stream, CancellationToken ct = default)
    {
        // 验证区域和文件 ID 的合法性，并防止路径穿越攻击
        ValidateRegionAndFileId(region, fileId);

        var regionPath = GetRegionPath(region);
        var tempPath = Path.Combine(regionPath, $".tmp_{fileId}");
        var finalPath = Path.Combine(regionPath, fileId);

        // 确保区域目录存在
        var directory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 将数据流写入临时文件（使用 80KB 缓冲区）
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
            // 如果已存在同名文件则先删除，再将临时文件重命名为最终文件名（原子操作）
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
            // 清理临时文件，忽略清理失败
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* ignore cleanup errors */ }
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Stream> GetFileAsync(string region, string fileId, CancellationToken ct = default)
    {
        // 验证区域和文件 ID
        ValidateRegionAndFileId(region, fileId);

        var filePath = GetFilePath(region, fileId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        // 将文件读入内存流后返回，便于 ASP.NET Core 处理
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

    /// <inheritdoc />
    public Task DeleteFileAsync(string region, string fileId, CancellationToken ct = default)
    {
        // 验证区域和文件 ID
        ValidateRegionAndFileId(region, fileId);

        var filePath = GetFilePath(region, fileId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string region, string fileId, CancellationToken ct = default)
    {
        ValidateRegionAndFileId(region, fileId);

        var filePath = GetFilePath(region, fileId);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <summary>
    /// 获取指定区域的根目录路径
    /// </summary>
    private string GetRegionPath(string region)
    {
        var regionPath = Path.Combine(_configService.StorageRoot, region);
        return Path.GetFullPath(regionPath);
    }

    /// <summary>
    /// 获取指定文件的完整路径
    /// </summary>
    private string GetFilePath(string region, string fileId)
    {
        var regionPath = GetRegionPath(region);
        var filePath = Path.Combine(regionPath, fileId);
        return Path.GetFullPath(filePath);
    }

    /// <summary>
    /// 验证区域和文件 ID 的合法性，防止路径穿越等安全风险
    /// </summary>
    private void ValidateRegionAndFileId(string region, string fileId)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region cannot be empty", nameof(region));

        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("FileId cannot be empty", nameof(fileId));

        if (!_configService.IsRegionValid(region))
            throw new ArgumentException($"Invalid region: {region}", nameof(region));

        // 防止路径穿越攻击，确保最终路径在区域目录内
        var regionPath = GetRegionPath(region);
        var fullPath = Path.GetFullPath(Path.Combine(regionPath, fileId));

        if (!fullPath.StartsWith(regionPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Path traversal detected");
        }
    }
}
