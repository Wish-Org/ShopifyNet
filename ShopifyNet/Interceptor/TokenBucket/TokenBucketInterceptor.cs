using System.Collections.Concurrent;
using GraphQLSharp;

namespace ShopifyNet;

//Note: cannot use System.Threading.RateLimiting.PartitionedRateLimiter with TokenBucketLimiter 
//because it doesn't support any priority aware queue and it is not suited to updated the options dynamically
//based on the response from the API
//It is designed for cases where we own the bucket but in our case the bucket is owned by Shopify
public class TokenBucketInterceptor : IInterceptor
{
    private static readonly TimeSpan THROTTLED_RETRY_DELAY = TimeSpan.FromSeconds(1);
    private static readonly int MAX_ATTEMPTS = 3;
    private readonly ConcurrentDictionary<string, TokenBucket> _tokenToBucket = new();
    private readonly IStopwatch _timeSinceLastIdleBucketCheck = new Stopwatch();
    private readonly Func<ShopifyGraphQLRequest, int> _getRequestPriority;
    private const int DEFAULT_GRAPHQL_MAX_AVAILABLE = 1_000;
    private const int DEFAULT_GRAPHQL_RESTORE_RATE = 50;
    private const int DEFAULT_GRAPHQL_UNKNOWN_COST = 50;

    public TokenBucketInterceptor(Func<ShopifyGraphQLRequest, int> getRequestPriority = null)
    {
        _getRequestPriority = getRequestPriority ?? (r => 0);
        _timeSinceLastIdleBucketCheck.Start();
    }

    private void RemoveIdleBucketsAsync()
    {
        if (_timeSinceLastIdleBucketCheck.Elapsed.TotalMinutes > 5)
        {
            _timeSinceLastIdleBucketCheck.Restart();
            _ = Task.Run(() =>
            {
                var idleBuckets = _tokenToBucket.Where(kv => kv.Value.IsIdle).ToArray();
                if (idleBuckets.Any())
                {
                    lock (_tokenToBucket)
                    {
                        //lock to ensure another thread doesn't pull one of the idle token at the same time as we remove it
                        foreach (var t in idleBuckets)
                        {
                            //the token may have turned non-idle just befor the lock was taken
                            if (t.Value.IsIdle)
                                _tokenToBucket.TryRemove(t);
                        }
                    }
                }
            });
        }
    }

    public async Task<GraphQLResponse<TData>> InterceptRequestAsync<TGraphQLRequest, TClientOptions, TData>(
        TGraphQLRequest request,
        TClientOptions options,
        CancellationToken cancellationToken,
        Func<TGraphQLRequest, CancellationToken, Task<GraphQLResponse<TData>>> executeAsync)
        where TGraphQLRequest : GraphQLRequest
        where TClientOptions : IGraphQLClientOptions
    {
        var r = request as ShopifyGraphQLRequest;
        var token = (options as ShopifyClientOptions).AccessToken ?? throw new ArgumentNullException(nameof(ShopifyClientOptions.AccessToken));
        TokenBucket bucket = null;
        lock (_tokenToBucket)
        {
            bucket = _tokenToBucket.GetOrAdd(token, t => new TokenBucket(DEFAULT_GRAPHQL_MAX_AVAILABLE, DEFAULT_GRAPHQL_RESTORE_RATE));
        }

        this.RemoveIdleBucketsAsync();

        var requestQueryCost = r.Cost ?? DEFAULT_GRAPHQL_UNKNOWN_COST;
        int attempt = 0;
        while (true) //try up to 3 times if throttled
        {
            attempt++;
            await bucket.WaitForAvailableAsync(requestQueryCost, _getRequestPriority(r), cancellationToken);

            GraphQLResponse<TData> res = null;
            try
            {
                //may throw GraphQLErrorsException if options.ThrowOnGraphQLErrors is true
                res = await executeAsync(request, cancellationToken);
            }
            catch (GraphQLErrorsException ex) when (attempt < MAX_ATTEMPTS && (ex.response as GraphQLResponse<TData>).IsThrottled())
            {
            }

            var cost = res.GetCost();
            //cost.actualQueryCost may be null if the query was throttled
            var actualQueryCost = cost.actualQueryCost ?? requestQueryCost;
            var refund = requestQueryCost - actualQueryCost;//may be negative if user supplied wrong or null cost
            var max = cost.throttleStatus.maximumAvailable;
            ////There seems to be a bug in the GraphQL API. It sometimes returns a currentlyAvailable larger than maximumAvailable.
            var currentlyAvailable = decimal.Clamp(cost.throttleStatus.currentlyAvailable, 0, max);
            //API returns the currentlyAvailable quantity but it doesn't consider requests that we have fired in the meantime 
            //so we compute our own estimation and take the minimum of the two
            var estimatedCurrentlyAvailable = decimal.Clamp(bucket.EstimatedCurrentlyAvailable + refund, 0, max);
            var newCurrentlyAvailable = Math.Min(currentlyAvailable, estimatedCurrentlyAvailable);
            bucket.SetState((int)max, (int)cost.throttleStatus.restoreRate, newCurrentlyAvailable);

            //The user might have supplied no cost or an invalid cost
            //We fix the query cost so the correct value is used if a retry is needed
            requestQueryCost = cost.requestedQueryCost;

            //if options.ThrowOnGraphQLErrors is false then no exception is thrown
            //but we don't try more than MAX_ATTEMPTS and return the response even it is throttled
            if (res.IsThrottled() && attempt < MAX_ATTEMPTS)//TODO log unexpected throttling
                await Task.Delay(THROTTLED_RETRY_DELAY, cancellationToken);
            else
                return res;
        }
    }
}
