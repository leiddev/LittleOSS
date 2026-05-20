using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LittleOSS.Authentication;
using LittleOSS.Models.Responses;
using LittleOSS.Services;

namespace LittleOSS.Controllers;

/// <summary>
/// 文件管理控制器，提供文件上传、下载、删除、列表查询及配额查询功能
/// </summary>
[ApiController]
[Route("api/{region}/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IConcurrencyCoordinator _coordinator;
    private readonly IMetadataService _metadata;
    private readonly IQuotaService _quota;
    private readonly IFileStorageService _fileStorage;
    private readonly IOssConfigService _configService;
    private readonly ILogger<FilesController> _logger;

    /// <summary>
    /// 初始化控制器，注入所需的服务依赖
    /// </summary>
    public FilesController(
        IConcurrencyCoordinator coordinator,
        IMetadataService metadata,
        IQuotaService quota,
        IFileStorageService fileStorage,
        IOssConfigService configService,
        ILogger<FilesController> logger)
    {
        _coordinator = coordinator;
        _metadata = metadata;
        _quota = quota;
        _fileStorage = fileStorage;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件到指定区域
    /// </summary>
    /// <param name="region">目标区域名称</param>
    /// <param name="file">待上传的文件（multipart/form-data）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功返回 201 Created，失败返回相应错误状态码</returns>
    [HttpPut]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> Upload(string region, IFormFile file, CancellationToken ct)
    {
        // 验证区域参数
        if (string.IsNullOrWhiteSpace(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = "Region is required" });
        }

        // 验证区域是否已配置
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        // 验证用户是否有权访问该区域
        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        // 验证文件是否存在
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_FILE", Message = "File is required" });
        }

        // 验证文件大小是否超过限制
        if (file.Length > _configService.MaxFileSizeBytes)
        {
            return StatusCode(413, new ErrorResponse
            {
                Error = "FILE_TOO_LARGE",
                Message = $"File size exceeds maximum allowed size of {_configService.MaxFileSizeBytes} bytes"
            });
        }

        var accessKeyId = User.GetAccessKeyId() ?? "unknown";

        // 通过协调器执行上传（包含配额检查和区域锁）
        await using var stream = file.OpenReadStream();
        var result = await _coordinator.UploadAsync(
            region,
            stream,
            file.FileName,
            file.ContentType,
            accessKeyId,
            ct);

        // 处理上传失败的情况
        if (!result.Success)
        {
            var statusCode = result.ErrorCode switch
            {
                "QUOTA_EXCEEDED" => 413,
                "FILE_TOO_LARGE" => 413,
                _ => 500
            };

            return StatusCode(statusCode, new ErrorResponse
            {
                Error = result.ErrorCode ?? "UPLOAD_FAILED",
                Message = result.ErrorMessage
            });
        }

        // 构建成功响应
        var response = new FileInfoResponse
        {
            FileId = result.FileId!,
            OriginalFileName = result.Metadata!.OriginalFileName,
            Region = result.Metadata.Region,
            FileSizeBytes = result.Metadata.FileSizeBytes,
            ContentType = result.Metadata.ContentType,
            UploadTime = result.Metadata.UploadTime
        };

        return CreatedAtAction(nameof(Download), new { region, fileId = result.FileId }, response);
    }

    /// <summary>
    /// 列出指定区域的文件列表
    /// </summary>
    /// <param name="region">区域名称</param>
    /// <param name="skip">跳过的记录数（分页）</param>
    /// <param name="take">返回的记录数（最大 1000）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>文件列表及总数</returns>
    [HttpGet]
    public async Task<IActionResult> List(string region, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        // 验证区域
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        // 验证权限
        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        // 限制单次查询最大数量为 1000
        take = Math.Min(take, 1000);
        var files = await _metadata.GetRegionFilesAsync(region, skip, take, ct);
        var count = await _metadata.GetRegionFileCountAsync(region, ct);

        var response = files.Select(f => new FileInfoResponse
        {
            FileId = f.FileId,
            OriginalFileName = f.OriginalFileName,
            Region = f.Region,
            FileSizeBytes = f.FileSizeBytes,
            ContentType = f.ContentType,
            UploadTime = f.UploadTime
        }).ToList();

        return Ok(new { total = count, files = response });
    }

    /// <summary>
    /// 下载指定区域中的文件
    /// </summary>
    /// <param name="region">区域名称</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>文件流，支持断点续传</returns>
    [HttpGet("{fileId}")]
    public async Task<IActionResult> Download(string region, string fileId, CancellationToken ct)
    {
        // 验证区域
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        // 验证权限
        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        // 查询文件元数据
        var metadata = await _metadata.GetByFileIdAsync(fileId, ct);
        if (metadata == null)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found" });
        }

        // 确保文件属于请求的区域
        if (metadata.Region != region)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found in region '{region}'" });
        }

        try
        {
            // 获取文件流并返回
            var stream = await _fileStorage.GetFileAsync(region, fileId, ct);
            return File(stream, metadata.ContentType, metadata.OriginalFileName, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' physical file not found" });
        }
    }

    /// <summary>
    /// 删除指定区域中的文件
    /// </summary>
    /// <param name="region">区域名称</param>
    /// <param name="fileId">文件唯一标识</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功返回 204 No Content，失败返回 500</returns>
    [HttpDelete("{fileId}")]
    public async Task<IActionResult> Delete(string region, string fileId, CancellationToken ct)
    {
        // 验证区域
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        // 验证权限
        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        // 查询文件元数据
        var metadata = await _metadata.GetByFileIdAsync(fileId, ct);
        if (metadata == null)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found" });
        }

        // 确保文件属于请求的区域
        if (metadata.Region != region)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found in region '{region}'" });
        }

        try
        {
            // 通过协调器执行删除
            await _coordinator.DeleteAsync(region, fileId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
            return StatusCode(500, new ErrorResponse { Error = "DELETE_FAILED", Message = ex.Message });
        }
    }

    /// <summary>
    /// 查询指定区域的存储配额使用情况
    /// </summary>
    /// <param name="region">区域名称</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>配额信息，包括已用字节数、总配额及使用百分比</returns>
    [HttpGet("~/api/{region}/quota")]
    public async Task<IActionResult> GetQuota(string region, CancellationToken ct)
    {
        // 验证区域
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        // 验证权限
        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        var quotaInfo = await _quota.GetQuotaInfoAsync(region, ct);

        return Ok(new QuotaResponse
        {
            Region = quotaInfo.Region,
            UsedBytes = quotaInfo.UsedBytes,
            QuotaBytes = quotaInfo.QuotaBytes,
            UsedPercentage = quotaInfo.UsedPercentage
        });
    }
}
