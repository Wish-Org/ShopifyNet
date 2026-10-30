using System.Net;

namespace ShopifyNet;

/// <summary>
/// Thrown when Shopify rejects a request to the OAuth access token endpoint, or returns an invalid response.
/// </summary>
public class ShopifyOAuthException : Exception
{
    /// <summary>
    /// The HTTP status code returned by Shopify.
    /// For example 400 Bad Request for an invalid ID token or subject token, or 401 Unauthorized for an invalid refresh token.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The OAuth error code returned by Shopify, such as "invalid_subject_token" or "invalid_request", if any.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// The OAuth error description returned by Shopify, if any.
    /// </summary>
    public string ErrorDescription { get; }

    public ShopifyOAuthException(string message, HttpStatusCode statusCode, string error = null, string errorDescription = null, Exception innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Error = error;
        ErrorDescription = errorDescription;
    }
}
