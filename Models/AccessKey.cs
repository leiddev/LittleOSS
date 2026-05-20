namespace LittleOSS.Models;

public class AccessKey
{
    public string KeyId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string[] EnabledRegions { get; set; } = ["*"];
    public bool IsEnabled { get; set; } = true;
}
