using Bakein.Api.Infrastructure;
using Bakein.Api.Infrastructure.Providers;
using Npgsql;

namespace Bakein.Api.Api;

public static class OperationsEndpoints
{
    public static RouteGroupBuilder MapOperationsEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/operations").WithTags("Operations");

        group.MapGet("/readiness", GetReadinessAsync);
        group.MapGet("/provider-diagnostics", GetProviderDiagnostics);

        return routes;
    }

    private static async Task<IResult> GetReadinessAsync(NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var migrationCount = await db.QuerySingleOrDefaultAsync(
            "select count(*)::int from schema_migrations",
            reader => reader.GetInt32(0),
            cancellationToken: cancellationToken);
        var pendingOutbox = await db.QuerySingleOrDefaultAsync(
            "select count(*)::int from outbox_events where processed_at is null and failed_at is null",
            reader => reader.GetInt32(0),
            cancellationToken: cancellationToken);

        return Results.Ok(new
        {
            status = "ready",
            database = "ok",
            appliedMigrations = migrationCount,
            pendingOutbox,
            utc = DateTimeOffset.UtcNow,
        });
    }

    private static IResult GetProviderDiagnostics(ProviderRuntimeMode providerMode) =>
        Results.Ok(new
        {
            media = providerMode.MediaProvider,
            payment = providerMode.PaymentProvider,
            providerImplementation = "local_mock",
            tencentVodConfigured = providerMode.TencentVodConfigured,
        });
}
