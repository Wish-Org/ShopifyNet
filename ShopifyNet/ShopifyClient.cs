using GraphQLSharp;
using ShopifyNet.Types;

namespace ShopifyNet;

public partial class ShopifyClient
{
    public Task<GraphQLResponse<Mutation>> MutationAsync(string query, int? cost = null, CancellationToken cancellationToken = default)
    {
        var request = new ShopifyGraphQLRequest
        {
            query = query,
            Cost = cost
        };
        return MutationAsync(request, cancellationToken);
    }

    public Task<GraphQLResponse<QueryRoot>> QueryAsync(string query, int? cost = null, CancellationToken cancellationToken = default)
    {
        var request = new ShopifyGraphQLRequest
        {
            query = query,
            Cost = cost
        };
        return QueryAsync(request, cancellationToken);
    }

}