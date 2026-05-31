using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Bakein.Api.Application.Providers;

namespace Bakein.Api.Infrastructure.Providers.WeChat;

public sealed class WechatIdentityProvider(HttpClient httpClient, IConfiguration configuration) : IWechatIdentityProvider
{
    private const string DefaultCode2SessionEndpoint = "https://api.weixin.qq.com/sns/jscode2session";

    public string ProviderName => "wechat";

    public async Task<WechatSession> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new ExternalIdentityProviderException("wechat_code_required", "WeChat login code is required.");
        }

        if (configuration.GetValue("WeChat:UseMockSession", false))
        {
            return CreateMockSession(normalizedCode);
        }

        var appId = configuration["WeChat:AppId"];
        var appSecret = configuration["WeChat:AppSecret"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
        {
            throw new ExternalIdentityProviderException(
                "wechat_not_configured",
                "WeChat AppId and AppSecret must be configured before WeChat registration can be used.");
        }

        var endpoint = string.IsNullOrWhiteSpace(configuration["WeChat:Code2SessionEndpoint"])
            ? DefaultCode2SessionEndpoint
            : configuration["WeChat:Code2SessionEndpoint"]!.Trim();

        var url = BuildCode2SessionUrl(endpoint, appId, appSecret, normalizedCode);
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalIdentityProviderException(
                "wechat_unavailable",
                $"WeChat code2session returned HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<WechatCode2SessionResponse>(cancellationToken);
        if (payload is null)
        {
            throw new ExternalIdentityProviderException("wechat_invalid_response", "WeChat code2session returned an empty response.");
        }

        if (payload.ErrCode is not null and not 0)
        {
            throw new ExternalIdentityProviderException(
                "wechat_code_invalid",
                string.IsNullOrWhiteSpace(payload.ErrMsg) ? "WeChat login code was rejected." : payload.ErrMsg);
        }

        if (string.IsNullOrWhiteSpace(payload.OpenId) || string.IsNullOrWhiteSpace(payload.SessionKey))
        {
            throw new ExternalIdentityProviderException("wechat_invalid_response", "WeChat code2session did not return an openid/session_key.");
        }

        return new WechatSession(payload.OpenId, payload.UnionId, payload.SessionKey);
    }

    private static string BuildCode2SessionUrl(string endpoint, string appId, string appSecret, string code) =>
        $"{endpoint}?appid={Uri.EscapeDataString(appId)}" +
        $"&secret={Uri.EscapeDataString(appSecret)}" +
        $"&js_code={Uri.EscapeDataString(code)}" +
        "&grant_type=authorization_code";

    private static WechatSession CreateMockSession(string code)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
        return new WechatSession($"mock_{hash[..24]}", null, $"mock_session_{hash[..24]}");
    }

    private sealed record WechatCode2SessionResponse(
        [property: JsonPropertyName("openid")] string? OpenId,
        [property: JsonPropertyName("session_key")] string? SessionKey,
        [property: JsonPropertyName("unionid")] string? UnionId,
        [property: JsonPropertyName("errcode")] int? ErrCode,
        [property: JsonPropertyName("errmsg")] string? ErrMsg);
}
