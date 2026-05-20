using Microsoft.EntityFrameworkCore;
using LittleOSS.Data;
using LittleOSS.Models;

namespace LittleOSS.Services;

public class MetadataService : IMetadataService
{
    private readonly IDbContextFactory<OssDbContext> _contextFactory;
    private readonly ILogger<MetadataService> _logger;

    public MetadataService(IDbContextFactory<OssDbContext> contextFactory, ILogger<MetadataService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<FileMetadata?> GetByFileIdAsync(string fileId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.FileMetadatas
            .AsNoTracking()
            .Where(f => f.FileId == fileId && !f.IsDeleted)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<FileMetadata>> GetRegionFilesAsync(
        string region,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var files = await context.FileMetadatas
            .AsNoTracking()
            .Where(f => f.Region == region && !f.IsDeleted)
            .OrderByDescending(f => f.UploadTime)
            .Skip(skip)
            .Take(Math.Min(take, 1000))
            .ToListAsync(ct);

        return files.AsReadOnly();
    }

    public async Task<int> GetRegionFileCountAsync(string region, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.FileMetadatas
            .CountAsync(f => f.Region == region && !f.IsDeleted, ct);
    }

    public async Task CreateRecordAsync(FileMetadata metadata, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.FileMetadatas.Add(metadata);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "File metadata created: FileId={FileId}, Region={Region}, Size={Size}",
            metadata.FileId, metadata.Region, metadata.FileSizeBytes);
    }

    public async Task SoftDeleteAsync(string fileId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var metadata = await context.FileMetadatas
            .Where(f => f.FileId == fileId && !f.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (metadata != null)
        {
            metadata.IsDeleted = true;
            metadata.DeletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("File metadata soft deleted: FileId={FileId}", fileId);
        }
    }
}
