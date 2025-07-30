namespace ShopifyNet;

internal interface IStopwatch
{
    void Start();
    void Stop();
    void Reset();
    void Restart();
    TimeSpan Elapsed { get; }
    bool IsRunning { get; }
}

internal class Stopwatch : IStopwatch
{
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    public void Start() => _stopwatch.Start();
    public void Stop() => _stopwatch.Stop();
    public void Reset() => _stopwatch.Reset();
    public void Restart() => _stopwatch.Restart();
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public bool IsRunning => _stopwatch.IsRunning;
}
