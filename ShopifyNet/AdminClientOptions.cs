using System.Net.Http.Headers;
using GraphQLSharp;

namespace ShopifyNet;

public class AdminClientOptions : GraphQLClientOptions
{
    public const string DEFAULT_API_VERSION = "2025-07";

    private static readonly ProductInfoHeaderValue _defaultUserAgent = new(typeof(AdminClientOptions).Assembly.GetName().Name!, typeof(AdminClientOptions).Assembly.GetName().Version!.ToString());

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

    /// <summary>
    /// Api URI.
    /// Use MyShopifyDomain and APIVersion setters instead
    /// </summary>
    public override Uri Uri
    {
        get => MyShopifyDomain == null ? null : new Uri($"https://{MyShopifyDomain}/admin/api/{this.APIVersion ?? DEFAULT_API_VERSION}/graphql.json");

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        [Obsolete($"Use {nameof(MyShopifyDomain)} and {nameof(APIVersion)} instead.")]
        set => throw new NotSupportedException($"{nameof(Uri)} should not be set. Use {nameof(MyShopifyDomain)} and {nameof(APIVersion)} instead.");
#pragma warning restore CS0809
    }

    private Action<HttpRequestHeaders> _DefaultConfigureHttpRequestHeaders;

    /// <summary>
    /// Optional action to configure HTTP request headers.
    /// </summary>
    public override Action<HttpRequestHeaders> ConfigureHttpRequestHeaders
    {
        get => base.ConfigureHttpRequestHeaders;
        set => base.ConfigureHttpRequestHeaders = headers =>
        {
            value(headers);
            if (headers.Contains("X-Shopify-Access-Token"))
                throw new NotSupportedException("X-Shopify-Access-Token header should not be set. Use AccessToken property instead.");

            this._DefaultConfigureHttpRequestHeaders(headers);
        };
    }

    public AdminClientOptions()
    {
        base.ConfigureHttpRequestHeaders = this._DefaultConfigureHttpRequestHeaders = headers =>
        {
            headers.UserAgent.Add(_defaultUserAgent);
            if (AccessToken != null)
                headers.Add("X-Shopify-Access-Token", AccessToken);
        };
    }
}
