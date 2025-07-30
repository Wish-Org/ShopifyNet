using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class TokenBucketInterceptorTests
{
    private TokenBucketInterceptor _interceptor;

    [TestInitialize]
    public void Initialize()
    {
        _interceptor = new TokenBucketInterceptor();
    }

    [TestMethod]
    public void BucketRefillsUpToMax()
    {
    }
}