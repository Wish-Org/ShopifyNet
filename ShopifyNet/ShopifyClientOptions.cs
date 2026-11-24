using System.Net.Http.Headers;
using System.Text.Json;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyClientOptions : IGraphQLClientOptions<ShopifyClientOptions, ShopifyGraphQLRequest>
{
    public const string DEFAULT_API_VERSION = "2026-04";
    private static readonly ProductInfoHeaderValue _userAgent = new(
        typeof(ShopifyClientOptions).Assembly.GetName().Name!,
        typeof(ShopifyClientOptions).Assembly.GetName().Version!.ToString());

    /// <summary>
    /// A RetryInterceptor for handling retries.
    /// </summary>
    public static readonly IInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions> RETRY_INTERCEPTOR =
                            new RetryInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions>();

    /// <summary>
    /// The MyShopify domain of the store, such as "myshop.myshopify.com".
    /// </summary>
    public string MyShopifyDomain { get; private set; }

    /// <summary>
    /// Optional access token sent in the X-Shopify-Access-Token header with every request.
    /// Null when each request supplies its own token through <see cref="ShopifyGraphQLRequest.AccessToken"/>.
    /// </summary>
    public string AccessToken { get; private set; }

    /// <summary>
    /// Optional API version to use for the Shopify API.
    /// Defaults to ShopifyClientOptions.DEFAULT_API_VERSION if unspecified.
    /// </summary>
    public string APIVersion { get; private set; }

    /// <summary>
    /// Whether to get detailed query field level cost.
    /// Defaults to false if unspecified
    /// </summary>
    public bool? RequestDetailedQueryCost { get; init; } = false;

    public bool? ThrowOnGraphQLErrors { get; init; } = true;

    public HttpClient HttpClient { get; init; }

    public JsonSerializerOptions JsonSerializerOptions { get; init; }

    public IInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions> Interceptor { get; init; }

    private Uri _uri;

    public Uri Uri => _uri;

    Action<HttpRequestHeaders> IGraphQLClientOptions<ShopifyClientOptions, ShopifyGraphQLRequest>.ConfigureHttpRequestHeaders => this.ConfigureHttpRequestHeaders;

    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="accessToken">
    /// Optional access token sent with every request. May be null if each request supplies its own token through
    /// <see cref="ShopifyGraphQLRequest.AccessToken"/>, which an interceptor must apply.
    /// </param>
    /// <param name="apiVersion">Optional API version to use for the Shopify API. Defaults to ShopifyClientOptions.DEFAULT_API_VERSION if unspecified.</param>
    /// <param name="useRetryInterceptor">Whether to use the built-in retry interceptor (<see cref="RETRY_INTERCEPTOR"/>) to retry failed requests.</param>
    public ShopifyClientOptions(string myShopifyDomain,
                                string accessToken,
                                string apiVersion = DEFAULT_API_VERSION,
                                bool useRetryInterceptor = true)
    {
        MyShopifyDomain = myShopifyDomain ?? throw new ArgumentNullException(nameof(myShopifyDomain));
        AccessToken = accessToken;
        APIVersion = apiVersion ?? DEFAULT_API_VERSION;
        _uri = new Uri($"https://{MyShopifyDomain}/admin/api/{APIVersion}/graphql.json");

        if (useRetryInterceptor)
            Interceptor = RETRY_INTERCEPTOR;
    }

    private void ConfigureHttpRequestHeaders(HttpRequestHeaders headers)
    {
        headers.UserAgent.Add(_userAgent);

        if (this.AccessToken != null)
            headers.Add("X-Shopify-Access-Token", this.AccessToken);

        if (this.RequestDetailedQueryCost ?? true)
            headers.Add("Shopify-GraphQL-Cost-Debug", "1");
    }
}
