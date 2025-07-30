namespace ShopifyNet;

internal class TokenBucketRequest(int cost, CancellationToken cancellationToken) : IDisposable
{
    public readonly int Cost = cost;

    public readonly CancellationToken CancellationToken = cancellationToken;

    private bool _IsDisposed;

    private readonly SemaphoreSlim _Semaphore = new(0, 1);

    public void Dispose()
    {
        if (_IsDisposed)
            return;

        _Semaphore.Dispose();
        _IsDisposed = true;
    }

    public void Release()
    {
        if (!_IsDisposed)
            _Semaphore.Release();
    }

    public Task WaitAsync(CancellationToken t)
    {
        return _Semaphore.WaitAsync(t);
    }
}
