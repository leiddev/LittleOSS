using System.ComponentModel.DataAnnotations;

namespace LittleOSS.Models;

public class FileMetadata
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string FileId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Region { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(128)]
    public string ContentType { get; set; } = string.Empty;

    public DateTime UploadTime { get; set; }

    [Required]
    [MaxLength(64)]
    public string AccessKeyId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }
}
