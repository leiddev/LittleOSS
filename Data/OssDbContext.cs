using Microsoft.EntityFrameworkCore;
using LittleOSS.Models;

namespace LittleOSS.Data;

public class OssDbContext : DbContext
{
    public OssDbContext(DbContextOptions<OssDbContext> options) : base(options)
    {
    }

    public DbSet<FileMetadata> FileMetadatas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FileMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FileId)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(e => e.FileId)
                .IsUnique();

            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Region)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(e => e.ContentType)
                .HasMaxLength(128);

            entity.Property(e => e.AccessKeyId)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(e => e.Region);
            entity.HasIndex(e => e.AccessKeyId);
            entity.HasIndex(e => new { e.Region, e.IsDeleted });
        });
    }
}
