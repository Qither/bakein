using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Bakein.Api.Infrastructure.Postgres;

public sealed class PostgresMigrationRunner(NpgsqlDataSource dataSource, ILogger<PostgresMigrationRunner> logger)
{
    private const string EnsureSchemaMigrationsSql =
        """
        create table if not exists schema_migrations (
          id text primary key,
          applied_at timestamptz not null default now(),
          checksum text not null
        );
        """;

    public async Task ApplyAsync(IReadOnlyList<PostgresMigration> migrations, CancellationToken cancellationToken = default)
    {
        if (migrations.Count == 0)
        {
            return;
        }

        var duplicate = migrations
            .GroupBy(migration => migration.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate Postgres migration id '{duplicate.Key}'.");
        }

        await dataSource.ExecuteAsync(EnsureSchemaMigrationsSql, cancellationToken: cancellationToken);

        foreach (var migration in migrations.OrderBy(migration => migration.Id, StringComparer.Ordinal))
        {
            await ApplyMigrationAsync(migration, cancellationToken);
        }
    }

    private async Task ApplyMigrationAsync(PostgresMigration migration, CancellationToken cancellationToken)
    {
        var checksum = ComputeChecksum(migration.Sql);
        var appliedChecksum = await dataSource.QuerySingleOrDefaultAsync(
            "select checksum from schema_migrations where id = @id",
            reader => reader.GetString(0),
            [Pg.Param("id", migration.Id)],
            cancellationToken);

        if (appliedChecksum == checksum)
        {
            logger.LogDebug("Skipping already-applied Postgres migration {MigrationId}", migration.Id);
            return;
        }

        if (appliedChecksum is not null)
        {
            throw new InvalidOperationException(
                $"Postgres migration '{migration.Id}' checksum drift detected. " +
                "Create a new migration instead of editing an applied migration.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand(migration.Sql, connection, transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            insert into schema_migrations (id, checksum)
            values (@id, @checksum)
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("id", migration.Id));
            command.Parameters.Add(Pg.Param("checksum", checksum));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Applied Postgres migration {MigrationId}", migration.Id);
    }

    private static string ComputeChecksum(string sql)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
