using System.Text.Json.Serialization;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyGraphQLRequest : GraphQLRequest
{
    [JsonIgnore]
    public int? Cost { get; set; }

    /// <summary>
    /// Optional per-request access token. Not applied by ShopifyNet itself: an interceptor is expected to read it
    /// and set the X-Shopify-Access-Token header through the ExecuteRequestAsync header callback.
    /// Because <see cref="ShopifyClientOptions"/> already adds its own token to that header, the callback must call
    /// <c>headers.Remove("X-Shopify-Access-Token")</c> before adding this one, otherwise both values are sent.
    /// When set, <see cref="TokenBucketInterceptor"/> uses it to select the rate-limit bucket.
    /// </summary>
    [JsonIgnore]
    public string AccessToken { get; set; }

    public static implicit operator ShopifyGraphQLRequest(string query)
    {
        return new ShopifyGraphQLRequest
        {
            query = query
        };
    }

}
