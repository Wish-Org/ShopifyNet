using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyOAuthValidationTests
{
    private readonly ShopifyOAuthClient _client = new("client-id", "client-secret");

    private static Dictionary<string, string> ValidQuery() => new()
    {
        ["timestamp"] = "1792713600",
        ["state"] = "nonce",
        ["shop"] = "test-shop.myshopify.com",
        ["host"] = "YWRtaW4=",
        ["code"] = "authorization-code",
        // Independently computed HMAC-SHA256 over alphabetically sorted, decoded pairs.
        ["hmac"] = "eda4de7d2e9914fbc39294ca2ed6767d63a543c13a0f2fd54a9cee07cbeb3188",
    };

    [DataTestMethod]
    [DataRow("test-shop.myshopify.com", true)]
    [DataRow("SHOP-123.MYSHOPIFY.COM", true)]
    [DataRow("a.myshopify.com", true)]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("evil.com", false)]
    [DataRow("myshopify.com", false)]
    [DataRow("shop.myshopify.com.evil.com", false)]
    [DataRow("sub.shop.myshopify.com", false)]
    [DataRow("https://shop.myshopify.com", false)]
    [DataRow("shop.myshopify.com:443", false)]
    [DataRow("shop.myshopify.com/", false)]
    [DataRow("shop.myshopify.com\n", false)]
    [DataRow("shop.myshopify.com.", false)]
    [DataRow("-shop.myshopify.com", false)]
    [DataRow("shop-.myshopify.com", false)]
    [DataRow("shop_name.myshopify.com", false)]
    [DataRow("shop@evil.com", false)]
    public void ValidatesShopDomain(string domain, bool valid)
    {
        Assert.AreEqual(valid, ShopifyOAuthClient.IsValidShopDomain(domain));
    }

    [TestMethod]
    public async Task RejectsUntrustedDomainBeforeSendingCredentials()
    {
        var sent = false;
        var client = new ShopifyOAuthClient("client-id", "client-secret", new TestHttpClient(_ =>
        {
            sent = true;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RefreshOfflineAccessTokenAsync("evil.com", "refresh"));
        Assert.IsFalse(sent);
    }

    [TestMethod]
    public void AcceptsValidHmacRegardlessOfParameterOrder()
    {
        Assert.IsTrue(_client.IsValidHmac(ValidQuery()));
        Assert.IsTrue(_client.IsValidHmac(ValidQuery().Reverse()));
    }

    [TestMethod]
    public void UsesDecodedParameterValues()
    {
        var query = ValidQuery();
        query.Remove("host");
        query["code"] = "a+b %/=";
        query["hmac"] = "abe180ca6bca63040a04a74f4f4b2a7534a091d2792e09539d606f8acb84f8c6";
        Assert.IsTrue(_client.IsValidHmac(query));
    }

    [DataTestMethod]
    [DataRow("code")]
    [DataRow("shop")]
    [DataRow("state")]
    [DataRow("timestamp")]
    [DataRow("host")]
    [DataRow("extra")]
    public void RejectsTamperedParameters(string parameter)
    {
        var query = ValidQuery();
        query[parameter] = "tampered";
        Assert.IsFalse(_client.IsValidHmac(query));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("invalid")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void RejectsMalformedOrIncorrectHmac(string hmac)
    {
        var query = ValidQuery();
        query["hmac"] = hmac;
        Assert.IsFalse(_client.IsValidHmac(query));
    }

    [TestMethod]
    public void RejectsMissingHmacAndWrongSecret()
    {
        Assert.IsFalse(new ShopifyOAuthClient("client-id", "wrong-secret").IsValidHmac(ValidQuery()));
        var query = ValidQuery();
        query.Remove("hmac");
        Assert.IsFalse(_client.IsValidHmac(query));
        Assert.IsFalse(_client.IsValidHmac(null));
    }

    [DataTestMethod]
    [DataRow("hmac")]
    [DataRow("shop")]
    public void RejectsDuplicateParameters(string key)
    {
        var query = ValidQuery();
        Assert.IsFalse(_client.IsValidHmac(query.Append(new KeyValuePair<string, string>(key, query[key]))));
    }

    [TestMethod]
    public void RejectsAmbiguousQueryDelimiters()
    {
        var query = ValidQuery();
        query.Remove("host");
        query["code"] = "authorization-code&host=YWRtaW4=";
        Assert.IsFalse(_client.IsValidHmac(query));

        query.Remove("code");
        query["code=authorization-code&host"] = "YWRtaW4=";
        Assert.IsFalse(_client.IsValidHmac(query));
    }
}
