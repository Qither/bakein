namespace Bakein.Api.Application.Providers;

public interface IMediaProvider
{
    Task<MediaUploadIntentResult> CreateUploadIntentAsync(MediaUploadIntentCommand command, CancellationToken cancellationToken);
}

public interface IMediaReviewProvider
{
    MediaReviewDecision Decide(string suggestion);
}

public sealed record MediaUploadIntentCommand(
    Guid MediaAssetId,
    Guid AccountId,
    string FileName,
    string ContentType,
    string MediaType);

public sealed record MediaUploadIntentResult(
    string Provider,
    string ProviderFileId,
    string UploadUrl,
    string? PlaybackUrl,
    DateTimeOffset ExpiresAt);

public sealed record MediaReviewDecision(string Suggestion, string MediaStatus, string? ModerationReason);
