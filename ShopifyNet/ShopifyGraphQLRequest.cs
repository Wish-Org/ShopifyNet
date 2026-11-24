using System.Text.Json.Serialization;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyGraphQLRequest : GraphQLRequest
{
    /// <summary>
    /// Optional estimated query cost. Not used by ShopifyNet itself: it is available to custom interceptors,
    /// for example to reserve rate-limit budget before sending the request.
    /// </summary>
    [JsonIgnore]
    public int? Cost { get; set; }

    /// <summary>
    /// Optional per-request access token. Not applied by ShopifyNet itself: an interceptor is expected to read it
    /// and set the X-Shopify-Access-Token header through the ExecuteRequestAsync header callback.
    /// Because <see cref="ShopifyClientOptions"/> already adds its own token to that header, the callback must call
    /// <c>headers.Remove("X-Shopify-Access-Token")</c> before adding this one, otherwise both values are sent.
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
