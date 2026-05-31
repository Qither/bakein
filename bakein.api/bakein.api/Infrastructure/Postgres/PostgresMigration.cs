namespace Bakein.Api.Infrastructure.Postgres;

public sealed record PostgresMigration(string Id, string Sql);
