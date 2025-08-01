using GraphQLSharp;
using ShopifyNet.Types;

namespace ShopifyNet;

public partial class ShopifyClient
{
    /// <summary>
    /// The default interceptor used if none is specified in the options.
    /// It is a ChainedInterceptor that includes a TokenBucketInterceptor for rate limiting and a RetryInterceptor for handling retries.
    /// </summary>
    public static readonly IInterceptor DEFAULT_INTERCEPTOR =
                            new ChainedInterceptor(new TokenBucketInterceptor(), new RetryInterceptor());

    protected override IInterceptor DefaultInterceptor => DEFAULT_INTERCEPTOR;

    public Task<GraphQLResponse<Mutation>> MutationAsync(string query, int? cost = null, ShopifyClientOptions options = null, CancellationToken cancellationToken = default)
    {
        var request = new ShopifyGraphQLRequest
        {
            query = query,
            Cost = cost
        };
        return MutationAsync(request, options, cancellationToken);
    }

    public Task<GraphQLResponse<QueryRoot>> QueryAsync(string query, int? cost = null, ShopifyClientOptions options = null, CancellationToken cancellationToken = default)
    {
        var request = new ShopifyGraphQLRequest
        {
            query = query,
            Cost = cost
        };
        return QueryAsync(request, options, cancellationToken);
    }

}