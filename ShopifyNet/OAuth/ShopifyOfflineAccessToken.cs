namespace ShopifyNet;

/// <summary>
/// An offline access token, as returned by Shopify when migrating a non-expiring offline access token,
/// refreshing an expiring offline access token, or exchanging an ID token or authorization code for an offline access token.
/// See https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens#offline-access-tokens
/// </summary>
public sealed record ShopifyOfflineAccessToken
{
    /// <summary>
    /// The access token, sent in the X-Shopify-Access-Token header.
    /// </summary>
    public string access_token { get; init; }

    /// <summary>
    /// The comma separated list of access scopes granted to the token.
    /// </summary>
    public string scope { get; init; }

    /// <summary>
    /// The number of seconds until the access token expires, or null for a non-expiring token.
    /// </summary>
    public int? expires_in { get; init; }

    /// <summary>
    /// The refresh token, used to get a new access token before this one expires.
    /// Shopify rotates the refresh token: always use the one returned by the latest refresh.
    /// Null for a non-expiring token.
    /// </summary>
    public string refresh_token { get; init; }

    /// <summary>
    /// The number of seconds until the refresh token expires, or null for a non-expiring token.
    /// </summary>
    public int? refresh_token_expires_in { get; init; }

    internal DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the access token expires, computed from <see cref="expires_in"/> and the request timestamp.
    /// It is computed from the time the request was sent, so it is slightly earlier than the actual expiry.
    /// Null for a non-expiring token.
    /// </summary>
    public DateTimeOffset? ExpiresAt => expires_in is int expiresIn ? RequestedAt.AddSeconds(expiresIn) : null;

    /// <summary>
    /// When the refresh token expires, computed from <see cref="refresh_token_expires_in"/> and the request timestamp.
    /// It is computed from the time the request was sent, so it is slightly earlier than the actual expiry.
    /// Null for a non-expiring token.
    /// </summary>
    public DateTimeOffset? RefreshTokenExpiresAt => refresh_token_expires_in is int expiresIn ? RequestedAt.AddSeconds(expiresIn) : null;

    //avoid leaking secrets into logs
    public override string ToString()
    {
        return $"{nameof(ShopifyOfflineAccessToken)} {{ {nameof(scope)} = {scope}, {nameof(expires_in)} = {expires_in}, {nameof(refresh_token_expires_in)} = {refresh_token_expires_in}, {nameof(ExpiresAt)} = {ExpiresAt:O}, {nameof(RefreshTokenExpiresAt)} = {RefreshTokenExpiresAt:O} }}";
    }
}
