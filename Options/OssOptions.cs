namespace LittleOSS.Options;

public class OssOptions
{
    public const string Position = "Oss";

    public string StorageRoot { get; set; } = "./oss-storage";

    public string[] Regions { get; set; } = [];

    public Dictionary<string, string> MaxQuotaPerRegion { get; set; } = new();

    public long MaxFileSizeBytes { get; set; } = 10485760; // 10MB default

    public DatabaseOptions Database { get; set; } = new();
}

public class DatabaseOptions
{
    /// 数据库类型：Sqlite / MySql
    public string Provider { get; set; } = "Sqlite";

    /// MySQL 连接字符串（当 Provider=MySql 时使用）
    public string? ConnectionString { get; set; }

    /// SQLite 数据库文件路径（当 Provider=Sqlite 时使用）
    public string? SqlitePath { get; set; }
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
