namespace LittleOSS.Options;

public class OssOptions
{
    public const string Position = "Oss";

    public string StorageRoot { get; set; } = "./oss-storage";

    public string[] Regions { get; set; } = [];

    public Dictionary<string, string> MaxQuotaPerRegion { get; set; } = new();

    public long MaxFileSizeBytes { get; set; } = 10485760; // 10MB default
}

public class AccessKeyOptions
{
    public const string Position = "AccessKeys";

    public List<AccessKeyConfig> AccessKeys { get; set; } = [];
}

public class AccessKeyConfig
{
    public string KeyId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string[] EnabledRegions { get; set; } = ["*"];
}
