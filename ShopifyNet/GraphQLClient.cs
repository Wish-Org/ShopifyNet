using GraphQLSharp;

namespace ShopifyNet;

public partial class GraphQLClient
{
    private readonly Func<int> _getRequestPriority;
    private readonly IInterceptor _tokenBucketWithRetryInterceptor;

    public GraphQLClient(ShopifyClientOptions defaultOptions, Func<int> getRequestPriority = null)
        : this(defaultOptions)
    {
        _getRequestPriority = getRequestPriority;
        _tokenBucketWithRetryInterceptor = new ChainedInterceptor(new TokenBucketInterceptor(_getRequestPriority),
                                                                  new RetryInterceptor());
    }


    protected override IInterceptor DefaultInterceptor => _tokenBucketWithRetryInterceptor;
}