using Npgsql;

namespace Bakein.Api.Infrastructure;

public static class Pg
{
    public static NpgsqlParameter Param(string name, object? value) => new(name, value ?? DBNull.Value);

    public static async Task<int> ExecuteAsync(
        this NpgsqlDataSource dataSource,
        string sql,
        IEnumerable<NpgsqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(sql);
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<List<T>> QueryAsync<T>(
        this NpgsqlDataSource dataSource,
        string sql,
        Func<NpgsqlDataReader, T> map,
        IEnumerable<NpgsqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(sql);
        AddParameters(command, parameters);

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(map(reader));
        }

        return results;
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(
        this NpgsqlDataSource dataSource,
        string sql,
        Func<NpgsqlDataReader, T> map,
        IEnumerable<NpgsqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(sql);
        AddParameters(command, parameters);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return default;
        }

        return map(reader);
    }

    public static string? GetNullableString(this NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static DateTimeOffset GetDateTimeOffset(this NpgsqlDataReader reader, string name) =>
        reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));

    public static string[] GetStringArray(this NpgsqlDataReader reader, string name) =>
        reader.GetFieldValue<string[]>(reader.GetOrdinal(name));

    private static void AddParameters(NpgsqlCommand command, IEnumerable<NpgsqlParameter>? parameters)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }
    }
}
