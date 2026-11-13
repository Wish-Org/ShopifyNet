using GraphQLSharp;

namespace ShopifyNet.Tests;

internal class TestInterceptor : IInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions>
{
    private readonly IInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions> _interceptor;
    public int CallCount { get; private set; }

    public TestInterceptor(IInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions> interceptor)
    {
        this._interceptor = interceptor;
    }

    public Task<GraphQLResponse<TData>> InterceptRequestAsync<TData>(ShopifyGraphQLRequest request, ShopifyClientOptions options, CancellationToken cancellationToken, ExecuteRequestAsync<ShopifyGraphQLRequest, TData> executeAsync)
    {
        return _interceptor.InterceptRequestAsync(request, options, cancellationToken, async (r, _, ct) =>
        {
            CallCount++;
            return await executeAsync(r, null, ct);
        });
    }
}
