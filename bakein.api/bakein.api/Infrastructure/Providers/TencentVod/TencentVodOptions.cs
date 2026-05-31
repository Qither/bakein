namespace Bakein.Api.Infrastructure.Providers.TencentVod;

public sealed class TencentVodOptions
{
    public const string SectionName = "TencentVod";

    public string Region { get; init; } = "";

    public long? SubApplicationId { get; init; }

    public string UploadProcedureTemplate { get; init; } = "";

    public string ReviewProcedureTemplate { get; init; } = "";

    public string CallbackSecret { get; init; } = "";
}
