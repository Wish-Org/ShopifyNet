namespace ShopifyNet;

internal class TestStopwatch : IStopwatch
{
    private TimeSpan _elapsed;
    public void Start() => _elapsed = TimeSpan.Zero;
    public void Stop() { }
    public void Reset() => _elapsed = TimeSpan.Zero;
    public void Restart() => _elapsed = TimeSpan.Zero;
    public void Advance(TimeSpan time) => _elapsed += time;
    public void AdvanceSeconds(double seconds) => Advance(TimeSpan.FromSeconds(seconds));
    public TimeSpan Elapsed => _elapsed;
    public bool IsRunning { get; set; }
}
