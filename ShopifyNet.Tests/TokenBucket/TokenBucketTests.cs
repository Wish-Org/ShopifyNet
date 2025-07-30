using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class TokenBucketTests
{
    private TokenBucket _tokenBucket;
    private TestStopwatch _sinceTouched;
    private TestStopwatch _sinceLastUpdated;
    private int _currentPriority = 0;

    [TestInitialize]
    public void Initialize()
    {
        _sinceTouched = new TestStopwatch();
        _sinceLastUpdated = new TestStopwatch();
        _tokenBucket = new TokenBucket(
            100, // maximum tokens
            10,  // restore rate
            () => _currentPriority,
            _sinceTouched,
            _sinceLastUpdated
        );
    }

    [TestMethod]
    public void BucketRefillsUpToMax()
    {
        _tokenBucket.SetState(100, 10, 40);
        Assert.IsTrue(_tokenBucket.EstimatedCurrentlyAvailable == 40);
        _sinceLastUpdated.AdvanceSeconds(5);
        Assert.IsTrue(_tokenBucket.EstimatedCurrentlyAvailable == 90);
        _sinceLastUpdated.AdvanceSeconds(5);
        Assert.IsTrue(_tokenBucket.EstimatedCurrentlyAvailable == 100);
    }

    [TestMethod]
    public void SufficientTokensCompleteImmediately()
    {
        var waitTasks = Enumerable.Range(0, 10)
                                 .Select(_ => _tokenBucket.WaitForAvailableAsync(100))
                                 .ToList();
        Thread.Sleep(100);
        Assert.IsTrue(waitTasks.All(t => t.IsCompletedSuccessfully));
    }
}