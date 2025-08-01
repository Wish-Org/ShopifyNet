using System.Net.Http.Headers;
using System.Text.Json;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyClientOptions : IGraphQLClientOptions
{
    public const string DEFAULT_API_VERSION = "2025-07";
    private static readonly ProductInfoHeaderValue _userAgent = new(
        typeof(ShopifyClientOptions).Assembly.GetName().Name!,
        typeof(ShopifyClientOptions).Assembly.GetName().Version!.ToString());

    /// <summary>
    /// A ChainedInterceptor with a TokenBucketInterceptor for rate limiting and a RetryInterceptor for handling retries.
    /// </summary>
    public static readonly IInterceptor SMART_INTERCEPTOR =
                            new ChainedInterceptor(new TokenBucketInterceptor(), new RetryInterceptor());

    /// <summary>
    /// The MyShopify domain of the store, such as "myshop.myshopify.com".
    /// </summary>
    public string MyShopifyDomain { get; private set; }

    public string AccessToken { get; private set; }

    /// <summary>
    /// Optional API version to use for the Shopify API.
    /// Defaults to AdminClientOptions.DEFAULT_API_VERSION if unspecified.
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

    public IInterceptor Interceptor { get; init; }

    private Uri _uri;

    Uri IGraphQLClientOptions.Uri => _uri;

    Action<HttpRequestHeaders> IGraphQLClientOptions.ConfigureHttpRequestHeaders => this.ConfigureHttpRequestHeaders;

    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="accessToken"></param>
    /// <param name="apiVersion">Optional API version to use for the Shopify API. Defaults to AdminClientOptions.DEFAULT_API_VERSION if unspecified.</param>
    /// <param name="useSmartInterceptor">Whether to use the built-in smart interceptor for rate limiting and retries.
    public ShopifyClientOptions(string myShopifyDomain,
                                string accessToken,
                                string apiVersion = DEFAULT_API_VERSION,
                                bool useSmartInterceptor = true)
    {
        MyShopifyDomain = myShopifyDomain ?? throw new ArgumentNullException(nameof(myShopifyDomain));
        AccessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        APIVersion = apiVersion ?? DEFAULT_API_VERSION;
        _uri = new Uri($"https://{MyShopifyDomain}/admin/api/{APIVersion}/graphql.json");

        if (useSmartInterceptor)
            Interceptor = SMART_INTERCEPTOR;
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
