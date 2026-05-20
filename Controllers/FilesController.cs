using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LittleOSS.Authentication;
using LittleOSS.Models.Responses;
using LittleOSS.Services;

namespace LittleOSS.Controllers;

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

    [HttpPut]
    [RequestSizeLimit(104857600)]
    public async Task<IActionResult> Upload(string region, IFormFile file, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = "Region is required" });
        }

        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_FILE", Message = "File is required" });
        }

        if (file.Length > _configService.MaxFileSizeBytes)
        {
            return StatusCode(413, new ErrorResponse
            {
                Error = "FILE_TOO_LARGE",
                Message = $"File size exceeds maximum allowed size of {_configService.MaxFileSizeBytes} bytes"
            });
        }

        var accessKeyId = User.GetAccessKeyId() ?? "unknown";

        await using var stream = file.OpenReadStream();
        var result = await _coordinator.UploadAsync(
            region,
            stream,
            file.FileName,
            file.ContentType,
            accessKeyId,
            ct);

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

    [HttpGet]
    public async Task<IActionResult> List(string region, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

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

    [HttpGet("{fileId}")]
    public async Task<IActionResult> Download(string region, string fileId, CancellationToken ct)
    {
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        var metadata = await _metadata.GetByFileIdAsync(fileId, ct);
        if (metadata == null)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found" });
        }

        if (metadata.Region != region)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found in region '{region}'" });
        }

        try
        {
            var stream = await _fileStorage.GetFileAsync(region, fileId, ct);

            return File(stream, metadata.ContentType, metadata.OriginalFileName, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' physical file not found" });
        }
    }

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> Delete(string region, string fileId, CancellationToken ct)
    {
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

        if (!User.HasRegionAccess(region))
        {
            return StatusCode(403, new ErrorResponse
            {
                Error = "ACCESS_DENIED",
                Message = $"AccessKey does not have permission to access region '{region}'"
            });
        }

        var metadata = await _metadata.GetByFileIdAsync(fileId, ct);
        if (metadata == null)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found" });
        }

        if (metadata.Region != region)
        {
            return NotFound(new ErrorResponse { Error = "FILE_NOT_FOUND", Message = $"File '{fileId}' not found in region '{region}'" });
        }

        try
        {
            await _coordinator.DeleteAsync(region, fileId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
            return StatusCode(500, new ErrorResponse { Error = "DELETE_FAILED", Message = ex.Message });
        }
    }

    [HttpGet("~/api/{region}/quota")]
    public async Task<IActionResult> GetQuota(string region, CancellationToken ct)
    {
        if (!_configService.IsRegionValid(region))
        {
            return BadRequest(new ErrorResponse { Error = "INVALID_REGION", Message = $"Region '{region}' is not configured" });
        }

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
