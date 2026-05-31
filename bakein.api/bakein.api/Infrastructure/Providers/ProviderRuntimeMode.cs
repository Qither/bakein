namespace Bakein.Api.Infrastructure.Providers;

public sealed record ProviderRuntimeMode(string MediaProvider, string PaymentProvider, bool TencentVodConfigured);
