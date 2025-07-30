namespace ShopifyNet;

internal class TokenBucket
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    private readonly IStopwatch _sinceTouched;

    private readonly IStopwatch _sinceUpdated;

    private int MaximumAllowed { get; set; }

    private int RestoreRatePerSecond { get; set; }

    private decimal _lastCurrentlyAvailable;
    private decimal LastCurrentlyAvailable
    {
        get => _lastCurrentlyAvailable;
        set
        {
            _lastCurrentlyAvailable = value;
            _sinceUpdated.Restart();
            _sinceTouched.Restart();
        }
    }

    public decimal EstimatedCurrentlyAvailable
    {
        get
        {
            lock (_lock)
            {
                return Math.Min(MaximumAllowed,
                                LastCurrentlyAvailable + ((decimal)_sinceUpdated.Elapsed.TotalSeconds * RestoreRatePerSecond)
                );
            }
        }
    }

    internal bool IsIdle
    {
        get
        {
            lock (_lock)
            {
                return EstimatedCurrentlyAvailable == MaximumAllowed &&
                                    _sinceTouched.Elapsed > IdleTimeout &&
                                    _waitingRequests.Count == 0;
            }
        }
    }

    private readonly Func<int> _getCurrentPriority;
    private readonly PriorityQueue<TokenBucketRequest, int> _waitingRequests = new();

    private readonly Lock _lock = new();

    internal TokenBucket(int maximumAvailable, int restoreRatePerSecond, Func<int> getCurrentPriority)
                         : this(maximumAvailable, restoreRatePerSecond, getCurrentPriority, new Stopwatch(), new Stopwatch())
    {
    }

    internal TokenBucket(int maximumAvailable, int restoreRatePerSecond, Func<int> getCurrentPriority, IStopwatch sinceTouched, IStopwatch sinceUpdated)
    {
        _sinceTouched = sinceTouched ?? throw new ArgumentNullException(nameof(sinceTouched));
        _sinceUpdated = sinceUpdated ?? throw new ArgumentNullException(nameof(sinceUpdated));
        _getCurrentPriority = getCurrentPriority;
        SetState(maximumAvailable, restoreRatePerSecond, maximumAvailable);
    }

    internal void Touch()
    {
        lock (_lock)
        {
            _sinceTouched.Restart();
        }
    }

    public void SetState(int maximumAvailable, int restoreRatePerSecond, decimal currentlyAvailable)
    {
        if (maximumAvailable <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(maximumAvailable)} must be greater than zero");

        if (currentlyAvailable < 0)
            throw new ArgumentOutOfRangeException($"{nameof(currentlyAvailable)} must be positive or zero.");

        if (restoreRatePerSecond <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(restoreRatePerSecond)} must be greater than zero");

        if (currentlyAvailable > maximumAvailable)
            throw new ArgumentOutOfRangeException($"{nameof(currentlyAvailable)} must not be greater than {nameof(maximumAvailable)}");

        lock (_lock)
        {
            MaximumAllowed = maximumAvailable;
            RestoreRatePerSecond = restoreRatePerSecond;
            LastCurrentlyAvailable = currentlyAvailable;
        }
        ReleaseRequests();
    }

    private void ConsumeTokens(int cost)
    {
        lock (_lock)
        {
            LastCurrentlyAvailable = Math.Max(0, this.EstimatedCurrentlyAvailable - cost);
        }
    }

    public async Task WaitForAvailableAsync(int requestCost, CancellationToken cancellationToken = default)
    {
        if (requestCost <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(requestCost)} must be greater than zero");
        if (requestCost > MaximumAllowed)
            throw new ArgumentOutOfRangeException($"Requested query cost of {requestCost} is larger than maximum available {MaximumAllowed}");

        Touch();

        using var r = new TokenBucketRequest(requestCost, cancellationToken);

        lock (_lock)
        {
            if (EstimatedCurrentlyAvailable >= requestCost && _waitingRequests.Count == 0)
            {
                //there is enough capacity to proceed immediately
                ConsumeTokens(r.Cost);
                return;
            }

            //otherwise, we queue the request
            _waitingRequests.Enqueue(r, _getCurrentPriority());

            //if it's the first queued request, we schedule it to be released
            if (_waitingRequests.Count == 1)
                _ = ScheduleRequest(r);
        }

        //TaskCanceledException can bubble up
        //The request will be dequeued when the semaphore is released
        await r.WaitAsync(cancellationToken);
        Touch();
    }

    private async Task ScheduleRequest(TokenBucketRequest r)
    {
        using var sub = r.CancellationToken.Register(ReleaseRequests);
        TimeSpan waitFor;
        lock (_lock)
        {
            waitFor = TimeSpan.FromSeconds((double)Math.Max(0, (r.Cost - EstimatedCurrentlyAvailable) / RestoreRatePerSecond));
        }
        try
        {
            r.IsScheduled = true;
            await Task.Delay(waitFor, r.CancellationToken);
            ReleaseRequests();
        }
        finally
        {
            //note that because the queue is a priority queue so the request may not be at the front anymore
            //and might need to be rescheduled
            r.IsScheduled = false;
        }
    }

    private void ReleaseRequests()
    {
        lock (_lock)
        {
            while (_waitingRequests.Count > 0)
            {
                var req = _waitingRequests.Peek();

                if (req.CancellationToken.IsCancellationRequested)
                {
                    _waitingRequests.Dequeue();
                    req.Release();
                }
                else if (EstimatedCurrentlyAvailable >= req.Cost)
                {
                    // Release the request
                    _waitingRequests.Dequeue();
                    req.Release();
                    ConsumeTokens(req.Cost);
                }
                else
                {
                    // Not enough capacity, exit the loop
                    break;
                }
            }

            if (_waitingRequests.Count > 0)
            {
                var nextRequest = _waitingRequests.Peek();
                if (!nextRequest.IsScheduled)
                    _ = ScheduleRequest(nextRequest);
            }
        }
    }
}
