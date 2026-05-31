using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", MeAsync);
        group.MapPost("/logout", LogoutAsync);

        return group;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var displayName = request.DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return Results.BadRequest(new ApiError("invalid_email", "Email is required."));
        }

        if (request.Password.Length < 8)
        {
            return Results.BadRequest(new ApiError("weak_password", "Password must be at least 8 characters."));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Results.BadRequest(new ApiError("invalid_display_name", "Display name is required."));
        }

        var existingAccountId = await db.QuerySingleOrDefaultAsync(
            "select id from accounts where email = @email",
            reader => reader.GetGuid(0),
            [Pg.Param("email", email)],
            cancellationToken);

        if (existingAccountId != Guid.Empty)
        {
            return Results.Conflict(new ApiError("email_exists", "An account with this email already exists."));
        }

        var account = await db.QuerySingleOrDefaultAsync(
            """
            insert into accounts (email, password_hash, display_name, avatar_text, role)
            values (@email, @password_hash, @display_name, @avatar_text, 'learner')
            returning id, email, display_name, role, created_at
            """,
            MapAccount,
            [
                Pg.Param("email", email),
                Pg.Param("password_hash", PasswordHasher.Hash(request.Password)),
                Pg.Param("display_name", displayName),
                Pg.Param("avatar_text", $"{displayName}头像"),
            ],
            cancellationToken);

        if (account is null)
        {
            return Results.Problem("Failed to create account.");
        }

        await db.ExecuteAsync(
            "insert into user_profiles (account_id) values (@account_id) on conflict do nothing",
            [Pg.Param("account_id", account.Id)],
            cancellationToken);

        return Results.Created($"/api/auth/me", await CreateSessionAsync(db, account, cancellationToken));
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var account = await db.QuerySingleOrDefaultAsync(
            """
            select id, email, display_name, role, created_at, password_hash
            from accounts
            where email = @email
            """,
            reader => new AccountWithPassword(MapAccount(reader), reader.GetString(reader.GetOrdinal("password_hash"))),
            [Pg.Param("email", NormalizeEmail(request.Email))],
            cancellationToken);

        if (account is null || !PasswordHasher.Verify(request.Password, account.PasswordHash))
        {
            return Results.Json(new ApiError("invalid_credentials", "Email or password is incorrect."), statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(await CreateSessionAsync(db, account.Account, cancellationToken));
    }

    private static async Task<IResult> MeAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var account = await db.QuerySingleOrDefaultAsync(
            """
            select id, email, display_name, role, created_at
            from accounts
            where id = @id
            """,
            MapAccount,
            [Pg.Param("id", user.Id)],
            cancellationToken);

        return account is null ? Results.Unauthorized() : Results.Ok(account);
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var token = SessionAuthenticationMiddleware.ReadBearerToken(httpContext.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.Unauthorized();
        }

        await db.ExecuteAsync(
            "update user_sessions set revoked_at = now() where token_hash = @token_hash and revoked_at is null",
            [Pg.Param("token_hash", SessionToken.Hash(token))],
            cancellationToken);

        return Results.NoContent();
    }

    internal static AccountDto MapAccount(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("email")),
            reader.GetString(reader.GetOrdinal("display_name")),
            reader.GetString(reader.GetOrdinal("role")),
            reader.GetDateTimeOffset("created_at"));

    private static async Task<AuthResponse> CreateSessionAsync(NpgsqlDataSource db, AccountDto account, CancellationToken cancellationToken)
    {
        var token = SessionToken.Create();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        await db.ExecuteAsync(
            """
            insert into user_sessions (account_id, token_hash, expires_at)
            values (@account_id, @token_hash, @expires_at)
            """,
            [
                Pg.Param("account_id", account.Id),
                Pg.Param("token_hash", SessionToken.Hash(token)),
                Pg.Param("expires_at", expiresAt),
            ],
            cancellationToken);

        return new AuthResponse(token, expiresAt, account);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private sealed record AccountWithPassword(AccountDto Account, string PasswordHash);
}
