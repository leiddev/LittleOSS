namespace LittleOSS.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(string region, string fileId, Stream stream, CancellationToken ct = default);
    Task<Stream> GetFileAsync(string region, string fileId, CancellationToken ct = default);
    Task DeleteFileAsync(string region, string fileId, CancellationToken ct = default);
    Task<bool> FileExistsAsync(string region, string fileId, CancellationToken ct = default);
}
