using GraphQLSharp;

namespace ShopifyNet.Tests;

internal class TestInterceptor : IInterceptor
{
    private readonly IInterceptor _interceptor;
    public int CallCount { get; private set; }

    public TestInterceptor(IInterceptor interceptor)
    {
        this._interceptor = interceptor;
    }

    public Task<GraphQLResponse<TData>> InterceptRequestAsync<TGraphQLRequest, TClientOptions, TData>(TGraphQLRequest request,
        TClientOptions options,
        CancellationToken cancellationToken,
        Func<TGraphQLRequest, CancellationToken, Task<GraphQLResponse<TData>>> executeAsync)
        where TGraphQLRequest : GraphQLRequest
        where TClientOptions : IGraphQLClientOptions
    {
        return _interceptor.InterceptRequestAsync(request, options, cancellationToken, async (r, ct) =>
        {
            CallCount++;
            return await executeAsync(r, ct);
        });
    }
}
