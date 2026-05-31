using Bakein.Api.Application.Providers;

namespace Bakein.Api.Infrastructure.Providers.Local;

public sealed class LocalMediaProvider : IMediaProvider, IMediaReviewProvider
{
    public Task<MediaUploadIntentResult> CreateUploadIntentAsync(MediaUploadIntentCommand command, CancellationToken cancellationToken)
    {
        var providerFileId = $"local-{command.MediaAssetId:N}";
        var result = new MediaUploadIntentResult(
            "local",
            providerFileId,
            $"/local/media/uploads/{command.MediaAssetId:N}",
            null,
            DateTimeOffset.UtcNow.AddMinutes(30));

        return Task.FromResult(result);
    }

    public MediaReviewDecision Decide(string suggestion)
    {
        var normalized = suggestion.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pass" or "approved" or "approve" => new MediaReviewDecision("pass", "approved", null),
            "manual" or "review" => new MediaReviewDecision("manual", "review_pending", "manual_review_required"),
            "block" or "blocked" or "reject" => new MediaReviewDecision("block", "blocked", "provider_blocked"),
            _ => new MediaReviewDecision("manual", "review_pending", "unknown_provider_suggestion"),
        };
    }
}
