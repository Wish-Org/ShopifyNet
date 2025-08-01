using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class TokenBucketTests
{
    private TokenBucket _tokenBucket;
    private TestStopwatch _sinceLastUpdated;

    [TestInitialize]
    public void Initialize()
    {
        _sinceLastUpdated = new TestStopwatch();
        _tokenBucket = new TokenBucket(
            100, // maximum tokens
            10,  // restore rate
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
                                 .Select(_ => _tokenBucket.WaitForAvailableAsync(10))
                                 .ToList();
        Thread.Sleep(100);
        Assert.IsTrue(waitTasks.All(t => t.IsCompletedSuccessfully));
        var task = _tokenBucket.WaitForAvailableAsync(20);
        Assert.IsFalse(task.IsCompletedSuccessfully);
        _sinceLastUpdated.AdvanceSeconds(2);
        Thread.Sleep(2000 + 200);// add buffer time
        Assert.IsTrue(task.IsCompletedSuccessfully);
    }

    [TestMethod]
    public void SufficientTokensCompleteImmediatelyCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var waitTasks = Enumerable.Range(0, 10)
                                 .Select(_ => _tokenBucket.WaitForAvailableAsync(_tokenBucket.MaximumAllowed, 0, cts.Token))
                                 .ToList();
        Thread.Sleep(100);
        Assert.IsTrue(waitTasks.All(t => t.IsCanceled));
    }

    [TestMethod]
    public async Task IsIdleAfterAWhile()
    {
        _sinceLastUpdated.Advance(TimeSpan.FromHours(1));
        Assert.IsTrue(_tokenBucket.IsIdle);

        await _tokenBucket.WaitForAvailableAsync(1);
        Assert.IsFalse(_tokenBucket.IsIdle);

        _sinceLastUpdated.Advance(TimeSpan.FromHours(1));
        Assert.IsTrue(_tokenBucket.IsIdle);

        await _tokenBucket.WaitForAvailableAsync(1);
        Assert.IsFalse(_tokenBucket.IsIdle);
    }

    [TestMethod]
    public void CompletesInPriorityOrder()
    {
        //set available tokens to 0
        _tokenBucket.SetState(100, 100, 0);

        var list = new List<int>();
        //queue 10 requests with different priorities
        var waitTasks = Enumerable.Range(0, 10)
                                 .Select(async i =>
                                 {
                                     await _tokenBucket.WaitForAvailableAsync(10, -i);
                                     lock (list)
                                     {
                                         list.Add(i);
                                     }
                                 })
                                 .ToList();

        Assert.IsTrue(waitTasks.All(t => !t.IsCompleted));
        Enumerable.Range(0, 10).ForEach(i =>
        {
            _sinceLastUpdated.AdvanceSeconds(0.1);
            Thread.Sleep(150); // add buffer time
        });
        Assert.IsTrue(waitTasks.All(t => t.IsCompletedSuccessfully));
        Assert.IsTrue(list.SequenceEqual(Enumerable.Range(0, 10).OrderDescending()));
    }
}