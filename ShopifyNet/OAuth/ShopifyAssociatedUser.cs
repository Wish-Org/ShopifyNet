namespace ShopifyNet;

/// <summary>
/// The staff member who authorized an online access token.
/// </summary>
public sealed record ShopifyAssociatedUser
{
    /// <summary>
    /// Uniquely identifies the user. Use it to key per-user data.
    /// </summary>
    public long id { get; init; }

    public string first_name { get; init; }

    public string last_name { get; init; }

    /// <summary>
    /// The email of the user. Only trust it when <see cref="email_verified"/> is true.
    /// </summary>
    public string email { get; init; }

    public bool email_verified { get; init; }

    /// <summary>
    /// Whether the user owns the Shopify account.
    /// </summary>
    public bool account_owner { get; init; }

    public string locale { get; init; }

    /// <summary>
    /// Whether the user is a collaborator rather than a staff member.
    /// </summary>
    public bool collaborator { get; init; }
}
