using Bakein.Api.Infrastructure;
using Npgsql;

namespace Bakein.Api.Security;

public static class SessionAuthenticationMiddleware
{
    public static IApplicationBuilder UseSessionAuthentication(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var token = ReadBearerToken(context.Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                var dataSource = context.RequestServices.GetRequiredService<NpgsqlDataSource>();
                var tokenHash = SessionToken.Hash(token);
                var user = await dataSource.QuerySingleOrDefaultAsync(
                    """
                    select a.id, a.email, a.display_name, a.role
                    from user_sessions s
                    join accounts a on a.id = s.account_id
                    where s.token_hash = @token_hash
                      and s.revoked_at is null
                      and s.expires_at > now()
                    """,
                    reader => new AuthenticatedUser(
                        reader.GetGuid(reader.GetOrdinal("id")),
                        reader.GetString(reader.GetOrdinal("email")),
                        reader.GetString(reader.GetOrdinal("display_name")),
                        reader.GetString(reader.GetOrdinal("role"))),
                    [Pg.Param("token_hash", tokenHash)]);

                if (user is not null)
                {
                    context.SetAuthenticatedUser(user);
                }
            }

            await next(context);
        });

    public static string? ReadBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}
