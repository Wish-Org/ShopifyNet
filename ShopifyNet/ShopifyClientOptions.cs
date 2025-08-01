using System.Net.Http.Headers;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyClientOptions : GraphQLClientOptionsBase, IGraphQLClientOptions<ShopifyClientOptions>
{
    public const string DEFAULT_API_VERSION = "2025-07";
    private static readonly ProductInfoHeaderValue _userAgent = new(typeof(ShopifyClientOptions).Assembly.GetName().Name!, typeof(ShopifyClientOptions).Assembly.GetName().Version!.ToString());

    /// <summary>
    /// The MyShopify domain of the store, such as "myshop.myshopify.com".
    /// </summary>
    public string MyShopifyDomain { get; set; }

    public string AccessToken { get; set; }

    /// <summary>
    /// Optional API version to use for the Shopify API.
    /// Defaults to AdminClientOptions.DEFAULT_API_VERSION if unspecified.
    /// </summary>
    public string APIVersion { get; set; }

    /// <summary>
    /// Whether to get detailed query field level cost.
    /// Defaults to false if unspecified
    /// </summary>
    public bool? RequestDetailedQueryCost { get; set; }

    public ShopifyClientOptions()
    {
    }

    /// <param name="myShopifyDomain">The MyShopify domain of the store, such as "myshop.myshopify.com".</param>
    /// <param name="accessToken"></param>
    /// <param name="apiVersion">Optional API version to use for the Shopify API.
    ///  Defaults to AdminClientOptions.DEFAULT_API_VERSION if unspecified.</param>
    /// <param name="requestDetailedQueryCost">Whether to get detailed query field level cost. 
    /// Defaults to false if unspecified</param>
    public ShopifyClientOptions(string myShopifyDomain, string accessToken, string apiVersion = null, bool? requestDetailedQueryCost = null)
    {
        MyShopifyDomain = myShopifyDomain ?? throw new ArgumentNullException(nameof(myShopifyDomain));
        AccessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));
        APIVersion = apiVersion;
        RequestDetailedQueryCost = requestDetailedQueryCost;
    }

    static Uri IGraphQLClientOptions<ShopifyClientOptions>.GetUri(ShopifyClientOptions defaultOptions, ShopifyClientOptions requestOptions)
    {
        string myShopifyDomain = requestOptions?.MyShopifyDomain ?? defaultOptions?.MyShopifyDomain ?? throw new ArgumentNullException(nameof(MyShopifyDomain));
        string apiVersion = requestOptions?.APIVersion ?? defaultOptions?.APIVersion ?? DEFAULT_API_VERSION;
        return new Uri($"https://{myShopifyDomain}/admin/api/{apiVersion}/graphql.json");
    }

    static Action<HttpRequestHeaders> IGraphQLClientOptions<ShopifyClientOptions>.GetConfigureHttpRequestHeaders(ShopifyClientOptions defaultOptions, ShopifyClientOptions requestOptions)
    {
        string accessToken = requestOptions?.AccessToken ?? defaultOptions?.AccessToken ?? throw new ArgumentNullException(nameof(AccessToken));
        bool requestDetailedQueryCost = requestOptions?.RequestDetailedQueryCost ?? defaultOptions?.RequestDetailedQueryCost ?? false;
        return headers =>
        {
            headers.UserAgent.Add(_userAgent);

            if (accessToken != null)
                headers.Add("X-Shopify-Access-Token", accessToken);

            if (requestDetailedQueryCost)
                headers.Add("Shopify-GraphQL-Cost-Debug", "1");
        };
    }
}
