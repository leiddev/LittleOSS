using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using LittleOSS.Models;
using LittleOSS.Options;

namespace LittleOSS.Authentication;

public class OssAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string AccessKeyIdHeader = "X-AccessKey-Id";
    private const string AccessKeySecretHeader = "X-AccessKey-Secret";

    private readonly AccessKeyOptions _accessKeyOptions;

    public OssAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AccessKeyOptions accessKeyOptions)
        : base(options, logger, encoder)
    {
        _accessKeyOptions = accessKeyOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AccessKeyIdHeader, out var keyIdValues) ||
            string.IsNullOrWhiteSpace(keyIdValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {AccessKeyIdHeader} header"));
        }

        if (!Request.Headers.TryGetValue(AccessKeySecretHeader, out var secretValues) ||
            string.IsNullOrWhiteSpace(secretValues.FirstOrDefault()))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {AccessKeySecretHeader} header"));
        }

        var keyId = keyIdValues.First()!;
        var secretKey = secretValues.First()!;

        var accessKey = _accessKeyOptions.AccessKeys
            .FirstOrDefault(k => k.KeyId == keyId && k.SecretKey == secretKey);

        if (accessKey == null)
        {
            Logger.LogWarning("Authentication failed: Invalid AccessKey for KeyId={KeyId}", keyId);
            return Task.FromResult(AuthenticateResult.Fail("Invalid AccessKey"));
        }

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

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Invalid or missing AccessKey credentials\"}");
    }
}

public static class AuthenticationExtensions
{
    public static bool HasRegionAccess(this ClaimsPrincipal user, string region)
    {
        var enabledRegionsClaim = user.FindFirst("EnabledRegions")?.Value;
        if (string.IsNullOrEmpty(enabledRegionsClaim))
            return false;

        var enabledRegions = enabledRegionsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return enabledRegions.Contains("*") || enabledRegions.Contains(region, StringComparer.OrdinalIgnoreCase);
    }

    public static string? GetAccessKeyId(this ClaimsPrincipal user)
    {
        return user.FindFirst("AccessKeyId")?.Value;
    }
}
