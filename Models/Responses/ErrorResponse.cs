namespace LittleOSS.Models.Responses;

public class ErrorResponse
{
    public required string Error { get; set; }
    public string? Message { get; set; }
    public string? Details { get; set; }
}
