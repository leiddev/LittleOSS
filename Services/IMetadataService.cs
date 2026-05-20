using LittleOSS.Models;

namespace LittleOSS.Services;

public interface IMetadataService
{
    Task<FileMetadata?> GetByFileIdAsync(string fileId, CancellationToken ct = default);
    Task<IReadOnlyList<FileMetadata>> GetRegionFilesAsync(string region, int skip = 0, int take = 100, CancellationToken ct = default);
    Task<int> GetRegionFileCountAsync(string region, CancellationToken ct = default);
    Task CreateRecordAsync(FileMetadata metadata, CancellationToken ct = default);
    Task SoftDeleteAsync(string fileId, CancellationToken ct = default);
}
