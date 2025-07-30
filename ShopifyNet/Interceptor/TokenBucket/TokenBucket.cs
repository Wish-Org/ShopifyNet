namespace ShopifyNet;

internal class TokenBucket
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private readonly AsyncManualResetEvent _processSignal = new();
    private readonly PriorityQueue<TokenBucketRequest, int> _queue = new();

    private readonly IStopwatch _sinceTouched;

    private readonly IStopwatch _sinceUpdated;

    internal int MaximumAllowed { get; set; }

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
                                    _queue.Count == 0;
            }
        }
    }

    private readonly Lock _lock = new();

    internal TokenBucket(int maximumAvailable, int restoreRatePerSecond)
                         : this(maximumAvailable, restoreRatePerSecond, new Stopwatch(), new Stopwatch())
    {
    }

    internal TokenBucket(int maximumAvailable, int restoreRatePerSecond, IStopwatch sinceTouched, IStopwatch sinceUpdated)
    {
        _sinceTouched = sinceTouched ?? throw new ArgumentNullException(nameof(sinceTouched));
        _sinceUpdated = sinceUpdated ?? throw new ArgumentNullException(nameof(sinceUpdated));
        SetState(maximumAvailable, restoreRatePerSecond, maximumAvailable);
        _ = ThrottleQueue();
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
        _processSignal.Set();
    }

    /// <summary>
    /// Waits for available tokens in the bucket.
    /// </summary>
    /// <param name="priority">Priority of the request. Lower values are processed first</param>
    public async Task WaitForAvailableAsync(int requestCost, int priority = int.MaxValue, CancellationToken cancellationToken = default)
    {
        if (requestCost <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(requestCost)} must be greater than zero");
        if (requestCost > MaximumAllowed)
            throw new ArgumentOutOfRangeException($"Requested query cost of {requestCost} is larger than maximum available {MaximumAllowed}");

        using var r = new TokenBucketRequest(requestCost, cancellationToken);
        lock (_lock)
        {
            Touch();
            _queue.Enqueue(r, priority);
        }
        _processSignal.Set();
        await r.WaitAsync(cancellationToken);
        Touch();
    }

    private async Task ThrottleQueue()
    {
        TokenBucketRequest req;
        TimeSpan waitFor;

        while (true)
        {
            lock (_lock)
            {
                if (!_queue.TryPeek(out req, out _))
                    waitFor = Timeout.InfiniteTimeSpan;
                else if (req.CancellationToken.IsCancellationRequested)
                {
                    // Release cancelled request without consuming tokens
                    _queue.Dequeue();
                    req.Release();
                    continue;
                }
                else if (EstimatedCurrentlyAvailable >= req.Cost)
                {
                    // Release the request for processing
                    _queue.Dequeue();
                    LastCurrentlyAvailable = Math.Max(0, this.EstimatedCurrentlyAvailable - req.Cost);
                    req.Release();
                    continue;
                }
                else
                    waitFor = TimeSpan.FromSeconds((double)Math.Max(0, (req.Cost - EstimatedCurrentlyAvailable) / RestoreRatePerSecond));
            }
            using var sub = req?.CancellationToken.Register(_processSignal.Set);
            await Task.WhenAny(Task.Delay(waitFor), _processSignal.WaitAsync());
            _processSignal.Reset();
        }
    }
}
