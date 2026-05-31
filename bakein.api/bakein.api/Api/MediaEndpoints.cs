using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bakein.Api.Application.Providers;
using Bakein.Api.Domain;
using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class MediaEndpoints
{
    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/media").WithTags("Media");

        group.MapPost("/upload-intents", CreateUploadIntentAsync);
        group.MapGet("/assets/{id:guid}", GetMediaAssetAsync);
        group.MapPost("/callbacks/local", HandleLocalCallbackAsync);

        return routes;
    }

    private static async Task<IResult> CreateUploadIntentAsync(
        MediaUploadIntentRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        IMediaProvider mediaProvider,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentType))
        {
            return Results.BadRequest(new ApiError("invalid_media", "File name and content type are required."));
        }

        var mediaAssetId = Guid.NewGuid();
        var providerIntent = await mediaProvider.CreateUploadIntentAsync(
            new MediaUploadIntentCommand(
                mediaAssetId,
                user.Id,
                request.FileName.Trim(),
                request.ContentType.Trim(),
                NormalizeMediaType(request.MediaType)),
            cancellationToken);
        var uploadIntentId = Guid.NewGuid();

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(
            """
            insert into media_assets (
              id, owner_account_id, provider, provider_file_id, media_type, file_name, content_type, status, playback_url
            ) values (
              @id, @owner_account_id, @provider, @provider_file_id, @media_type, @file_name, @content_type, 'upload_pending', @playback_url
            )
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", mediaAssetId));
            command.Parameters.Add(Pg.Param("owner_account_id", user.Id));
            command.Parameters.Add(Pg.Param("provider", providerIntent.Provider));
            command.Parameters.Add(Pg.Param("provider_file_id", providerIntent.ProviderFileId));
            command.Parameters.Add(Pg.Param("media_type", NormalizeMediaType(request.MediaType)));
            command.Parameters.Add(Pg.Param("file_name", request.FileName.Trim()));
            command.Parameters.Add(Pg.Param("content_type", request.ContentType.Trim()));
            command.Parameters.Add(Pg.Param("playback_url", providerIntent.PlaybackUrl));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            insert into media_upload_intents (id, media_asset_id, provider, provider_file_id, upload_url, expires_at)
            values (@id, @media_asset_id, @provider, @provider_file_id, @upload_url, @expires_at)
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", uploadIntentId));
            command.Parameters.Add(Pg.Param("media_asset_id", mediaAssetId));
            command.Parameters.Add(Pg.Param("provider", providerIntent.Provider));
            command.Parameters.Add(Pg.Param("provider_file_id", providerIntent.ProviderFileId));
            command.Parameters.Add(Pg.Param("upload_url", providerIntent.UploadUrl));
            command.Parameters.Add(Pg.Param("expires_at", providerIntent.ExpiresAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var dto = new MediaUploadIntentDto(
            mediaAssetId,
            uploadIntentId,
            providerIntent.Provider,
            providerIntent.ProviderFileId,
            providerIntent.UploadUrl,
            providerIntent.PlaybackUrl,
            providerIntent.ExpiresAt,
            "upload_pending");

        return Results.Created($"/api/media/assets/{mediaAssetId}", dto);
    }

    private static async Task<IResult> GetMediaAssetAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var asset = await db.QuerySingleOrDefaultAsync(
            """
            select id, owner_account_id, provider, provider_file_id, media_type, file_name, content_type, status, playback_url, review_suggestion
            from media_assets
            where id = @id
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                OwnerAccountId = reader.GetGuid(reader.GetOrdinal("owner_account_id")),
                Provider = reader.GetString(reader.GetOrdinal("provider")),
                ProviderFileId = reader.GetNullableString("provider_file_id"),
                MediaType = reader.GetString(reader.GetOrdinal("media_type")),
                FileName = reader.GetString(reader.GetOrdinal("file_name")),
                ContentType = reader.GetString(reader.GetOrdinal("content_type")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                PlaybackUrl = reader.GetNullableString("playback_url"),
                ReviewSuggestion = reader.GetNullableString("review_suggestion"),
            },
            [Pg.Param("id", id)],
            cancellationToken);

        if (asset is null)
        {
            return Results.NotFound(new ApiError("media_not_found", "Media asset was not found."));
        }

        return asset.OwnerAccountId == user.Id || IsStaff(user)
            ? Results.Ok(asset)
            : Results.NotFound(new ApiError("media_not_found", "Media asset was not found."));
    }

    private static async Task<IResult> HandleLocalCallbackAsync(
        LocalMediaCallbackRequest request,
        NpgsqlDataSource db,
        IMediaReviewProvider reviewProvider,
        CancellationToken cancellationToken)
    {
        var asset = await db.QuerySingleOrDefaultAsync(
            """
            select id, provider, provider_file_id, status
            from media_assets
            where id = @id
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Provider = reader.GetString(reader.GetOrdinal("provider")),
                ProviderFileId = reader.GetNullableString("provider_file_id"),
                Status = reader.GetString(reader.GetOrdinal("status")),
            },
            [Pg.Param("id", request.MediaAssetId)],
            cancellationToken);

        if (asset is null)
        {
            return Results.NotFound(new ApiError("media_not_found", "Media asset was not found."));
        }

        var providerFileId = request.ProviderFileId ?? asset.ProviderFileId ?? $"local-{asset.Id:N}";
        var providerEventId = request.ProviderEventId ?? $"{providerFileId}:{request.EventType}:{request.Suggestion}";
        var payload = JsonSerializer.Serialize(request);
        var eventHash = ComputeHash($"local:{request.EventType}:{providerFileId}:{providerEventId}:{request.Suggestion}");
        var decision = reviewProvider.Decide(request.Suggestion);

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid? eventId;
        await using (var command = new NpgsqlCommand(
            """
            insert into media_provider_events (provider, event_type, provider_file_id, provider_event_id, event_hash, payload)
            values ('local', @event_type, @provider_file_id, @provider_event_id, @event_hash, @payload::jsonb)
            on conflict do nothing
            returning id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("event_type", request.EventType));
            command.Parameters.Add(Pg.Param("provider_file_id", providerFileId));
            command.Parameters.Add(Pg.Param("provider_event_id", providerEventId));
            command.Parameters.Add(Pg.Param("event_hash", eventHash));
            command.Parameters.Add(Pg.Param("payload", payload));
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            eventId = scalar is Guid id ? id : null;
        }

        if (eventId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new MediaCallbackResultDto(asset.Id, asset.Status, decision.Suggestion, Duplicate: true));
        }

        if (!MediaAssetState.CanAdvanceTo(asset.Status, decision.MediaStatus))
        {
            await using var staleCommand = new NpgsqlCommand(
                "update media_provider_events set processed_at = now() where id = @id",
                connection,
                transaction);
            staleCommand.Parameters.Add(Pg.Param("id", eventId.Value));
            await staleCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new MediaCallbackResultDto(asset.Id, asset.Status, decision.Suggestion, Duplicate: false));
        }

        await using (var command = new NpgsqlCommand(
            """
            update media_assets
            set status = @status,
                review_suggestion = @suggestion,
                updated_at = now()
            where id = @id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", asset.Id));
            command.Parameters.Add(Pg.Param("status", decision.MediaStatus));
            command.Parameters.Add(Pg.Param("suggestion", decision.Suggestion));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            insert into media_review_results (media_asset_id, provider, suggestion, raw_payload)
            values (@media_asset_id, 'local', @suggestion, @payload::jsonb)
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("media_asset_id", asset.Id));
            command.Parameters.Add(Pg.Param("suggestion", decision.Suggestion));
            command.Parameters.Add(Pg.Param("payload", payload));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (decision.ModerationReason is not null)
        {
            await using var moderationCommand = new NpgsqlCommand(
                """
                insert into moderation_tasks (target_type, target_id, reason)
                values ('media_asset', @target_id, @reason)
                """,
                connection,
                transaction);
            moderationCommand.Parameters.Add(Pg.Param("target_id", asset.Id.ToString()));
            moderationCommand.Parameters.Add(Pg.Param("reason", decision.ModerationReason));
            await moderationCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else if (decision.MediaStatus == "approved")
        {
            await using var publishPostsCommand = new NpgsqlCommand(
                """
                update community_posts
                set status = 'published'
                where media_asset_id = @media_asset_id and status = 'media_review_pending'
                """,
                connection,
                transaction);
            publishPostsCommand.Parameters.Add(Pg.Param("media_asset_id", asset.Id));
            await publishPostsCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            "update media_provider_events set processed_at = now() where id = @id",
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", eventId.Value));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            insert into outbox_events (aggregate_type, aggregate_id, event_type, payload)
            values ('media_asset', @aggregate_id, 'media.reviewed', @payload::jsonb)
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("aggregate_id", asset.Id.ToString()));
            command.Parameters.Add(Pg.Param("payload", payload));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new MediaCallbackResultDto(asset.Id, decision.MediaStatus, decision.Suggestion, Duplicate: false));
    }

    private static string NormalizeMediaType(string mediaType) =>
        string.IsNullOrWhiteSpace(mediaType) ? "image" : mediaType.Trim().ToLowerInvariant();

    private static bool IsStaff(AuthenticatedUser user) => user.Role is "staff" or "admin";

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
