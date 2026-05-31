using Bakein.Api.Domain;
using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/admin").WithTags("Admin");

        group.MapPost("/courses/versions", CreateCourseVersionAsync);
        group.MapPost("/courses/versions/{id:guid}/steps", CreateCourseVersionStepAsync);
        group.MapPost("/courses/versions/{id:guid}/submit", SubmitCourseVersionAsync);
        group.MapPost("/courses/versions/{id:guid}/approve", ApproveCourseVersionAsync);
        group.MapPost("/courses/versions/{id:guid}/publish", PublishCourseVersionAsync);
        group.MapPost("/courses/versions/{id:guid}/archive", ArchiveCourseVersionAsync);
        group.MapGet("/moderation/tasks", GetModerationTasksAsync);
        group.MapPost("/moderation/tasks/{id:guid}/resolve", ResolveModerationTaskAsync);
        group.MapGet("/orders", GetOrdersAsync);
        group.MapGet("/audit-logs", GetAuditLogsAsync);

        return routes;
    }

    private static async Task<IResult> CreateCourseVersionAsync(
        AdminCourseVersionRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = RequireStaff(httpContext);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CourseId) ||
            string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Intro))
        {
            return Results.BadRequest(new ApiError("invalid_course_version", "Course id, title, and intro are required."));
        }

        var version = await db.QuerySingleOrDefaultAsync(
            """
            insert into course_versions (course_id, version_no, status, title, intro, cover_text, teacher, created_by)
            select @course_id,
                   coalesce((select max(version_no) + 1 from course_versions where course_id = @course_id), 1),
                   'draft',
                   @title,
                   @intro,
                   @cover_text,
                   @teacher,
                   @created_by
            where exists (select 1 from courses where id = @course_id)
            returning id, course_id, version_no, status, title, created_at
            """,
            MapCourseVersion,
            [
                Pg.Param("course_id", request.CourseId.Trim()),
                Pg.Param("title", request.Title.Trim()),
                Pg.Param("intro", request.Intro.Trim()),
                Pg.Param("cover_text", string.IsNullOrWhiteSpace(request.CoverText) ? "course-cover" : request.CoverText.Trim()),
                Pg.Param("teacher", string.IsNullOrWhiteSpace(request.Teacher) ? user.DisplayName : request.Teacher.Trim()),
                Pg.Param("created_by", user.Id),
            ],
            cancellationToken);

        if (version is null)
        {
            return Results.NotFound(new ApiError("course_not_found", "Course was not found."));
        }

        await WriteAuditAsync(db, user.Id, "course_version.create", "course_version", version.Id.ToString(), cancellationToken);
        return Results.Created($"/api/admin/courses/versions/{version.Id}", version);
    }

    private static async Task<IResult> CreateCourseVersionStepAsync(
        Guid id,
        AdminCourseVersionStepRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = RequireStaff(httpContext);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return Results.BadRequest(new ApiError("invalid_course_step", "Step title and description are required."));
        }

        var step = await db.QuerySingleOrDefaultAsync(
            """
            insert into course_version_steps (
              version_id, section_id, source_step_id, title, description, media_asset_id, duration_seconds, sort_order
            )
            select @version_id, @section_id, @source_step_id, @title, @description, @media_asset_id, @duration_seconds, @sort_order
            where exists (
              select 1 from course_versions
              where id = @version_id and status in ('draft', 'submitted', 'approved')
            )
            returning id, version_id, source_step_id, title, description, duration_seconds, sort_order
            """,
            MapCourseVersionStep,
            [
                Pg.Param("version_id", id),
                Pg.Param("section_id", request.SectionId),
                Pg.Param("source_step_id", string.IsNullOrWhiteSpace(request.SourceStepId) ? null : request.SourceStepId.Trim()),
                Pg.Param("title", request.Title.Trim()),
                Pg.Param("description", request.Description.Trim()),
                Pg.Param("media_asset_id", request.MediaAssetId),
                Pg.Param("duration_seconds", Math.Max(0, request.DurationSeconds)),
                Pg.Param("sort_order", request.SortOrder),
            ],
            cancellationToken);

        if (step is null)
        {
            return Results.BadRequest(new ApiError("course_version_not_editable", "Course version was not found or is no longer editable."));
        }

        await WriteAuditAsync(db, user.Id, "course_version_step.create", "course_version", id.ToString(), cancellationToken);
        return Results.Created($"/api/admin/courses/versions/{id}/steps/{step.Id}", step);
    }

    private static Task<IResult> SubmitCourseVersionAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken) =>
        TransitionCourseVersionAsync(id, CourseVersionState.Submitted, "course_version.submit", httpContext, db, cancellationToken);

    private static Task<IResult> ApproveCourseVersionAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken) =>
        TransitionCourseVersionAsync(id, CourseVersionState.Approved, "course_version.approve", httpContext, db, cancellationToken);

    private static Task<IResult> ArchiveCourseVersionAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken) =>
        TransitionCourseVersionAsync(id, CourseVersionState.Archived, "course_version.archive", httpContext, db, cancellationToken);

    private static async Task<IResult> PublishCourseVersionAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = RequireStaff(httpContext);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var current = await LoadCourseVersionAsync(connection, transaction, id, cancellationToken);
        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.NotFound(new ApiError("course_version_not_found", "Course version was not found."));
        }

        if (!CourseVersionState.CanTransition(current.Status, CourseVersionState.Published))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.BadRequest(new ApiError("invalid_transition", $"Cannot publish course version from '{current.Status}'."));
        }

        await using (var command = new NpgsqlCommand(
            """
            update course_versions
            set status = 'archived', archived_at = now(), updated_at = now()
            where course_id = @course_id and status = 'published' and id <> @id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("course_id", current.CourseId));
            command.Parameters.Add(Pg.Param("id", id));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        AdminCourseVersionDto? published;
        await using (var command = new NpgsqlCommand(
            """
            update course_versions
            set status = 'published', published_at = now(), updated_at = now()
            where id = @id
            returning id, course_id, version_no, status, title, created_at
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", id));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            published = await reader.ReadAsync(cancellationToken) ? MapCourseVersion(reader) : null;
        }

        await using (var command = new NpgsqlCommand(
            """
            update courses c
            set published_version_id = @version_id,
                title = cv.title,
                intro = cv.intro,
                cover_text = cv.cover_text,
                teacher = cv.teacher,
                updated_at = now()
            from course_versions cv
            where c.id = @course_id and cv.id = @version_id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("version_id", id));
            command.Parameters.Add(Pg.Param("course_id", current.CourseId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteAuditAsync(connection, transaction, user.Id, "course_version.publish", "course_version", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return published is null ? Results.Problem("Failed to publish course version.") : Results.Ok(published);
    }

    private static async Task<IResult> TransitionCourseVersionAsync(
        Guid id,
        string nextStatus,
        string auditAction,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = RequireStaff(httpContext);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var current = await db.QuerySingleOrDefaultAsync(
            "select id, course_id, version_no, status, title, created_at from course_versions where id = @id",
            MapCourseVersion,
            [Pg.Param("id", id)],
            cancellationToken);

        if (current is null)
        {
            return Results.NotFound(new ApiError("course_version_not_found", "Course version was not found."));
        }

        if (!CourseVersionState.CanTransition(current.Status, nextStatus))
        {
            return Results.BadRequest(new ApiError("invalid_transition", $"Cannot transition course version from '{current.Status}' to '{nextStatus}'."));
        }

        var timestampColumn = nextStatus switch
        {
            CourseVersionState.Submitted => "submitted_at",
            CourseVersionState.Approved => "approved_at",
            CourseVersionState.Archived => "archived_at",
            _ => "updated_at",
        };

        var version = await db.QuerySingleOrDefaultAsync(
            $"""
            update course_versions
            set status = @status,
                {timestampColumn} = now(),
                updated_at = now()
            where id = @id
            returning id, course_id, version_no, status, title, created_at
            """,
            MapCourseVersion,
            [Pg.Param("id", id), Pg.Param("status", nextStatus)],
            cancellationToken);

        await WriteAuditAsync(db, user.Id, auditAction, "course_version", id.ToString(), cancellationToken);
        return version is null ? Results.Problem("Failed to update course version.") : Results.Ok(version);
    }

    private static async Task<IResult> GetModerationTasksAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        if (RequireStaff(httpContext) is null)
        {
            return Results.Unauthorized();
        }

        var tasks = await db.QueryAsync(
            """
            select id, target_type, target_id, status, reason, created_at, resolved_at
            from moderation_tasks
            order by case when status = 'open' then 0 else 1 end, created_at desc
            limit 100
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                TargetType = reader.GetString(reader.GetOrdinal("target_type")),
                TargetId = reader.GetString(reader.GetOrdinal("target_id")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                Reason = reader.GetString(reader.GetOrdinal("reason")),
                CreatedAt = reader.GetDateTimeOffset("created_at"),
                ResolvedAt = reader.IsDBNull(reader.GetOrdinal("resolved_at")) ? (DateTimeOffset?)null : reader.GetDateTimeOffset("resolved_at"),
            },
            cancellationToken: cancellationToken);

        return Results.Ok(tasks);
    }

    private static async Task<IResult> ResolveModerationTaskAsync(
        Guid id,
        AdminModerationDecisionRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = RequireStaff(httpContext);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var resolution = string.IsNullOrWhiteSpace(request.Resolution) ? "hide" : request.Resolution.Trim().ToLowerInvariant();
        if (resolution is not ("publish" or "hide" or "block" or "approve"))
        {
            return Results.BadRequest(new ApiError("invalid_resolution", "Resolution must be publish, approve, hide, or block."));
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var task = await LoadModerationTaskAsync(connection, transaction, id, cancellationToken);
        if (task is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.NotFound(new ApiError("moderation_task_not_found", "Moderation task was not found."));
        }

        if (task.TargetType == "community_post")
        {
            var postStatus = resolution is "publish" or "approve" ? "published" : "hidden";
            await ExecuteAsync(connection, transaction, "update community_posts set status = @status where id = @id", cancellationToken, Pg.Param("status", postStatus), Pg.Param("id", Guid.Parse(task.TargetId)));
        }
        else if (task.TargetType == "media_asset")
        {
            var mediaStatus = resolution is "publish" or "approve" ? "approved" : "blocked";
            await ExecuteAsync(connection, transaction, "update media_assets set status = @status, updated_at = now() where id = @id", cancellationToken, Pg.Param("status", mediaStatus), Pg.Param("id", Guid.Parse(task.TargetId)));
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            update moderation_tasks
            set status = 'resolved',
                resolved_by = @resolved_by,
                resolution = @resolution,
                resolved_at = now()
            where id = @id
            """,
            cancellationToken,
            Pg.Param("id", id),
            Pg.Param("resolved_by", user.Id),
            Pg.Param("resolution", resolution));

        await WriteAuditAsync(connection, transaction, user.Id, "moderation.resolve", task.TargetType, task.TargetId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { id, status = "resolved", resolution });
    }

    private static async Task<IResult> GetOrdersAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        if (RequireStaff(httpContext) is null)
        {
            return Results.Unauthorized();
        }

        var orders = await db.QueryAsync(
            """
            select o.id, o.order_no, o.status, o.total_cents, o.created_at, a.email
            from orders o
            join accounts a on a.id = o.account_id
            order by o.created_at desc
            limit 100
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                OrderNo = reader.GetString(reader.GetOrdinal("order_no")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                TotalCents = reader.GetInt32(reader.GetOrdinal("total_cents")),
                Total = ApiFormatting.Money(reader.GetInt32(reader.GetOrdinal("total_cents"))),
                CreatedAt = reader.GetDateTimeOffset("created_at"),
                AccountEmail = reader.GetString(reader.GetOrdinal("email")),
            },
            cancellationToken: cancellationToken);

        return Results.Ok(orders);
    }

    private static async Task<IResult> GetAuditLogsAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        if (RequireStaff(httpContext) is null)
        {
            return Results.Unauthorized();
        }

        var logs = await db.QueryAsync(
            """
            select id, action, target_type, target_id, created_at
            from audit_logs
            order by created_at desc
            limit 100
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Action = reader.GetString(reader.GetOrdinal("action")),
                TargetType = reader.GetNullableString("target_type"),
                TargetId = reader.GetNullableString("target_id"),
                CreatedAt = reader.GetDateTimeOffset("created_at"),
            },
            cancellationToken: cancellationToken);

        return Results.Ok(logs);
    }

    private static AuthenticatedUser? RequireStaff(HttpContext httpContext)
    {
        var user = httpContext.GetAuthenticatedUser();
        return user is not null && user.Role is "staff" or "admin" ? user : null;
    }

    private static AdminCourseVersionDto MapCourseVersion(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("course_id")),
            reader.GetInt32(reader.GetOrdinal("version_no")),
            reader.GetString(reader.GetOrdinal("status")),
            reader.GetString(reader.GetOrdinal("title")),
            reader.GetDateTimeOffset("created_at"));

    private static AdminCourseVersionStepDto MapCourseVersionStep(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetGuid(reader.GetOrdinal("version_id")),
            reader.GetNullableString("source_step_id"),
            reader.GetString(reader.GetOrdinal("title")),
            reader.GetString(reader.GetOrdinal("description")),
            reader.GetInt32(reader.GetOrdinal("duration_seconds")),
            reader.GetInt32(reader.GetOrdinal("sort_order")));

    private static async Task<AdminCourseVersionDto?> LoadCourseVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select id, course_id, version_no, status, title, created_at from course_versions where id = @id for update",
            connection,
            transaction);
        command.Parameters.Add(Pg.Param("id", id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapCourseVersion(reader) : null;
    }

    private static async Task<ModerationTask?> LoadModerationTaskAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "select id, target_type, target_id from moderation_tasks where id = @id for update",
            connection,
            transaction);
        command.Parameters.Add(Pg.Param("id", id));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ModerationTask(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("target_type")),
                reader.GetString(reader.GetOrdinal("target_id")))
            : null;
    }

    private static async Task WriteAuditAsync(NpgsqlDataSource db, Guid actorId, string action, string targetType, string targetId, CancellationToken cancellationToken) =>
        await db.ExecuteAsync(
            """
            insert into audit_logs (actor_account_id, action, target_type, target_id)
            values (@actor_account_id, @action, @target_type, @target_id)
            """,
            [Pg.Param("actor_account_id", actorId), Pg.Param("action", action), Pg.Param("target_type", targetType), Pg.Param("target_id", targetId)],
            cancellationToken);

    private static async Task WriteAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorId,
        string action,
        string targetType,
        string targetId,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            """
            insert into audit_logs (actor_account_id, action, target_type, target_id)
            values (@actor_account_id, @action, @target_type, @target_id)
            """,
            cancellationToken,
            Pg.Param("actor_account_id", actorId),
            Pg.Param("action", action),
            Pg.Param("target_type", targetType),
            Pg.Param("target_id", targetId));
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ModerationTask(Guid Id, string TargetType, string TargetId);
}
