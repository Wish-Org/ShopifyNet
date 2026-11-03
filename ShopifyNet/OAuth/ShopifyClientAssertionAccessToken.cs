namespace ShopifyNet;

/// <summary>
/// An application-only access token obtained using a client assertion.
/// There is no refresh token: request a new token with a fresh assertion before expiry.
/// </summary>
public sealed record ShopifyClientAssertionAccessToken
{
    /// <summary>The access token to send in the X-Shopify-Access-Token header.</summary>
    public string access_token { get; init; }

    /// <summary>The comma separated scopes granted to the app installation.</summary>
    public string scope { get; init; }

    /// <summary>The number of seconds until expiry, as returned by Shopify.</summary>
    public int expires_in { get; init; }

    // Avoid leaking the token into logs.
    public override string ToString() =>
        $"{nameof(ShopifyClientAssertionAccessToken)} {{ {nameof(scope)} = {scope}, {nameof(expires_in)} = {expires_in} }}";
}
