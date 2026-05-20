using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using LittleOSS.Models;
using LittleOSS.Options;

namespace LittleOSS.Authentication;

/// <summary>
/// 基于 AccessKey 的认证处理器，用于验证客户端请求头中的密钥信息
/// </summary>
public class OssAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>请求头名称：AccessKey ID</summary>
    private const string AccessKeyIdHeader = "X-AccessKey-Id";

    /// <summary>请求头名称：AccessKey 密钥</summary>
    private const string AccessKeySecretHeader = "X-AccessKey-Secret";

    private readonly AccessKeyOptions _accessKeyOptions;

    /// <summary>
    /// 初始化认证处理器
    /// </summary>
    public OssAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AccessKeyOptions accessKeyOptions)
        : base(options, logger, encoder)
    {
        _accessKeyOptions = accessKeyOptions;
    }

    /// <summary>
    /// 处理认证逻辑：从请求头提取密钥并验证
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 验证 X-AccessKey-Id 请求头
        if (!Request.Headers.TryGetValue(AccessKeyIdHeader, out var keyIdValues) ||
            string.IsNullOrWhiteSpace(keyIdValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {AccessKeyIdHeader} header"));
        }

        // 验证 X-AccessKey-Secret 请求头
        if (!Request.Headers.TryGetValue(AccessKeySecretHeader, out var secretValues) ||
            string.IsNullOrWhiteSpace(secretValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {AccessKeySecretHeader} header"));
        }

        var keyId = keyIdValues.First()!;
        var secretKey = secretValues.First()!;

        // 在配置中查找匹配的 AccessKey
        var accessKey = _accessKeyOptions.AccessKeys
            .FirstOrDefault(k => k.KeyId == keyId && k.SecretKey == secretKey);

        if (accessKey == null)
        {
            Logger.LogWarning("Authentication failed: Invalid AccessKey for KeyId={KeyId}", keyId);
            return Task.FromResult(AuthenticateResult.Fail("Invalid AccessKey"));
        }

        // 构建声明并生成认证票据
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accessKey.KeyId),
            new Claim("AccessKeyId", accessKey.KeyId),
            new Claim("EnabledRegions", string.Join(",", accessKey.EnabledRegions))
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogDebug("Authentication successful for KeyId={KeyId}", keyId);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// 处理未授权访问，返回 JSON 格式的错误响应
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Invalid or missing AccessKey credentials\"}");
    }
}

/// <summary>
/// 认证相关的扩展方法，用于 ClaimsPrincipal 的便捷操作
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// 检查当前用户是否有权访问指定区域
    /// </summary>
    /// <param name="user">当前用户主体</param>
    /// <param name="region">区域名称</param>
    /// <returns>有权限返回 true，否则返回 false</returns>
    public static bool HasRegionAccess(this ClaimsPrincipal user, string region)
    {
        var enabledRegionsClaim = user.FindFirst("EnabledRegions")?.Value;
        if (string.IsNullOrEmpty(enabledRegionsClaim))
            return false;

        var enabledRegions = enabledRegionsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return enabledRegions.Contains("*") || enabledRegions.Contains(region, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取当前用户的 AccessKey ID
    /// </summary>
    public static string? GetAccessKeyId(this ClaimsPrincipal user)
    {
        return user.FindFirst("AccessKeyId")?.Value;
    }
}
