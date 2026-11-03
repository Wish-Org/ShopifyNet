using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShopifyNet;

/// <summary>
/// Builds Shopify OAuth authorization URLs, validates callbacks and calls the access token endpoint.
/// See https://shopify.dev/docs/apps/build/authentication-authorization/access-tokens
/// </summary>
public class ShopifyOAuthClient
{
    private const string GRANT_TYPE_KEY = "grant_type";
    private const string CLIENT_ID_KEY = "client_id";
    private const string CLIENT_SECRET_KEY = "client_secret";
    private const string CODE_KEY = "code";
    private const string SUBJECT_TOKEN_KEY = "subject_token";
    private const string SUBJECT_TOKEN_TYPE_KEY = "subject_token_type";
    private const string REQUESTED_TOKEN_TYPE_KEY = "requested_token_type";
    private const string REFRESH_TOKEN_KEY = "refresh_token";
    private const string EXPIRING_KEY = "expiring";
    private const string SCOPE_KEY = "scope";
    private const string REDIRECT_URI_KEY = "redirect_uri";
    private const string STATE_KEY = "state";
    private const string GRANT_OPTIONS_KEY = "grant_options[]";

    private const string EXPIRING_VALUE = "1";
    private const string REFRESH_TOKEN_GRANT_TYPE = "refresh_token";
    private const string TOKEN_EXCHANGE_GRANT_TYPE = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string ID_TOKEN_TYPE = "urn:ietf:params:oauth:token-type:id_token";
    private const string OFFLINE_ACCESS_TOKEN_TYPE = "urn:shopify:params:oauth:token-type:offline-access-token";
    private const string ONLINE_ACCESS_TOKEN_TYPE = "urn:shopify:params:oauth:token-type:online-access-token";

    private static readonly HttpClient _defaultHttpClient = new(new SocketsHttpHandler
    {
        // Request and automatically decode compressed responses from Shopify.
        AutomaticDecompression = DecompressionMethods.All
    });

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;

