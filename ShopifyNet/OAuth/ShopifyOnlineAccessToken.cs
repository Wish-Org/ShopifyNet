namespace ShopifyNet;

/// <summary>
/// An online access token, tied to the authorizing staff member, as returned by Shopify when exchanging an ID token or authorization code.
/// Online access tokens can't be refreshed: request a new one with a fresh ID token or repeat the authorization code flow.
/// See https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens#online-access-tokens
/// </summary>
public sealed record ShopifyOnlineAccessToken
{
    /// <summary>
    /// The access token, sent in the X-Shopify-Access-Token header.
    /// </summary>
    public string access_token { get; init; }

    /// <summary>
    /// The comma separated list of access scopes granted to the app.
    /// </summary>
    public string scope { get; init; }

    /// <summary>
    /// The number of seconds until the access token expires, or null when Shopify omits expiry metadata.
    /// Null means the expiry is unknown, not that the token never expires.
    /// The token also expires when the user logs out of the Shopify admin.
    /// </summary>
    public int? expires_in { get; init; }

    /// <summary>
    /// The comma separated list of access scopes available to the token:
    /// the intersection of the app's scopes and the permissions of <see cref="associated_user"/>.
    /// </summary>
    public string associated_user_scope { get; init; }

    /// <summary>
    /// The staff member who authorized the token.
    /// </summary>
    public ShopifyAssociatedUser associated_user { get; init; }

    //avoid leaking secrets and personal information into logs
    public override string ToString()
    {
        return $"{nameof(ShopifyOnlineAccessToken)} {{ {nameof(scope)} = {scope}, {nameof(expires_in)} = {expires_in}, {nameof(associated_user_scope)} = {associated_user_scope}, associated_user_id = {associated_user?.id} }}";
    }
}
