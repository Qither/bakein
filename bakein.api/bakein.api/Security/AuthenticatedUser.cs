namespace Bakein.Api.Security;

public sealed record AuthenticatedUser(Guid Id, string Email, string DisplayName, string Role);

public static class HttpContextAuthExtensions
{
    private const string UserItemKey = "Bakein.AuthenticatedUser";

    public static void SetAuthenticatedUser(this HttpContext context, AuthenticatedUser user) =>
        context.Items[UserItemKey] = user;

    public static AuthenticatedUser? GetAuthenticatedUser(this HttpContext context) =>
        context.Items.TryGetValue(UserItemKey, out var value) ? value as AuthenticatedUser : null;
}
