using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bakein.Api.Application.Providers;
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
        group.MapPost("/wechat/register", RegisterWithWechatAsync);
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

    private static async Task<IResult> RegisterWithWechatAsync(
        WechatRegisterRequest request,
        IWechatIdentityProvider wechatIdentityProvider,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        if (request.Profile is null)
        {
            return Results.BadRequest(new ApiError("wechat_profile_required", "WeChat profile is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.BadRequest(new ApiError("wechat_code_required", "WeChat login code is required."));
        }

        var displayName = request.Profile.NickName.Trim();
        var avatarUrl = string.IsNullOrWhiteSpace(request.Profile.AvatarUrl) ? null : request.Profile.AvatarUrl.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Results.BadRequest(new ApiError("invalid_wechat_nickname", "WeChat nickname is required."));
        }

        if (displayName.Length > 64)
        {
            return Results.BadRequest(new ApiError("invalid_wechat_nickname", "WeChat nickname is too long."));
        }

        if (avatarUrl?.Length > 2048)
        {
            return Results.BadRequest(new ApiError("invalid_wechat_avatar", "WeChat avatar URL is too long."));
        }

        WechatSession wechatSession;
        try
        {
            wechatSession = await wechatIdentityProvider.ExchangeCodeAsync(request.Code, cancellationToken);
        }
        catch (ExternalIdentityProviderException ex)
        {
            var statusCode = ex.Code == "wechat_not_configured"
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status400BadRequest;
            return Results.Json(new ApiError(ex.Code, ex.Message), statusCode: statusCode);
        }

        var profileJson = JsonSerializer.Serialize(new
        {
            nickName = displayName,
            avatarUrl,
        });

        var account = await LoadAccountByWechatSubjectAsync(db, wechatSession.OpenId, cancellationToken);
        if (account is null)
        {
            account = await UpsertWechatAccountAsync(db, wechatSession.OpenId, displayName, avatarUrl, cancellationToken);
        }
        else
        {
            await UpdateWechatAccountProfileAsync(db, account.Id, displayName, avatarUrl, cancellationToken);
            account = account with { DisplayName = displayName };
        }

        if (account is null)
        {
            return Results.Problem("Failed to create WeChat account.");
        }

        await EnsureLearnerScaffoldingAsync(db, account.Id, cancellationToken);
        await UpsertWechatIdentityAsync(
            db,
            account.Id,
            wechatSession.OpenId,
            wechatSession.UnionId,
            displayName,
            avatarUrl,
            profileJson,
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

    private static async Task<AccountDto?> LoadAccountByWechatSubjectAsync(
        NpgsqlDataSource db,
        string providerSubject,
        CancellationToken cancellationToken) =>
        await db.QuerySingleOrDefaultAsync(
            """
            select a.id, a.email, a.display_name, a.role, a.created_at
            from account_external_identities ei
            join accounts a on a.id = ei.account_id
            where ei.provider = 'wechat' and ei.provider_subject = @provider_subject
            """,
            MapAccount,
            [Pg.Param("provider_subject", providerSubject)],
            cancellationToken);

    private static async Task<AccountDto?> UpsertWechatAccountAsync(
        NpgsqlDataSource db,
        string providerSubject,
        string displayName,
        string? avatarUrl,
        CancellationToken cancellationToken) =>
        await db.QuerySingleOrDefaultAsync(
            """
            insert into accounts (email, password_hash, display_name, avatar_text, role)
            values (@email, @password_hash, @display_name, @avatar_url, 'learner')
            on conflict (email) do update set
              display_name = excluded.display_name,
              avatar_text = excluded.avatar_text,
              updated_at = now()
            returning id, email, display_name, role, created_at
            """,
            MapAccount,
            [
                Pg.Param("email", BuildExternalEmail(providerSubject)),
                Pg.Param("password_hash", PasswordHasher.Hash(Guid.NewGuid().ToString("N"))),
                Pg.Param("display_name", displayName),
                Pg.Param("avatar_url", avatarUrl),
            ],
            cancellationToken);

    private static Task UpdateWechatAccountProfileAsync(
        NpgsqlDataSource db,
        Guid accountId,
        string displayName,
        string? avatarUrl,
        CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            update accounts
            set display_name = @display_name,
                avatar_text = @avatar_url,
                updated_at = now()
            where id = @account_id
            """,
            [
                Pg.Param("account_id", accountId),
                Pg.Param("display_name", displayName),
                Pg.Param("avatar_url", avatarUrl),
            ],
            cancellationToken);

    private static async Task EnsureLearnerScaffoldingAsync(NpgsqlDataSource db, Guid accountId, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            "insert into user_profiles (account_id) values (@account_id) on conflict do nothing",
            [Pg.Param("account_id", accountId)],
            cancellationToken);

        await db.ExecuteAsync(
            "insert into account_roles (account_id, role) values (@account_id, 'learner') on conflict do nothing",
            [Pg.Param("account_id", accountId)],
            cancellationToken);
    }

    private static Task UpsertWechatIdentityAsync(
        NpgsqlDataSource db,
        Guid accountId,
        string providerSubject,
        string? unionSubject,
        string displayName,
        string? avatarUrl,
        string profileJson,
        CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            insert into account_external_identities (
              provider, provider_subject, account_id, union_subject, display_name, avatar_url, raw_profile, updated_at
            )
            values ('wechat', @provider_subject, @account_id, @union_subject, @display_name, @avatar_url, @raw_profile::jsonb, now())
            on conflict (provider, provider_subject) do update set
              account_id = excluded.account_id,
              union_subject = coalesce(excluded.union_subject, account_external_identities.union_subject),
              display_name = excluded.display_name,
              avatar_url = excluded.avatar_url,
              raw_profile = excluded.raw_profile,
              updated_at = now()
            """,
            [
                Pg.Param("provider_subject", providerSubject),
                Pg.Param("account_id", accountId),
                Pg.Param("union_subject", unionSubject),
                Pg.Param("display_name", displayName),
                Pg.Param("avatar_url", avatarUrl),
                Pg.Param("raw_profile", profileJson),
            ],
            cancellationToken);

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

    private static string BuildExternalEmail(string providerSubject)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(providerSubject))).ToLowerInvariant();
        return $"wechat-{hash[..32]}@wechat.bakein.local";
    }

    private sealed record AccountWithPassword(AccountDto Account, string PasswordHash);
}
