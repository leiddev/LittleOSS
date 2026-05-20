namespace LittleOSS.Models.Requests;

public class UploadFileRequest
{
    public required IFormFile File { get; set; }
}
