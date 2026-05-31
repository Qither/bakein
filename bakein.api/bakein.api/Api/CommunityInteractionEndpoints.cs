using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class CommunityInteractionEndpoints
{
    public static RouteGroupBuilder MapCommunityInteractionEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/community").WithTags("Community");

        group.MapPost("/check-ins", CreateCheckInAsync);
        group.MapPost("/posts/{postId:guid}/comments", CreateCommentAsync);
        group.MapPost("/posts/{postId:guid}/likes", LikePostAsync);
        group.MapDelete("/posts/{postId:guid}/likes", UnlikePostAsync);
        group.MapPost("/posts/{postId:guid}/reports", ReportPostAsync);

        return routes;
    }

    private static async Task<IResult> CreateCheckInAsync(
        CommunityCheckInRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new ApiError("empty_post", "Post text is required."));
        }

        var status = request.MediaAssetId is null ? "published" : "media_review_pending";
        var post = await db.QuerySingleOrDefaultAsync(
            """
            insert into community_posts (
              account_id, author_name, course_id, step_id, media_asset_id, post_type, status, text, image_text
            ) values (
              @account_id, @author_name, @course_id, @step_id, @media_asset_id, 'check_in', @status, @text, '浣滃搧鐓х墖'
            )
            returning id, author_name, course_id, null::text as course_title, text, image_text, likes_count, comments_count, created_at
            """,
            CatalogEndpoints.MapCommunityPost,
            [
                Pg.Param("account_id", user.Id),
                Pg.Param("author_name", user.DisplayName),
                Pg.Param("course_id", string.IsNullOrWhiteSpace(request.CourseId) ? null : request.CourseId),
                Pg.Param("step_id", string.IsNullOrWhiteSpace(request.StepId) ? null : request.StepId),
                Pg.Param("media_asset_id", request.MediaAssetId),
                Pg.Param("status", status),
                Pg.Param("text", request.Text.Trim()),
            ],
            cancellationToken);

        await db.ExecuteAsync(
            """
            insert into learning_stats (account_id, check_in_count)
            values (@account_id, 1)
            on conflict (account_id) do update set
              check_in_count = learning_stats.check_in_count + 1,
              updated_at = now()
            """,
            [Pg.Param("account_id", user.Id)],
            cancellationToken);

        return post is null ? Results.Problem("Failed to create check-in.") : Results.Created($"/api/community/posts/{post.Id}", post);
    }

    private static async Task<IResult> CreateCommentAsync(
        Guid postId,
        CommunityCommentRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new ApiError("empty_comment", "Comment text is required."));
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        CommunityCommentDto? comment;
        await using (var command = new NpgsqlCommand(
            """
            insert into community_comments (post_id, account_id, author_name, text)
            select @post_id, @account_id, @author_name, @text
            where exists (select 1 from community_posts where id = @post_id and status = 'published')
            returning id, post_id, author_name, text, created_at
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("post_id", postId));
            command.Parameters.Add(Pg.Param("account_id", user.Id));
            command.Parameters.Add(Pg.Param("author_name", user.DisplayName));
            command.Parameters.Add(Pg.Param("text", request.Text.Trim()));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            comment = await reader.ReadAsync(cancellationToken)
                ? new CommunityCommentDto(
                    reader.GetGuid(reader.GetOrdinal("id")),
                    reader.GetGuid(reader.GetOrdinal("post_id")),
                    reader.GetString(reader.GetOrdinal("author_name")),
                    reader.GetString(reader.GetOrdinal("text")),
                    reader.GetDateTimeOffset("created_at"))
                : null;
        }

        if (comment is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.NotFound(new ApiError("post_not_found", "Published post was not found."));
        }

        await using (var command = new NpgsqlCommand(
            "update community_posts set comments_count = comments_count + 1 where id = @id",
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", postId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/community/posts/{postId}/comments/{comment.Id}", comment);
    }

    private static async Task<IResult> LikePostAsync(Guid postId, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var inserted = await db.QuerySingleOrDefaultAsync(
            """
            insert into community_likes (account_id, target_type, target_id)
            select @account_id, 'post', @target_id
            where exists (select 1 from community_posts where id = @target_id and status = 'published')
            on conflict do nothing
            returning target_id
            """,
            reader => reader.GetGuid(0),
            [Pg.Param("account_id", user.Id), Pg.Param("target_id", postId)],
            cancellationToken);

        if (inserted == Guid.Empty)
        {
            if (!await PublishedPostExistsAsync(db, postId, cancellationToken))
            {
                return Results.NotFound(new ApiError("post_not_found", "Published post was not found."));
            }

            return Results.Ok(new { postId, liked = true, changed = false });
        }

        await db.ExecuteAsync(
            "update community_posts set likes_count = likes_count + 1 where id = @id",
            [Pg.Param("id", postId)],
            cancellationToken);

        return Results.Ok(new { postId, liked = true, changed = true });
    }

    private static async Task<IResult> UnlikePostAsync(Guid postId, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var removed = await db.ExecuteAsync(
            """
            delete from community_likes
            where account_id = @account_id
              and target_type = 'post'
              and target_id = @target_id
              and exists (select 1 from community_posts where id = @target_id and status = 'published')
            """,
            [Pg.Param("account_id", user.Id), Pg.Param("target_id", postId)],
            cancellationToken);

        if (removed == 0 && !await PublishedPostExistsAsync(db, postId, cancellationToken))
        {
            return Results.NotFound(new ApiError("post_not_found", "Published post was not found."));
        }

        if (removed > 0)
        {
            await db.ExecuteAsync(
                "update community_posts set likes_count = greatest(0, likes_count - 1) where id = @id",
                [Pg.Param("id", postId)],
                cancellationToken);
        }

        return Results.Ok(new { postId, liked = false, changed = removed > 0 });
    }

    private static async Task<IResult> ReportPostAsync(
        Guid postId,
        CommunityReportRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new ApiError("invalid_report", "Report reason is required."));
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid? reportId;
        await using (var command = new NpgsqlCommand(
            """
            insert into community_reports (target_type, target_id, account_id, reason)
            select 'post', @target_id, @account_id, @reason
            where exists (select 1 from community_posts where id = @target_id and status = 'published')
            returning id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("target_id", postId));
            command.Parameters.Add(Pg.Param("account_id", user.Id));
            command.Parameters.Add(Pg.Param("reason", request.Reason.Trim()));
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            reportId = scalar is Guid id ? id : null;
        }

        if (reportId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.NotFound(new ApiError("post_not_found", "Post was not found."));
        }

        await using (var command = new NpgsqlCommand(
            """
            insert into moderation_tasks (target_type, target_id, reason)
            values ('community_post', @target_id, @reason)
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("target_id", postId.ToString()));
            command.Parameters.Add(Pg.Param("reason", request.Reason.Trim()));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/community/reports/{reportId}", new { id = reportId, postId, status = "open" });
    }

    private static async Task<bool> PublishedPostExistsAsync(NpgsqlDataSource db, Guid postId, CancellationToken cancellationToken) =>
        await db.QuerySingleOrDefaultAsync(
            "select exists(select 1 from community_posts where id = @id and status = 'published')",
            reader => reader.GetBoolean(0),
            [Pg.Param("id", postId)],
            cancellationToken);
}