    public ShopifyOAuthClient(string clientId, string clientSecret, HttpClient httpClient = null)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _httpClient = httpClient ?? _defaultHttpClient;
    }

    /// <summary>
    /// Builds the Shopify authorization URL to which the merchant should be redirected.
    /// No request is sent. The caller must generate and store a random state nonce, then validate it in the callback.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/authenticate-standalone-apps
    /// </summary>
    public string BuildAuthorizationUrl(string myShopifyDomain, string scopes, string redirectUri, string state, bool online = false)
    {
        ArgumentNullException.ThrowIfNull(myShopifyDomain);
        if (!IsValidShopDomain(myShopifyDomain))
            throw new ArgumentException("Invalid shop domain.", nameof(myShopifyDomain));
        ArgumentException.ThrowIfNullOrWhiteSpace(scopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var callbackUri) ||
            (callbackUri.Scheme != Uri.UriSchemeHttps && callbackUri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(callbackUri.Fragment) || !string.IsNullOrEmpty(callbackUri.UserInfo))
            throw new ArgumentException("Expected an absolute HTTP or HTTPS callback URI without a fragment or user information.", nameof(redirectUri));

        var parameters = new Dictionary<string, string>
        {
            [CLIENT_ID_KEY] = _clientId,
            [SCOPE_KEY] = scopes,
            [REDIRECT_URI_KEY] = redirectUri,
            [STATE_KEY] = state,
        };
        if (online)
            parameters[GRANT_OPTIONS_KEY] = "per-user";

        var query = string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        return $"https://{myShopifyDomain}/admin/oauth/authorize?{query}";
    }

    /// <summary>
    /// Checks for a plain shop.myshopify.com hostname with a single valid DNS label for the shop.
    /// URLs, ports, paths, subdomains and trailing dots are not accepted. All token requests enforce this check.
    /// </summary>
    public static bool IsValidShopDomain(string myShopifyDomain)
    {
        return myShopifyDomain != null &&
            Regex.IsMatch(myShopifyDomain, @"\A[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.myshopify\.com\z",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Verifies the OAuth callback query's hexadecimal HMAC-SHA256 using the app's client secret.
    /// Supply all URL-decoded query parameters, including hmac, preserving any duplicates.
    /// Parameters are sorted ordinally and joined as key=value pairs, excluding hmac and signature.
    /// Repeated keys ending in [] represent arrays: ids[]=1 and ids[]=2 are signed as ids=["1", "2"].
    /// Preserve array element order and supply each element as a separate key/value pair, even for one element.
    /// Escape % and &amp; in keys and values, and = in keys only. Other characters remain literal.
    /// Duplicate scalar keys, conflicting scalar/array keys, null values and invalid HMACs return false.
    /// This checks authenticity only: separately validate the shop, stored state nonce and any timestamp freshness policy.
    /// </summary>
    public bool IsValidHmac(IEnumerable<KeyValuePair<string, string>> queryParameters)
    {
        if (queryParameters == null)
            return false;

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in queryParameters.GroupBy(p => p.Key, StringComparer.Ordinal))
        {
            var isArray = group.Key?.EndsWith("[]", StringComparison.Ordinal) == true;
            // For arrays key names end with [], but the key used in the HMAC calculation omits the brackets.
            var key = isArray ? group.Key[..^2] : group.Key;
            var values = group.Select(p => p.Value).ToList();
            if (string.IsNullOrEmpty(key) || values.Contains(null) || (!isArray && values.Count > 1) ||
                (isArray && key is "hmac" or "signature"))
                return false;
            // A comma followed by a space is part of Shopify's signed array representation.
            //https://github.com/nozzlegear/ShopifySharp/pull/437
            //https://github.com/nozzlegear/ShopifySharp/issues/530
            var value = isArray ? "[\"" + string.Join("\", \"", values) + "\"]" : values[0];
            if (!parameters.TryAdd(key, value))
                return false;
        }

        if (!parameters.Remove("hmac", out var hmac) || hmac.Length != 64)
            return false;

        byte[] suppliedHash;
        try
        {
            suppliedHash = Convert.FromHexString(hmac);
        }
        catch (FormatException)
        {
            return false;
        }

        var message = string.Join("&", parameters.Where(p => p.Key != "signature").Select(p =>
            $"{EncodeHmacQueryComponent(p.Key, isKey: true)}={EncodeHmacQueryComponent(p.Value)}"));
        var expectedHash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_clientSecret), Encoding.UTF8.GetBytes(message));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    private static string EncodeHmacQueryComponent(string value, bool isKey = false)
    {
        // This method encodes query components for HMAC verification according to Shopify's requirements.
        // See https://github.com/nozzlegear/ShopifySharp/blob/master/ShopifySharp/Utilities/ShopifyRequestValidationUtility.cs#L107
        //     https://webkul.com/blog/hmac-validation-shopify-app/
        var encoded = value.Replace("%", "%25").Replace("&", "%26");
        return isKey ? encoded.Replace("=", "%3D") : encoded;
    }

    /// <summary>
    /// Migrates a non-expiring offline access token to an expiring offline access token, without requiring a user session.
    /// Migration permanently invalidates the non-expiring token for GraphQL Admin API requests: the returned token MUST be persisted.
    /// If the response is lost, the same request can be repeated with the original non-expiring token within seven days to recover the same token.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/migrate-to-expiring-offline-access-tokens#migrate-existing-tokens-without-a-user-session
    /// </summary>
    public Task<ShopifyOfflineAccessToken> MigrateToExpiringOfflineAccessTokenAsync(string myShopifyDomain, string nonExpiringAccessToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nonExpiringAccessToken);
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, new()
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
    public Task<ShopifyOfflineAccessToken> RefreshOfflineAccessTokenAsync(string myShopifyDomain, string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, new()
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
    public Task<ShopifyOfflineAccessToken> ExchangeIdTokenForOfflineAccessTokenAsync(string myShopifyDomain, string idToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idToken);
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, new()
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
    public Task<ShopifyOnlineAccessToken> ExchangeIdTokenForOnlineAccessTokenAsync(string myShopifyDomain, string idToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idToken);
        //the expiring parameter only applies to offline access tokens
        return RequestAccessTokenAsync<ShopifyOnlineAccessToken>(myShopifyDomain, new()
        {
            [GRANT_TYPE_KEY] = TOKEN_EXCHANGE_GRANT_TYPE,
            [SUBJECT_TOKEN_KEY] = idToken,
            [SUBJECT_TOKEN_TYPE_KEY] = ID_TOKEN_TYPE,
            [REQUESTED_TOKEN_TYPE_KEY] = ONLINE_ACCESS_TOKEN_TYPE,
        }, cancellationToken);
    }

    /// <summary>
    /// Exchanges an authorization code for an expiring or non-expiring offline access token.
    /// The caller must validate the OAuth callback (including HMAC, state and shop) before exchanging the code.
    /// The code must come from an authorization request without grant_options[]=per-user.
    /// </summary>
    public Task<ShopifyOfflineAccessToken> ExchangeCodeForOfflineAccessTokenAsync(string myShopifyDomain, string code, bool expiring, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        var parameters = new Dictionary<string, string>
        {
            [CODE_KEY] = code,
        };
        if (expiring)
            parameters[EXPIRING_KEY] = EXPIRING_VALUE;
        return RequestAccessTokenAsync<ShopifyOfflineAccessToken>(myShopifyDomain, parameters, cancellationToken);
    }

    /// <summary>
    /// Exchanges an authorization code for an online access token tied to the authorizing user.
    /// The caller must validate the OAuth callback (including HMAC, state and shop) before exchanging the code.
    /// The code must come from an authorization request with grant_options[]=per-user.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/authenticate-standalone-apps
    /// </summary>
    public Task<ShopifyOnlineAccessToken> ExchangeCodeForOnlineAccessTokenAsync(string myShopifyDomain, string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return RequestAccessTokenAsync<ShopifyOnlineAccessToken>(myShopifyDomain, new()
        {
            [CODE_KEY] = code,
        }, cancellationToken);
    }

    private async Task<T> RequestAccessTokenAsync<T>(string myShopifyDomain, Dictionary<string, string> parameters, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(myShopifyDomain);
        //The client secret must only be sent to a Shopify store hostname.
        if (!IsValidShopDomain(myShopifyDomain))
            throw new ArgumentException("Invalid shop domain.", nameof(myShopifyDomain));

        parameters[CLIENT_ID_KEY] = _clientId;
        parameters[CLIENT_SECRET_KEY] = _clientSecret;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{myShopifyDomain}/admin/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.ParseAdd("application/json");

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

        switch (token)
        {
            case ShopifyOfflineAccessToken offlineToken:
                var expiringRequested = parameters.GetValueOrDefault(EXPIRING_KEY) == EXPIRING_VALUE ||
                    parameters.GetValueOrDefault(GRANT_TYPE_KEY) == REFRESH_TOKEN_GRANT_TYPE;
                ValidateOfflineToken(offlineToken, expiringRequested, response.StatusCode);
                return token;
            case ShopifyOnlineAccessToken onlineToken:
                ValidateOnlineToken(onlineToken, response.StatusCode);
                return token;
            default:
                throw new ShopifyOAuthException("The Shopify access token response does not contain a usable access_token.", response.StatusCode);
        }
    }

    private static void ValidateOfflineToken(ShopifyOfflineAccessToken token, bool expiringRequested, HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(token.access_token))
            throw new ShopifyOAuthException("The Shopify access token response does not contain a usable access_token.", statusCode);

        // Check the request too, so a response missing all expiry metadata cannot pass as non-expiring.
        var expiring = expiringRequested || token.expires_in.HasValue ||
            token.refresh_token_expires_in.HasValue || token.refresh_token != null;
        if (expiring && (string.IsNullOrWhiteSpace(token.refresh_token) ||
            token.expires_in is not > 0 || token.refresh_token_expires_in is not > 0))
            throw new ShopifyOAuthException("An expiring offline token response must contain a usable refresh_token and positive expires_in and refresh_token_expires_in values.", statusCode);
    }

    private static void ValidateOnlineToken(ShopifyOnlineAccessToken token, HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(token.access_token))
            throw new ShopifyOAuthException("The Shopify access token response does not contain a usable access_token.", statusCode);

        if (token.expires_in is <= 0)
            throw new ShopifyOAuthException("The online token response must contain a positive expires_in when provided.", statusCode);
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
