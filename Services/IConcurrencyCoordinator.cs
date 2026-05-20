using LittleOSS.Models;

namespace LittleOSS.Services;

public interface IConcurrencyCoordinator
{
    Task<UploadResult> UploadAsync(
        string region,
        Stream stream,
        string originalFileName,
        string contentType,
        string accessKeyId,
        CancellationToken ct = default);

    Task DeleteAsync(string region, string fileId, CancellationToken ct = default);
}

public record UploadResult(
    bool Success,
    string? FileId = null,
    FileMetadata? Metadata = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
