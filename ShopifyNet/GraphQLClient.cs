using GraphQLSharp;

namespace ShopifyNet;

public partial class GraphQLClient
{
    private readonly Func<int> _getRequestPriority;
    private readonly IInterceptor _tokenBucketWithRetryInterceptor;

    public GraphQLClient(ShopifyClientOptions defaultOptions = null, Func<int> getRequestPriority = null)
        : this(defaultOptions)
    {
        _getRequestPriority = getRequestPriority;
        _tokenBucketWithRetryInterceptor = new ChainedInterceptor(new TokenBucketInterceptor(_getRequestPriority),
                                                                  new RetryInterceptor());
    }


    protected override IInterceptor DefaultInterceptor => _tokenBucketWithRetryInterceptor;

    protected override void ValidateOptions(ShopifyClientOptions defaultOptions, ShopifyClientOptions options)
    {
        _ = defaultOptions?.MyShopifyDomain ?? options?.MyShopifyDomain ?? throw new ArgumentNullException(nameof(options.MyShopifyDomain));
        _ = defaultOptions?.AccessToken ?? options?.AccessToken ?? throw new ArgumentNullException(nameof(options.AccessToken));
    }
}