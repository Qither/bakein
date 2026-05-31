using Bakein.Api.Application.Providers;
using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/users/me").WithTags("Me");

        group.MapGet("/profile", GetProfileAsync);
        group.MapGet("/cart", GetCartAsync);
        group.MapPut("/cart/items", UpsertCartItemAsync);
        group.MapPatch("/cart/items/{id:guid}", UpdateCartItemAsync);
        group.MapDelete("/cart/items/{id:guid}", DeleteCartItemAsync);
        group.MapPost("/cart/checkout", CheckoutAsync);
        group.MapGet("/orders", GetOrdersAsync);
        group.MapGet("/orders/{id:guid}", GetOrderAsync);
        group.MapGet("/progress", GetProgressAsync);
        group.MapPut("/progress", UpdateProgressAsync);

        return routes;
    }

    private static async Task<IResult> GetProfileAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await db.QuerySingleOrDefaultAsync(
            """
            select a.id, a.email, a.display_name, a.role, a.created_at,
                   coalesce(m.status, 'none') as membership_status,
                   coalesce(p.learning_days, 0) as learning_days,
                   coalesce(p.streak_days, 0) as streak_days,
                   (
                     select count(distinct oi.sku_id)::int
                     from orders o
                     join order_items oi on oi.order_id = o.id
                     where o.account_id = a.id and oi.item_type = 'course' and o.status <> 'cancelled'
                   ) as purchased_courses,
                   (
                     select count(*)::int
                     from learning_progress lp
                     where lp.account_id = a.id
                   ) as completed_steps,
                   (
                     select count(*)::int
                     from community_posts cp
                     where cp.account_id = a.id
                   ) as check_in_count
            from accounts a
            left join user_profiles p on p.account_id = a.id
            left join lateral (
              select status
              from memberships
              where account_id = a.id and ends_at > now()
              order by ends_at desc
              limit 1
            ) m on true
            where a.id = @account_id
            """,
            reader => new ProfileDto(
                AuthEndpoints.MapAccount(reader),
                reader.GetString(reader.GetOrdinal("membership_status")),
                reader.GetInt32(reader.GetOrdinal("learning_days")),
                reader.GetInt32(reader.GetOrdinal("streak_days")),
                reader.GetInt32(reader.GetOrdinal("purchased_courses")),
                reader.GetInt32(reader.GetOrdinal("completed_steps")),
                reader.GetInt32(reader.GetOrdinal("check_in_count"))),
            [Pg.Param("account_id", user.Id)],
            cancellationToken);

        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }

    private static async Task<IResult> GetCartAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        return user is null ? Results.Unauthorized() : Results.Ok(await LoadCartAsync(db, user.Id, cancellationToken));
    }

    private static async Task<IResult> UpsertCartItemAsync(
        UpsertCartItemRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var sku = await ResolveSkuAsync(db, request.ItemType, request.SkuId, cancellationToken);
        if (sku is null)
        {
            return Results.NotFound(new ApiError("sku_not_found", "Cart item target was not found."));
        }

        await db.ExecuteAsync(
            """
            insert into cart_items (account_id, item_type, sku_id, name, unit_price_cents, quantity, selected)
            values (@account_id, @item_type, @sku_id, @name, @unit_price_cents, @quantity, @selected)
            on conflict (account_id, item_type, sku_id) do update set
              name = excluded.name,
              unit_price_cents = excluded.unit_price_cents,
              quantity = excluded.quantity,
              selected = excluded.selected,
              updated_at = now()
            """,
            [
                Pg.Param("account_id", user.Id),
                Pg.Param("item_type", sku.ItemType),
                Pg.Param("sku_id", sku.SkuId),
                Pg.Param("name", sku.Name),
                Pg.Param("unit_price_cents", sku.PriceCents),
                Pg.Param("quantity", Math.Max(1, request.Quantity)),
                Pg.Param("selected", request.Selected),
            ],
            cancellationToken);

        return Results.Ok(await LoadCartAsync(db, user.Id, cancellationToken));
    }

    private static async Task<IResult> UpdateCartItemAsync(
        Guid id,
        UpdateCartItemRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var changed = await db.ExecuteAsync(
            """
            update cart_items
            set quantity = greatest(1, coalesce(@quantity::integer, quantity)),
                selected = coalesce(@selected::boolean, selected),
                updated_at = now()
            where id = @id and account_id = @account_id
            """,
            [
                Pg.Param("id", id),
                Pg.Param("account_id", user.Id),
                Pg.Param("quantity", request.Quantity),
                Pg.Param("selected", request.Selected),
            ],
            cancellationToken);

        return changed == 0
            ? Results.NotFound(new ApiError("cart_item_not_found", "Cart item was not found."))
            : Results.Ok(await LoadCartAsync(db, user.Id, cancellationToken));
    }

    private static async Task<IResult> DeleteCartItemAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        await db.ExecuteAsync(
            "delete from cart_items where id = @id and account_id = @account_id",
            [Pg.Param("id", id), Pg.Param("account_id", user.Id)],
            cancellationToken);

        return Results.Ok(await LoadCartAsync(db, user.Id, cancellationToken));
    }

    private static async Task<IResult> CheckoutAsync(
        HttpContext httpContext,
        NpgsqlDataSource db,
        IPaymentProvider paymentProvider,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var items = new List<CartItemDto>();
        await using (var command = CreateCommand(
            connection,
            transaction,
            """
            select id, item_type, sku_id, name, unit_price_cents, quantity, selected
            from cart_items
            where account_id = @account_id and selected = true
            order by created_at
            """,
            Pg.Param("account_id", user.Id)))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapCartItem(reader));
            }
        }

        if (items.Count == 0)
        {
            return Results.BadRequest(new ApiError("empty_cart", "No selected cart items to checkout."));
        }

        var orderId = Guid.NewGuid();
        var orderNo = $"BK{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        var totalCents = items.Sum(item => item.LineTotalCents);
        var createdAt = DateTimeOffset.UtcNow;
        var paymentIntentId = Guid.NewGuid();
        var providerIntent = await paymentProvider.CreatePaymentIntentAsync(
            new PaymentIntentCommand(paymentIntentId, orderId, user.Id, totalCents, "CNY"),
            cancellationToken);

        await using (var command = CreateCommand(
            connection,
            transaction,
            """
            insert into orders (id, account_id, order_no, status, total_cents, created_at)
            values (@id, @account_id, @order_no, 'pending_payment', @total_cents, @created_at)
            """,
            Pg.Param("id", orderId),
            Pg.Param("account_id", user.Id),
            Pg.Param("order_no", orderNo),
            Pg.Param("total_cents", totalCents),
            Pg.Param("created_at", createdAt)))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var orderItems = new List<OrderItemDto>();
        foreach (var item in items)
        {
            var orderItemId = Guid.NewGuid();
            await using var command = CreateCommand(
                connection,
                transaction,
                """
                insert into order_items (id, order_id, item_type, sku_id, name, unit_price_cents, quantity)
                values (@id, @order_id, @item_type, @sku_id, @name, @unit_price_cents, @quantity)
                """,
                Pg.Param("id", orderItemId),
                Pg.Param("order_id", orderId),
                Pg.Param("item_type", item.ItemType),
                Pg.Param("sku_id", item.SkuId),
                Pg.Param("name", item.Name),
                Pg.Param("unit_price_cents", item.UnitPriceCents),
                Pg.Param("quantity", item.Quantity));
            await command.ExecuteNonQueryAsync(cancellationToken);

            orderItems.Add(new OrderItemDto(orderItemId, item.ItemType, item.SkuId, item.Name, item.UnitPriceCents, item.Quantity, item.LineTotalCents));
        }

        await using (var command = CreateCommand(
            connection,
            transaction,
            """
            insert into payment_intents (
              id, order_id, account_id, provider, provider_intent_id, amount_cents, currency, status, client_secret, expires_at
            ) values (
              @id, @order_id, @account_id, @provider, @provider_intent_id, @amount_cents, 'CNY', 'requires_action', @client_secret, @expires_at
            )
            """,
            Pg.Param("id", paymentIntentId),
            Pg.Param("order_id", orderId),
            Pg.Param("account_id", user.Id),
            Pg.Param("provider", providerIntent.Provider),
            Pg.Param("provider_intent_id", providerIntent.ProviderIntentId),
            Pg.Param("amount_cents", totalCents),
            Pg.Param("client_secret", providerIntent.ClientSecret),
            Pg.Param("expires_at", providerIntent.ExpiresAt)))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = CreateCommand(
            connection,
            transaction,
            "delete from cart_items where account_id = @account_id and selected = true",
            Pg.Param("account_id", user.Id)))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var order = new OrderDto(orderId, orderNo, "pending_payment", totalCents, ApiFormatting.Money(totalCents), createdAt, orderItems);
        return Results.Created($"/api/users/me/orders/{order.Id}", order);
    }

    private static async Task<IResult> GetOrdersAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var rows = await db.QueryAsync(
            """
            select id, order_no, status, total_cents, created_at
            from orders
            where account_id = @account_id
            order by created_at desc
            """,
            reader => new OrderRow(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("order_no")),
                reader.GetString(reader.GetOrdinal("status")),
                reader.GetInt32(reader.GetOrdinal("total_cents")),
                reader.GetDateTimeOffset("created_at")),
            [Pg.Param("account_id", user.Id)],
            cancellationToken);

        var orders = new List<OrderDto>();
        foreach (var row in rows)
        {
            var items = await LoadOrderItemsAsync(db, row.Id, cancellationToken);
            orders.Add(new OrderDto(row.Id, row.OrderNo, row.Status, row.TotalCents, ApiFormatting.Money(row.TotalCents), row.CreatedAt, items));
        }

        return Results.Ok(orders);
    }

    private static async Task<IResult> GetOrderAsync(Guid id, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var row = await db.QuerySingleOrDefaultAsync(
            """
            select id, order_no, status, total_cents, created_at
            from orders
            where id = @id and account_id = @account_id
            """,
            reader => new OrderRow(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("order_no")),
                reader.GetString(reader.GetOrdinal("status")),
                reader.GetInt32(reader.GetOrdinal("total_cents")),
                reader.GetDateTimeOffset("created_at")),
            [Pg.Param("id", id), Pg.Param("account_id", user.Id)],
            cancellationToken);

        if (row is null)
        {
            return Results.NotFound(new ApiError("order_not_found", "Order was not found."));
        }

        var items = await LoadOrderItemsAsync(db, row.Id, cancellationToken);
        return Results.Ok(new OrderDto(row.Id, row.OrderNo, row.Status, row.TotalCents, ApiFormatting.Money(row.TotalCents), row.CreatedAt, items));
    }

    private static async Task<IResult> GetProgressAsync(string? courseId, HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var results = await LoadProgressAsync(db, user.Id, courseId, cancellationToken);
        return Results.Ok(results);
    }

    private static async Task<IResult> UpdateProgressAsync(
        ProgressUpdateRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var stepExists = await db.QuerySingleOrDefaultAsync(
            """
            with version_steps as (
              select coalesce(vs.source_step_id, vs.id::text) as step_id
              from courses c
              join course_version_steps vs on vs.version_id = c.published_version_id
              where c.id = @course_id
            ),
            visible_steps as (
              select step_id
              from version_steps
              union all
              select cs.id
              from course_steps cs
              where cs.course_id = @course_id
                and not exists (select 1 from version_steps)
            )
            select step_id
            from visible_steps
            where step_id = @step_id
            limit 1
            """,
            reader => reader.GetString(0),
            [Pg.Param("course_id", request.CourseId), Pg.Param("step_id", request.StepId)],
            cancellationToken);

        if (stepExists is null)
        {
            return Results.NotFound(new ApiError("step_not_found", "Course step was not found."));
        }

        if (request.Completed)
        {
            await db.ExecuteAsync(
                """
                insert into learning_progress (account_id, course_id, step_id)
                values (@account_id, @course_id, @step_id)
                on conflict (account_id, course_id, step_id) do update set completed_at = now()
                """,
                [Pg.Param("account_id", user.Id), Pg.Param("course_id", request.CourseId), Pg.Param("step_id", request.StepId)],
                cancellationToken);
        }
        else
        {
            await db.ExecuteAsync(
                "delete from learning_progress where account_id = @account_id and course_id = @course_id and step_id = @step_id",
                [Pg.Param("account_id", user.Id), Pg.Param("course_id", request.CourseId), Pg.Param("step_id", request.StepId)],
                cancellationToken);
        }

        return Results.Ok((await LoadProgressAsync(db, user.Id, request.CourseId, cancellationToken)).Single());
    }

    private static async Task<CartDto> LoadCartAsync(NpgsqlDataSource db, Guid accountId, CancellationToken cancellationToken)
    {
        var items = await db.QueryAsync(
            """
            select id, item_type, sku_id, name, unit_price_cents, quantity, selected
            from cart_items
            where account_id = @account_id
            order by created_at
            """,
            MapCartItem,
            [Pg.Param("account_id", accountId)],
            cancellationToken);

        var totalCents = items.Where(item => item.Selected).Sum(item => item.LineTotalCents);
        return new CartDto(items, totalCents, ApiFormatting.Money(totalCents));
    }

    private static CartItemDto MapCartItem(NpgsqlDataReader reader)
    {
        var unitPriceCents = reader.GetInt32(reader.GetOrdinal("unit_price_cents"));
        var quantity = reader.GetInt32(reader.GetOrdinal("quantity"));
        var lineTotalCents = unitPriceCents * quantity;

        return new CartItemDto(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("item_type")),
            reader.GetString(reader.GetOrdinal("sku_id")),
            reader.GetString(reader.GetOrdinal("name")),
            unitPriceCents,
            ApiFormatting.Money(unitPriceCents),
            quantity,
            reader.GetBoolean(reader.GetOrdinal("selected")),
            lineTotalCents,
            ApiFormatting.Money(lineTotalCents));
    }

    private static async Task<ResolvedSku?> ResolveSkuAsync(NpgsqlDataSource db, string itemType, string skuId, CancellationToken cancellationToken)
    {
        var normalizedType = NormalizeItemType(itemType);
        return normalizedType switch
        {
            "course" => await db.QuerySingleOrDefaultAsync(
                "select id, title || '课程' as name, price_cents from courses where id = @sku_id",
                reader => new ResolvedSku("course", reader.GetString(reader.GetOrdinal("id")), reader.GetString(reader.GetOrdinal("name")), reader.GetInt32(reader.GetOrdinal("price_cents"))),
                [Pg.Param("sku_id", skuId)],
                cancellationToken),
            "material_kit" => await db.QuerySingleOrDefaultAsync(
                "select id, name, price_cents from material_kits where id = @sku_id",
                reader => new ResolvedSku("material_kit", reader.GetString(reader.GetOrdinal("id")), reader.GetString(reader.GetOrdinal("name")), reader.GetInt32(reader.GetOrdinal("price_cents"))),
                [Pg.Param("sku_id", skuId)],
                cancellationToken),
            "membership_plan" => await db.QuerySingleOrDefaultAsync(
                "select id, name || '会员' as name, price_cents from membership_plans where id = @sku_id",
                reader => new ResolvedSku("membership_plan", reader.GetString(reader.GetOrdinal("id")), reader.GetString(reader.GetOrdinal("name")), reader.GetInt32(reader.GetOrdinal("price_cents"))),
                [Pg.Param("sku_id", skuId)],
                cancellationToken),
            _ => null,
        };
    }

    private static string NormalizeItemType(string itemType)
    {
        var normalized = itemType.Trim().ToLowerInvariant().Replace('-', '_');
        return normalized switch
        {
            "kit" or "material" or "materialkit" => "material_kit",
            "membership" or "plan" => "membership_plan",
            _ => normalized,
        };
    }

    private static async Task<IReadOnlyList<OrderItemDto>> LoadOrderItemsAsync(NpgsqlDataSource db, Guid orderId, CancellationToken cancellationToken) =>
        await db.QueryAsync(
            """
            select id, item_type, sku_id, name, unit_price_cents, quantity
            from order_items
            where order_id = @order_id
            order by id
            """,
            reader =>
            {
                var unitPriceCents = reader.GetInt32(reader.GetOrdinal("unit_price_cents"));
                var quantity = reader.GetInt32(reader.GetOrdinal("quantity"));
                return new OrderItemDto(
                    reader.GetGuid(reader.GetOrdinal("id")),
                    reader.GetString(reader.GetOrdinal("item_type")),
                    reader.GetString(reader.GetOrdinal("sku_id")),
                    reader.GetString(reader.GetOrdinal("name")),
                    unitPriceCents,
                    quantity,
                    unitPriceCents * quantity);
            },
            [Pg.Param("order_id", orderId)],
            cancellationToken);

    private static async Task<IReadOnlyList<LearningProgressDto>> LoadProgressAsync(
        NpgsqlDataSource db,
        Guid accountId,
        string? courseId,
        CancellationToken cancellationToken) =>
        await db.QueryAsync(
            """
            with course_scope as (
              select id, sort_order, published_version_id
              from courses
              where (@course_id::text is null or id = @course_id)
            ),
            visible_steps as (
              select c.id as course_id,
                     coalesce(vs.source_step_id, vs.id::text) as step_id,
                     vs.sort_order
              from course_scope c
              join course_version_steps vs on vs.version_id = c.published_version_id
              union all
              select c.id as course_id,
                     cs.id as step_id,
                     cs.sort_order
              from course_scope c
              join course_steps cs on cs.course_id = c.id
              where not exists (
                select 1
                from course_version_steps vs
                where vs.version_id = c.published_version_id
              )
            )
            select c.id as course_id,
                   coalesce(array_agg(lp.step_id order by vs.sort_order) filter (where lp.step_id is not null), array[]::text[]) as completed_step_ids,
                   count(vs.step_id)::int as total_steps
            from course_scope c
            join visible_steps vs on vs.course_id = c.id
            left join learning_progress lp
              on lp.course_id = vs.course_id
             and lp.step_id = vs.step_id
             and lp.account_id = @account_id
            group by c.id, c.sort_order
            order by c.sort_order
            """,
            reader =>
            {
                var completedStepIds = reader.GetStringArray("completed_step_ids");
                var totalSteps = reader.GetInt32(reader.GetOrdinal("total_steps"));
                return new LearningProgressDto(
                    reader.GetString(reader.GetOrdinal("course_id")),
                    completedStepIds,
                    completedStepIds.Length,
                    totalSteps);
            },
            [Pg.Param("account_id", accountId), Pg.Param("course_id", string.IsNullOrWhiteSpace(courseId) ? null : courseId)],
            cancellationToken);

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private sealed record ResolvedSku(string ItemType, string SkuId, string Name, int PriceCents);

    private sealed record OrderRow(Guid Id, string OrderNo, string Status, int TotalCents, DateTimeOffset CreatedAt);
}
