using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bakein.Api.Application.Providers;
using Bakein.Api.Domain;
using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class CommerceEndpoints
{
    public static RouteGroupBuilder MapCommerceEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/payments").WithTags("Payments");

        group.MapPost("/intents", CreatePaymentIntentAsync);
        group.MapPost("/callbacks/local", HandleLocalCallbackAsync);

        return routes;
    }

    private static async Task<IResult> CreatePaymentIntentAsync(
        PaymentIntentRequest request,
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

        var order = await db.QuerySingleOrDefaultAsync(
            """
            select id, account_id, total_cents
            from orders
            where id = @id and account_id = @account_id and status = 'pending_payment'
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                AccountId = reader.GetGuid(reader.GetOrdinal("account_id")),
                TotalCents = reader.GetInt32(reader.GetOrdinal("total_cents")),
            },
            [Pg.Param("id", request.OrderId), Pg.Param("account_id", user.Id)],
            cancellationToken);

        if (order is null)
        {
            return Results.NotFound(new ApiError("order_not_found", "Pending order was not found."));
        }

        var existing = await LoadPaymentIntentAsync(db, order.Id, user.Id, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(existing);
        }

        var paymentIntentId = Guid.NewGuid();
        var providerIntent = await paymentProvider.CreatePaymentIntentAsync(
            new PaymentIntentCommand(paymentIntentId, order.Id, user.Id, order.TotalCents, "CNY"),
            cancellationToken);

        var intent = await db.QuerySingleOrDefaultAsync(
            """
            insert into payment_intents (
              id, order_id, account_id, provider, provider_intent_id, amount_cents, currency, status, client_secret, expires_at
            ) values (
              @id, @order_id, @account_id, @provider, @provider_intent_id, @amount_cents, 'CNY', 'requires_action', @client_secret, @expires_at
            )
            returning id, order_id, provider, provider_intent_id, amount_cents, currency, status, client_secret, expires_at
            """,
            MapPaymentIntent,
            [
                Pg.Param("id", paymentIntentId),
                Pg.Param("order_id", order.Id),
                Pg.Param("account_id", user.Id),
                Pg.Param("provider", providerIntent.Provider),
                Pg.Param("provider_intent_id", providerIntent.ProviderIntentId),
                Pg.Param("amount_cents", order.TotalCents),
                Pg.Param("client_secret", providerIntent.ClientSecret),
                Pg.Param("expires_at", providerIntent.ExpiresAt),
            ],
            cancellationToken);

        return intent is null ? Results.Problem("Failed to create payment intent.") : Results.Created($"/api/payments/intents/{intent.Id}", intent);
    }

    private static async Task<IResult> HandleLocalCallbackAsync(
        LocalPaymentCallbackRequest request,
        IConfiguration configuration,
        IHostEnvironment environment,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var configuredProvider = configuration["Payment:Provider"] ?? "local";
        if (!environment.IsDevelopment() && !string.Equals(configuredProvider, "local", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new ApiError("callback_signature_required", "Local callback bypass is disabled outside local provider mode."), statusCode: StatusCodes.Status403Forbidden);
        }

        var intent = await db.QuerySingleOrDefaultAsync(
            """
            select id, order_id, account_id, provider, provider_intent_id, amount_cents, status
            from payment_intents
            where id = @id
            """,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                OrderId = reader.GetGuid(reader.GetOrdinal("order_id")),
                AccountId = reader.GetGuid(reader.GetOrdinal("account_id")),
                Provider = reader.GetString(reader.GetOrdinal("provider")),
                ProviderIntentId = reader.GetString(reader.GetOrdinal("provider_intent_id")),
                AmountCents = reader.GetInt32(reader.GetOrdinal("amount_cents")),
                Status = reader.GetString(reader.GetOrdinal("status")),
            },
            [Pg.Param("id", request.PaymentIntentId)],
            cancellationToken);

        if (intent is null)
        {
            return Results.NotFound(new ApiError("payment_intent_not_found", "Payment intent was not found."));
        }

        var normalizedStatus = request.Status.Trim().ToLowerInvariant();
        if (normalizedStatus is not (PaymentIntentState.Succeeded or PaymentIntentState.Failed or PaymentIntentState.Cancelled))
        {
            return Results.BadRequest(new ApiError("invalid_payment_status", "Payment status must be succeeded, failed, or cancelled."));
        }

        var providerEventId = request.ProviderEventId ?? $"{intent.ProviderIntentId}:{normalizedStatus}";
        var payload = JsonSerializer.Serialize(request);
        var eventHash = ComputeHash($"{intent.Provider}:{providerEventId}:{normalizedStatus}:{intent.Id}");

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        Guid? callbackId;
        await using (var command = new NpgsqlCommand(
            """
            insert into provider_callback_logs (
              provider, event_type, provider_event_id, event_hash, signature_status, payload
            ) values (
              @provider, 'payment.status', @provider_event_id, @event_hash, 'local_bypass', @payload::jsonb
            )
            on conflict do nothing
            returning id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("provider", intent.Provider));
            command.Parameters.Add(Pg.Param("provider_event_id", providerEventId));
            command.Parameters.Add(Pg.Param("event_hash", eventHash));
            command.Parameters.Add(Pg.Param("payload", payload));
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            callbackId = scalar is Guid id ? id : null;
        }

        if (callbackId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new PaymentCallbackResultDto(intent.Id, intent.OrderId, intent.Status, await LoadOrderStatusAsync(db, intent.OrderId, cancellationToken), Duplicate: true));
        }

        await using (var command = new NpgsqlCommand(
            """
            insert into payment_events (payment_intent_id, provider, provider_event_id, event_type, payload)
            values (@payment_intent_id, @provider, @provider_event_id, 'payment.status', @payload::jsonb)
            on conflict do nothing
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("payment_intent_id", intent.Id));
            command.Parameters.Add(Pg.Param("provider", intent.Provider));
            command.Parameters.Add(Pg.Param("provider_event_id", providerEventId));
            command.Parameters.Add(Pg.Param("payload", payload));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!PaymentIntentState.CanApplyCallback(intent.Status, normalizedStatus))
        {
            await using var staleCommand = new NpgsqlCommand(
                "update provider_callback_logs set processing_outcome = 'ignored_stale', processed_at = now() where id = @id",
                connection,
                transaction);
            staleCommand.Parameters.Add(Pg.Param("id", callbackId.Value));
            await staleCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new PaymentCallbackResultDto(intent.Id, intent.OrderId, intent.Status, await LoadOrderStatusAsync(db, intent.OrderId, cancellationToken), Duplicate: false));
        }

        var orderStatus = normalizedStatus == "succeeded" ? "paid" : "pending_payment";

        await using (var command = new NpgsqlCommand(
            """
            update payment_intents
            set status = @status,
                updated_at = now()
            where id = @id
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", intent.Id));
            command.Parameters.Add(Pg.Param("status", normalizedStatus));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (normalizedStatus == "succeeded")
        {
            await using (var command = new NpgsqlCommand(
                "update orders set status = 'paid' where id = @id and status = 'pending_payment'",
                connection,
                transaction))
            {
                command.Parameters.Add(Pg.Param("id", intent.OrderId));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = new NpgsqlCommand(
                """
                insert into course_entitlements (account_id, course_id, source_type, source_id)
                select o.account_id, oi.sku_id, 'order', o.id::text
                from orders o
                join order_items oi on oi.order_id = o.id
                where o.id = @order_id and oi.item_type = 'course'
                on conflict do nothing
                """,
                connection,
                transaction))
            {
                command.Parameters.Add(Pg.Param("order_id", intent.OrderId));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = new NpgsqlCommand(
                """
                insert into outbox_events (aggregate_type, aggregate_id, event_type, payload)
                values ('order', @aggregate_id, 'order.paid', @payload::jsonb)
                """,
                connection,
                transaction))
            {
                command.Parameters.Add(Pg.Param("aggregate_id", intent.OrderId.ToString()));
                command.Parameters.Add(Pg.Param("payload", payload));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var command = new NpgsqlCommand(
            "update provider_callback_logs set processing_outcome = 'processed', processed_at = now() where id = @id",
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", callbackId.Value));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new PaymentCallbackResultDto(intent.Id, intent.OrderId, normalizedStatus, orderStatus, Duplicate: false));
    }

    private static async Task<PaymentIntentDto?> LoadPaymentIntentAsync(NpgsqlDataSource db, Guid orderId, Guid accountId, CancellationToken cancellationToken) =>
        await db.QuerySingleOrDefaultAsync(
            """
            select id, order_id, provider, provider_intent_id, amount_cents, currency, status, client_secret, expires_at
            from payment_intents
            where order_id = @order_id and account_id = @account_id
            order by created_at desc
            limit 1
            """,
            MapPaymentIntent,
            [Pg.Param("order_id", orderId), Pg.Param("account_id", accountId)],
            cancellationToken);

    private static async Task<string> LoadOrderStatusAsync(NpgsqlDataSource db, Guid orderId, CancellationToken cancellationToken) =>
        await db.QuerySingleOrDefaultAsync(
            "select status from orders where id = @id",
            reader => reader.GetString(0),
            [Pg.Param("id", orderId)],
            cancellationToken) ?? "unknown";

    private static PaymentIntentDto MapPaymentIntent(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetGuid(reader.GetOrdinal("order_id")),
            reader.GetString(reader.GetOrdinal("provider")),
            reader.GetString(reader.GetOrdinal("provider_intent_id")),
            reader.GetInt32(reader.GetOrdinal("amount_cents")),
            reader.GetString(reader.GetOrdinal("currency")),
            reader.GetString(reader.GetOrdinal("status")),
            reader.GetString(reader.GetOrdinal("client_secret")),
            reader.GetDateTimeOffset("expires_at"));

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
