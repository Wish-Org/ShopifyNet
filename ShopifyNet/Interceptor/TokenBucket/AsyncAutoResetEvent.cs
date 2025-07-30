namespace ShopifyNet;

//https://devblogs.microsoft.com/dotnet/building-async-coordination-primitives-part-1-asyncmanualresetevent/
internal class AsyncManualResetEvent
{
    private volatile TaskCompletionSource _tcs = CreateTcs();

    private static TaskCompletionSource CreateTcs() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync()
    {
        return _tcs.Task;
    }

    public void Set()
    {
        _tcs.TrySetResult();
    }

    public void Reset()
    {
        while (true)
        {
            if (!_tcs.Task.IsCompleted)
                return;

            //take a snapshot of the current TCS
            var existingTcs = _tcs;

            //create a new TCS to replace the old one
            var newTcs = CreateTcs();

            //exchange atomatically such that it is impossible for multiple threads both assign a new TCS
            if (Interlocked.CompareExchange(ref _tcs, newTcs, existingTcs) == existingTcs)
                return;
        }
    }

    public bool IsSet => _tcs.Task.IsCompleted;
}
