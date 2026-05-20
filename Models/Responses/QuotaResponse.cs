namespace LittleOSS.Models.Responses;

public class QuotaResponse
{
    public required string Region { get; set; }
    public required long UsedBytes { get; set; }
    public required long QuotaBytes { get; set; }
    public required double UsedPercentage { get; set; }
}
