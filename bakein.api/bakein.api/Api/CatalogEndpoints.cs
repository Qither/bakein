using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder routes)
    {
        routes.MapGet("/catalog/categories", GetCategoriesAsync).WithTags("Catalog");
        routes.MapGet("/catalog/home", GetHomeFeedAsync).WithTags("Catalog");

        routes.MapGet("/courses", GetCoursesAsync).WithTags("Courses");
        routes.MapGet("/courses/{id}", GetCourseDetailAsync).WithTags("Courses");
        routes.MapGet("/courses/{id}/steps", GetCourseStepsAsync).WithTags("Courses");

        routes.MapGet("/membership/plans", GetMembershipPlansAsync).WithTags("Membership");

        routes.MapGet("/community/posts", GetCommunityPostsAsync).WithTags("Community");
        routes.MapPost("/community/posts", CreateCommunityPostAsync).WithTags("Community");

        return routes;
    }

    private static async Task<IResult> GetCategoriesAsync(NpgsqlDataSource db, CancellationToken cancellationToken) =>
        Results.Ok(await db.QueryAsync(
            """
            select id, name, sort_order
            from categories
            order by sort_order
            """,
            reader => new CategoryDto(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("name")),
                reader.GetInt32(reader.GetOrdinal("sort_order"))),
            cancellationToken: cancellationToken));

    private static async Task<IResult> GetHomeFeedAsync(NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var categories = await db.QueryAsync(
            "select id, name, sort_order from categories order by sort_order",
            reader => new CategoryDto(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("name")),
                reader.GetInt32(reader.GetOrdinal("sort_order"))),
            cancellationToken: cancellationToken);

        var beginnerCourses = await LoadCourseCardsAsync(db, "where c.beginner_rank is not null", "order by c.beginner_rank", cancellationToken: cancellationToken);
        var popularCourses = await LoadCourseCardsAsync(db, "where c.popular_rank is not null", "order by c.popular_rank", cancellationToken: cancellationToken);

        return Results.Ok(new HomeFeedDto(categories, beginnerCourses, popularCourses));
    }

    private static async Task<IResult> GetCoursesAsync(
        string? category,
        bool? memberFree,
        string? search,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var filters = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        if (!string.IsNullOrWhiteSpace(category))
        {
            filters.Add("(cat.id = @category or cat.name = @category)");
            parameters.Add(Pg.Param("category", category.Trim()));
        }

        if (memberFree is not null)
        {
            filters.Add("c.member_free = @member_free");
            parameters.Add(Pg.Param("member_free", memberFree.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add("(coalesce(cv.title, c.title) ilike @search or coalesce(cv.teacher, c.teacher) ilike @search or coalesce(cv.intro, c.intro) ilike @search)");
            parameters.Add(Pg.Param("search", $"%{search.Trim()}%"));
        }

        var whereClause = filters.Count == 0 ? "" : $"where {string.Join(" and ", filters)}";
        return Results.Ok(await LoadCourseCardsAsync(db, whereClause, "order by c.sort_order", parameters, cancellationToken));
    }

    private static async Task<IResult> GetCourseDetailAsync(string id, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var courses = await LoadCourseCardsAsync(
            db,
            "where c.id = @id",
            "",
            [Pg.Param("id", id)],
            cancellationToken);
        var course = courses.SingleOrDefault();
        if (course is null)
        {
            return Results.NotFound(new ApiError("course_not_found", "Course was not found."));
        }

        var steps = await LoadStepsAsync(db, id, cancellationToken);
        var reviews = await db.QueryAsync(
            """
            select id, author_name, content, rating, created_at
            from course_reviews
            where course_id = @course_id
            order by created_at desc
            """,
            reader => new CourseReviewDto(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("author_name")),
                reader.GetString(reader.GetOrdinal("content")),
                reader.GetDecimal(reader.GetOrdinal("rating")),
                reader.GetDateTimeOffset("created_at")),
            [Pg.Param("course_id", id)],
            cancellationToken);

        var materialKit = await db.QuerySingleOrDefaultAsync(
            """
            select id, course_id, name, description, price_cents
            from material_kits
            where course_id = @course_id
            order by id
            limit 1
            """,
            MapMaterialKit,
            [Pg.Param("course_id", id)],
            cancellationToken);

        return Results.Ok(new CourseDetailDto(course, steps, reviews, materialKit));
    }

    private static async Task<IResult> GetCourseStepsAsync(string id, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var courseExists = await db.QuerySingleOrDefaultAsync(
            "select id from courses where id = @id",
            reader => reader.GetString(0),
            [Pg.Param("id", id)],
            cancellationToken);

        return courseExists is null
            ? Results.NotFound(new ApiError("course_not_found", "Course was not found."))
            : Results.Ok(await LoadStepsAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> GetMembershipPlansAsync(NpgsqlDataSource db, CancellationToken cancellationToken) =>
        Results.Ok(await db.QueryAsync(
            """
            select id, name, price_cents, billing_period, description, sort_order
            from membership_plans
            order by sort_order
            """,
            reader => new MembershipPlanDto(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("name")),
                reader.GetInt32(reader.GetOrdinal("price_cents")),
                ApiFormatting.Money(reader.GetInt32(reader.GetOrdinal("price_cents"))),
                reader.GetString(reader.GetOrdinal("billing_period")),
                reader.GetString(reader.GetOrdinal("description")),
                reader.GetInt32(reader.GetOrdinal("sort_order"))),
            cancellationToken: cancellationToken));

    private static async Task<IResult> GetCommunityPostsAsync(NpgsqlDataSource db, CancellationToken cancellationToken) =>
        Results.Ok(await db.QueryAsync(
            """
            select p.id, p.author_name, p.course_id, c.title as course_title, p.text, p.image_text,
                   p.likes_count, p.comments_count, p.created_at
            from community_posts p
            left join courses c on c.id = p.course_id
            where p.status = 'published'
            order by p.created_at desc
            limit 50
            """,
            MapCommunityPost,
            cancellationToken: cancellationToken));

    private static async Task<IResult> CreateCommunityPostAsync(
        CreateCommunityPostRequest request,
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

        var post = await db.QuerySingleOrDefaultAsync(
            """
            insert into community_posts (account_id, author_name, course_id, text, image_text)
            values (@account_id, @author_name, @course_id, @text, @image_text)
            returning id, author_name, course_id, null::text as course_title, text, image_text, likes_count, comments_count, created_at
            """,
            MapCommunityPost,
            [
                Pg.Param("account_id", user.Id),
                Pg.Param("author_name", user.DisplayName),
                Pg.Param("course_id", string.IsNullOrWhiteSpace(request.CourseId) ? null : request.CourseId),
                Pg.Param("text", request.Text.Trim()),
                Pg.Param("image_text", string.IsNullOrWhiteSpace(request.ImageText) ? "作品照片" : request.ImageText.Trim()),
            ],
            cancellationToken);

        return post is null
            ? Results.Problem("Failed to create community post.")
            : Results.Created($"/api/community/posts/{post.Id}", post);
    }

    internal static async Task<IReadOnlyList<CourseStepDto>> LoadStepsAsync(NpgsqlDataSource db, string courseId, CancellationToken cancellationToken) =>
        await db.QueryAsync(
            """
            with version_steps as (
              select coalesce(vs.source_step_id, vs.id::text) as id,
                     vs.title,
                     vs.description,
                     vs.duration_seconds,
                     vs.sort_order
              from courses c
              join course_version_steps vs on vs.version_id = c.published_version_id
              where c.id = @course_id
            )
            select id, title, description, duration_seconds, sort_order
            from version_steps
            union all
            select id, title, description, duration_seconds, sort_order
            from course_steps
            where course_id = @course_id
              and not exists (select 1 from version_steps)
            order by sort_order
            """,
            reader => new CourseStepDto(
                reader.GetString(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("title")),
                reader.GetString(reader.GetOrdinal("description")),
                reader.GetInt32(reader.GetOrdinal("duration_seconds")),
                ApiFormatting.StepTime(reader.GetInt32(reader.GetOrdinal("duration_seconds"))),
                reader.GetInt32(reader.GetOrdinal("sort_order"))),
            [Pg.Param("course_id", courseId)],
            cancellationToken);

    internal static MaterialKitDto MapMaterialKit(NpgsqlDataReader reader)
    {
        var priceCents = reader.GetInt32(reader.GetOrdinal("price_cents"));
        return new MaterialKitDto(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("course_id")),
            reader.GetString(reader.GetOrdinal("name")),
            reader.GetString(reader.GetOrdinal("description")),
            priceCents,
            ApiFormatting.Money(priceCents));
    }

    internal static CommunityPostDto MapCommunityPost(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("author_name")),
            reader.GetNullableString("course_id"),
            reader.GetNullableString("course_title"),
            reader.GetString(reader.GetOrdinal("text")),
            reader.GetString(reader.GetOrdinal("image_text")),
            reader.GetInt32(reader.GetOrdinal("likes_count")),
            reader.GetInt32(reader.GetOrdinal("comments_count")),
            reader.GetDateTimeOffset("created_at"));

    private static async Task<IReadOnlyList<CourseCardDto>> LoadCourseCardsAsync(
        NpgsqlDataSource db,
        string whereClause,
        string orderClause,
        IEnumerable<NpgsqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return await db.QueryAsync(
            $$"""
            select c.id,
                   coalesce(cv.title, c.title) as title,
                   cat.name as category,
                   coalesce(cv.cover_text, c.cover_text) as cover_text,
                   c.duration_minutes,
                   c.level,
                   c.price_cents,
                   c.member_free,
                   coalesce(cv.teacher, c.teacher) as teacher,
                   c.rating,
                   c.student_count,
                   coalesce(cv.intro, c.intro) as intro,
                   coalesce(array_remove(array_agg(t.tag order by t.sort_order), null), array[]::text[]) as tags
            from courses c
            join categories cat on cat.id = c.category_id
            left join course_versions cv on cv.id = c.published_version_id and cv.status = 'published'
            left join course_tags t on t.course_id = c.id
            {{whereClause}}
            group by c.id, cat.name, cv.title, cv.cover_text, cv.teacher, cv.intro
            {{orderClause}}
            """,
            MapCourseCard,
            parameters,
            cancellationToken);
    }

    private static CourseCardDto MapCourseCard(NpgsqlDataReader reader)
    {
        var durationMinutes = reader.GetInt32(reader.GetOrdinal("duration_minutes"));
        var priceCents = reader.GetInt32(reader.GetOrdinal("price_cents"));
        var memberFree = reader.GetBoolean(reader.GetOrdinal("member_free"));
        var rating = reader.GetDecimal(reader.GetOrdinal("rating"));
        var studentCount = reader.GetInt32(reader.GetOrdinal("student_count"));

        return new CourseCardDto(
            reader.GetString(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("title")),
            reader.GetString(reader.GetOrdinal("category")),
            reader.GetString(reader.GetOrdinal("cover_text")),
            durationMinutes,
            ApiFormatting.Duration(durationMinutes),
            reader.GetString(reader.GetOrdinal("level")),
            priceCents,
            ApiFormatting.CoursePrice(priceCents, memberFree),
            memberFree,
            reader.GetString(reader.GetOrdinal("teacher")),
            rating,
            $"{rating:0.0}",
            studentCount,
            ApiFormatting.StudentLabel(studentCount),
            reader.GetStringArray("tags"),
            reader.GetString(reader.GetOrdinal("intro")));
    }
}
