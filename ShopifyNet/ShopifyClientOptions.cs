using System.Net.Http.Headers;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyClientOptions : GraphQLClientOptionsBase, IGraphQLClientOptions
{
    public const string DEFAULT_API_VERSION = "2025-07";

    private static readonly ProductInfoHeaderValue _userAgent = new(typeof(ShopifyClientOptions).Assembly.GetName().Name!, typeof(ShopifyClientOptions).Assembly.GetName().Version!.ToString());

    private static readonly IInterceptor _tokenBucketWithRetryInterceptor =
                            new ChainedInterceptor(new TokenBucketInterceptor(), new RetryInterceptor());

    /// <summary>
    /// Optional API version to use for the Shopify API.
    /// Defaults to AdminClientOptions.DEFAULT_API_VERSION if not set.
    /// </summary>
    public string APIVersion { get; }

    public string MyShopifyDomain { get; }

    public string AccessToken { get; }

    public bool RequestDetailedQueryCost { get; set; }

    private Uri _uri;

    Uri IGraphQLClientOptions.Uri => _uri;

    Action<HttpRequestHeaders> IGraphQLClientOptions.ConfigureHttpRequestHeaders => headers =>
        {
            headers.UserAgent.Add(_userAgent);
            if (AccessToken != null)
                headers.Add("X-Shopify-Access-Token", AccessToken);
            if (RequestDetailedQueryCost)
                headers.Add("Shopify-GraphQL-Cost-Debug", "1");
        };

    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="accessToken"></param>
    /// <param name="apiVersion">Optional API version to use for the Shopify API. Defaults to ShopifyClientOptions.DEFAULT_API_VERSION if not set.</param>
    public ShopifyClientOptions(string myShopifyDomain, string accessToken, string apiVersion = DEFAULT_API_VERSION)
    {
        MyShopifyDomain = myShopifyDomain ?? throw new ArgumentNullException(nameof(myShopifyDomain));
        AccessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        APIVersion = apiVersion;
        _uri = new Uri($"https://{MyShopifyDomain}/admin/api/{APIVersion}/graphql.json");
        Interceptor = _tokenBucketWithRetryInterceptor;//default interceptor for rate limiting and retry logic
    }
}
