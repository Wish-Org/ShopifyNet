using System.Net;
using System.Text.Json;

namespace ShopifyNet;

/// <summary>
/// Client for the Shopify OAuth access token endpoint (https://{shop}.myshopify.com/admin/oauth/access_token).
/// See https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens
/// </summary>
public class ShopifyOAuthClient
{
    private const string GRANT_TYPE_KEY = "grant_type";
    private const string CLIENT_ID_KEY = "client_id";
    private const string CLIENT_SECRET_KEY = "client_secret";
    private const string SUBJECT_TOKEN_KEY = "subject_token";
    private const string SUBJECT_TOKEN_TYPE_KEY = "subject_token_type";
    private const string REQUESTED_TOKEN_TYPE_KEY = "requested_token_type";
    private const string REFRESH_TOKEN_KEY = "refresh_token";
    private const string EXPIRING_KEY = "expiring";

    private const string EXPIRING_VALUE = "1";
    private const string REFRESH_TOKEN_GRANT_TYPE = "refresh_token";
    private const string TOKEN_EXCHANGE_GRANT_TYPE = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string ID_TOKEN_TYPE = "urn:ietf:params:oauth:token-type:id_token";
    private const string OFFLINE_ACCESS_TOKEN_TYPE = "urn:shopify:params:oauth:token-type:offline-access-token";
    private const string ONLINE_ACCESS_TOKEN_TYPE = "urn:shopify:params:oauth:token-type:online-access-token";

