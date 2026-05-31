using Bakein.Api.Application.Providers;
using Bakein.Api.Infrastructure.Providers.Local;
using Bakein.Api.Infrastructure.Providers.TencentVod;
using Bakein.Api.Infrastructure.Providers.WeChat;

namespace Bakein.Api.Infrastructure.Providers;

public static class ProviderRegistration
{
    public static IServiceCollection AddBakeinProviders(this IServiceCollection services, IConfiguration configuration)
    {
        var mediaProvider = NormalizeProvider(configuration["Media:Provider"]);
        var paymentProvider = NormalizeProvider(configuration["Payment:Provider"]);
        var tencentVodConfigured = HasTencentVodConfiguration(configuration);

        services.AddSingleton(new ProviderRuntimeMode(mediaProvider, paymentProvider, tencentVodConfigured));

        RegisterMediaProvider(services, configuration, mediaProvider);
        RegisterPaymentProvider(services, paymentProvider);
        RegisterExternalIdentityProviders(services);

        return services;
    }

    private static void RegisterExternalIdentityProviders(IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IWechatIdentityProvider, WechatIdentityProvider>();
    }

    private static void RegisterMediaProvider(IServiceCollection services, IConfiguration configuration, string provider)
    {
        if (provider == "local")
        {
            services.AddSingleton<LocalMediaProvider>();
            services.AddSingleton<IMediaProvider>(sp => sp.GetRequiredService<LocalMediaProvider>());
            services.AddSingleton<IMediaReviewProvider>(sp => sp.GetRequiredService<LocalMediaProvider>());
            return;
        }

        if (provider == "tencent_vod")
        {
            ValidateTencentVodConfiguration(configuration);
            throw new InvalidOperationException(
                "Media:Provider is 'tencent_vod', but the Tencent VOD production adapter is not enabled in this local/mock execution lane. " +
                "Set Media:Provider to 'local' or implement and register the Tencent VOD adapter before selecting it.");
        }

        throw new InvalidOperationException($"Unsupported Media:Provider '{provider}'. Supported value in this execution lane: local.");
    }

    private static void RegisterPaymentProvider(IServiceCollection services, string provider)
    {
        if (provider == "local")
        {
            services.AddSingleton<IPaymentProvider, LocalPaymentProvider>();
            return;
        }

        throw new InvalidOperationException($"Unsupported Payment:Provider '{provider}'. Supported value in this execution lane: local.");
    }

    private static void ValidateTencentVodConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(TencentVodOptions.SectionName);
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(section["Region"]))
        {
            missing.Add($"{TencentVodOptions.SectionName}:Region");
        }

        if (string.IsNullOrWhiteSpace(section["SubApplicationId"]))
        {
            missing.Add($"{TencentVodOptions.SectionName}:SubApplicationId");
        }

        if (string.IsNullOrWhiteSpace(section["UploadProcedureTemplate"]))
        {
            missing.Add($"{TencentVodOptions.SectionName}:UploadProcedureTemplate");
        }

        if (string.IsNullOrWhiteSpace(section["ReviewProcedureTemplate"]))
        {
            missing.Add($"{TencentVodOptions.SectionName}:ReviewProcedureTemplate");
        }

        if (string.IsNullOrWhiteSpace(section["CallbackSecret"]))
        {
            missing.Add($"{TencentVodOptions.SectionName}:CallbackSecret");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Tencent VOD provider selected without required configuration: {string.Join(", ", missing)}.");
        }
    }

    private static bool HasTencentVodConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(TencentVodOptions.SectionName);
        return !string.IsNullOrWhiteSpace(section["Region"]) ||
            !string.IsNullOrWhiteSpace(section["SubApplicationId"]) ||
            !string.IsNullOrWhiteSpace(section["UploadProcedureTemplate"]) ||
            !string.IsNullOrWhiteSpace(section["ReviewProcedureTemplate"]) ||
            !string.IsNullOrWhiteSpace(section["CallbackSecret"]);
    }

    private static string NormalizeProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? "local" : provider.Trim().ToLowerInvariant();
}
