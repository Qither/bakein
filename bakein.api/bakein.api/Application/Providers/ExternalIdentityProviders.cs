namespace Bakein.Api.Application.Providers;

public interface IExternalIdentityProvider
{
    string ProviderName { get; }
}

public interface IWechatIdentityProvider : IExternalIdentityProvider
{
    Task<WechatSession> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
}

public sealed record WechatSession(string OpenId, string? UnionId, string SessionKey);

public sealed class ExternalIdentityProviderException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
