namespace LittleOSS.Models.Responses;

public class FileInfoResponse
{
    public required string FileId { get; set; }
    public required string OriginalFileName { get; set; }
    public required string Region { get; set; }
    public required long FileSizeBytes { get; set; }
    public required string ContentType { get; set; }
    public required DateTime UploadTime { get; set; }
}
