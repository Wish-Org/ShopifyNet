using System.Text.Json.Serialization;
using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyGraphQLRequest : GraphQLRequest
{
    [JsonIgnore]
    public int? Cost { get; set; }

    public static implicit operator ShopifyGraphQLRequest(string query)
    {
        return new ShopifyGraphQLRequest
        {
            query = query
        };
    }

}
