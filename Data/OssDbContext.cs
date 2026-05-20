using Microsoft.EntityFrameworkCore;
using LittleOSS.Models;

namespace LittleOSS.Data;

/// <summary>
/// 文件元数据数据库上下文，使用 SQLite 存储文件索引信息
/// </summary>
public class OssDbContext : DbContext
{
    /// <summary>
    /// 初始化数据库上下文
    /// </summary>
    public OssDbContext(DbContextOptions<OssDbContext> options) : base(options)
    {
    }

    /// <summary>文件元数据表</summary>
    public DbSet<FileMetadata> FileMetadatas { get; set; } = null!;

    /// <summary>
    /// 配置实体模型映射和数据库约束
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FileMetadata>(entity =>
        {
            // 主键：自增整数 ID
            entity.HasKey(e => e.Id);

            // FileId：文件唯一标识，64 字符，设为唯一索引
            entity.Property(e => e.FileId)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(e => e.FileId)
                .IsUnique();

            // OriginalFileName：原始文件名，255 字符
            entity.Property(e => e.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            // Region：所属区域，64 字符
            entity.Property(e => e.Region)
                .IsRequired()
                .HasMaxLength(64);

            // FilePath：文件存储路径，512 字符
            entity.Property(e => e.FilePath)
                .IsRequired()
                .HasMaxLength(512);

            // ContentType：MIME 类型，128 字符
            entity.Property(e => e.ContentType)
                .HasMaxLength(128);

            // AccessKeyId：所属的 AccessKey ID，64 字符
            entity.Property(e => e.AccessKeyId)
                .IsRequired()
                .HasMaxLength(64);

            // 创建常用索引以提升查询性能
            entity.HasIndex(e => e.Region);
            entity.HasIndex(e => e.AccessKeyId);
            entity.HasIndex(e => new { e.Region, e.IsDeleted });
        });
    }
}
