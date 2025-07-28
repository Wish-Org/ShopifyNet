using GraphQLSharp;

namespace ShopifyNet;

public class ShopifyGraphQLRequest : GraphQLRequest
{
    public int Cost { get; set; }
}
