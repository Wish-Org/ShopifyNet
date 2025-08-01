using System.Net.Http.Headers;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyClientOptions : GraphQLClientOptionsBase, IGraphQLClientOptions
{
    public const string DEFAULT_API_VERSION = "2025-07";
    private static readonly ProductInfoHeaderValue _userAgent = new(typeof(ShopifyClientOptions).Assembly.GetName().Name!, typeof(ShopifyClientOptions).Assembly.GetName().Version!.ToString());

    /// <summary>
    /// Optional API version to use for the Shopify API.
    /// Defaults to AdminClientOptions.DEFAULT_API_VERSION if not set.
    /// </summary>
    public string APIVersion { get; set; }

    /// <summary>
    /// The MyShopify domain of the store, such as "myshop.myshopify.com".
    /// </summary>
    public string MyShopifyDomain { get; set; }

    public string AccessToken { get; set; }

    public bool? RequestDetailedQueryCost { get; set; }

    Uri IGraphQLClientOptions.Uri => MyShopifyDomain == null ? null : new Uri($"https://{MyShopifyDomain}/admin/api/{APIVersion}/graphql.json");

    Action<HttpRequestHeaders> IGraphQLClientOptions.ConfigureHttpRequestHeaders => headers =>
        {
            headers.UserAgent.Add(_userAgent);
            if (AccessToken != null)
                headers.Add("X-Shopify-Access-Token", AccessToken);
            if (RequestDetailedQueryCost == true)
                headers.Add("Shopify-GraphQL-Cost-Debug", "1");
        };
}