    private static readonly HttpClient _defaultHttpClient = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    /// <param name="clientId">The client ID (API key) of the app.</param>
    /// <param name="clientSecret">The client secret (API secret key) of the app.</param>
    /// <param name="httpClient">Optional HttpClient. If null, a shared default HttpClient is used. If set, you control the lifetime of the HttpClient.</param>
    /// <param name="timeProvider">Optional time provider used to compute the expiry dates of the returned tokens. Defaults to TimeProvider.System.</param>
    public ShopifyOAuthClient(string clientId, string clientSecret, HttpClient httpClient = null, TimeProvider timeProvider = null)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _httpClient = httpClient ?? _defaultHttpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Migrates a non-expiring offline access token to an expiring offline access token, without requiring a user session.
    /// Migration permanently invalidates the non-expiring token for GraphQL Admin API requests: the returned token MUST be persisted.
    /// If the response is lost, the same request can be repeated with the original non-expiring token within seven days to recover the same token.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/migrate-to-expiring-offline-access-tokens#migrate-existing-tokens-without-a-user-session
    /// </summary>
    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="nonExpiringAccessToken">The active non-expiring offline access token to migrate.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ShopifyOAuthException">
    /// Shopify rejected the request, for example with 400 Bad Request and the "invalid_subject_token" error if the token isn't an active non-expiring offline access token.
    /// </exception>
    public Task<ShopifyOfflineAccessToken> MigrateToExpiringOfflineAccessTokenAsync(string myShopifyDomain, string nonExpiringAccessToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nonExpiringAccessToken);
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, SetExpiryDates, new()
        {
            [GRANT_TYPE_KEY] = TOKEN_EXCHANGE_GRANT_TYPE,
            [SUBJECT_TOKEN_KEY] = nonExpiringAccessToken,
            [SUBJECT_TOKEN_TYPE_KEY] = OFFLINE_ACCESS_TOKEN_TYPE,
            [REQUESTED_TOKEN_TYPE_KEY] = OFFLINE_ACCESS_TOKEN_TYPE,
            [EXPIRING_KEY] = EXPIRING_VALUE,
        }, cancellationToken);
    }

    /// <summary>
    /// Exchanges the refresh token of an expiring offline access token for a new expiring offline access token.
    /// Shopify rotates the refresh token: the returned token (including its new refresh token) MUST be persisted and used for the next refresh.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/implement-token-exchange#refresh-an-expiring-offline-token
    /// </summary>
    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ShopifyOAuthException">
    /// Shopify rejected the request, for example with 401 Unauthorized if the refresh token is expired, already used or otherwise invalid,
    /// in which case the merchant must re-authenticate.
    /// </exception>
    public Task<ShopifyOfflineAccessToken> RefreshOfflineAccessTokenAsync(string myShopifyDomain, string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, SetExpiryDates, new()
        {
            [GRANT_TYPE_KEY] = REFRESH_TOKEN_GRANT_TYPE,
            [REFRESH_TOKEN_KEY] = refreshToken,
        }, cancellationToken);
    }

    /// <summary>
    /// Exchanges an ID (session) token, such as one provided by App Bridge, for an expiring offline access token.
    /// The ID token must have been validated by the caller.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens#token-exchange-grant
    /// </summary>
    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="idToken">The ID token.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ShopifyOAuthException">Shopify rejected the request, for example with 400 Bad Request if the ID token is expired or invalid.</exception>
    public Task<ShopifyOfflineAccessToken> ExchangeIdTokenForOfflineAccessTokenAsync(string myShopifyDomain, string idToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idToken);
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, SetExpiryDates, new()
        {
            [GRANT_TYPE_KEY] = TOKEN_EXCHANGE_GRANT_TYPE,
            [SUBJECT_TOKEN_KEY] = idToken,
            [SUBJECT_TOKEN_TYPE_KEY] = ID_TOKEN_TYPE,
            [REQUESTED_TOKEN_TYPE_KEY] = OFFLINE_ACCESS_TOKEN_TYPE,
            [EXPIRING_KEY] = EXPIRING_VALUE,
        }, cancellationToken);
    }

    /// <summary>
    /// Exchanges an ID (session) token, such as one provided by App Bridge, for an online access token tied to the user of the ID token.
    /// The ID token must have been validated by the caller.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens#token-exchange-grant
    /// </summary>
    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="idToken">The ID token.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ShopifyOAuthException">Shopify rejected the request, for example with 400 Bad Request if the ID token is expired or invalid.</exception>
    public Task<ShopifyOnlineAccessToken> ExchangeIdTokenForOnlineAccessTokenAsync(string myShopifyDomain, string idToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idToken);
        //the expiring parameter only applies to offline access tokens
        return RequestAccessTokenAsync<ShopifyOnlineAccessToken>(myShopifyDomain, SetExpiryDates, new()
        {
            [GRANT_TYPE_KEY] = TOKEN_EXCHANGE_GRANT_TYPE,
            [SUBJECT_TOKEN_KEY] = idToken,
            [SUBJECT_TOKEN_TYPE_KEY] = ID_TOKEN_TYPE,
            [REQUESTED_TOKEN_TYPE_KEY] = ONLINE_ACCESS_TOKEN_TYPE,
        }, cancellationToken);
    }

    private static ShopifyOfflineAccessToken SetExpiryDates(ShopifyOfflineAccessToken token, DateTimeOffset requestedAt) => token with
    {
        ExpiresAt = requestedAt.AddSeconds(token.expires_in),
        RefreshTokenExpiresAt = requestedAt.AddSeconds(token.refresh_token_expires_in),
    };

    private static ShopifyOnlineAccessToken SetExpiryDates(ShopifyOnlineAccessToken token, DateTimeOffset requestedAt) => token with
    {
        ExpiresAt = requestedAt.AddSeconds(token.expires_in),
    };

    private async Task<T> RequestAccessTokenAsync<T>(string myShopifyDomain, Func<T, DateTimeOffset, T> setExpiryDates, Dictionary<string, string> parameters, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(myShopifyDomain);
        //the client secret is sent to this host, so reject anything that isn't a plain host name (e.g. "evil.com/path?")
        if (Uri.CheckHostName(myShopifyDomain) != UriHostNameType.Dns)
            throw new ArgumentException($"'{myShopifyDomain}' is not a valid host name.", nameof(myShopifyDomain));

        parameters[CLIENT_ID_KEY] = _clientId;
        parameters[CLIENT_SECRET_KEY] = _clientSecret;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{myShopifyDomain}/admin/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.ParseAdd("application/json");

        //captured before sending so the computed expiry dates are never later than the actual ones
        var requestedAt = _timeProvider.GetUtcNow();
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = TryDeserialize<ErrorResponse>(body, out _);
            throw new ShopifyOAuthException(
                $"Shopify access token request failed with status {(int)response.StatusCode} ({response.StatusCode}): {error?.error ?? "unknown_error"} {error?.error_description}".TrimEnd(),
                response.StatusCode, error?.error, error?.error_description);
        }

        var token = TryDeserialize<T>(body, out var deserializationException);
        if (token == null)
            throw new ShopifyOAuthException("Failed to deserialize the Shopify access token response.", response.StatusCode, innerException: deserializationException);
        return setExpiryDates(token, requestedAt);
    }

    private static T TryDeserialize<T>(string json, out JsonException exception) where T : class
    {
        exception = null;
        try
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            exception = ex;
            return null;
        }
    }

    private sealed class ErrorResponse
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }
}
