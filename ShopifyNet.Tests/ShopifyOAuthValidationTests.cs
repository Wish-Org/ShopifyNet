using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyOAuthValidationTests
{
    private static Dictionary<string, string> ValidQuery() => new()
    {
        ["timestamp"] = "1792713600",
        ["state"] = "nonce",
        ["shop"] = "test-shop.myshopify.com",
        ["host"] = "YWRtaW4=",
        ["code"] = "authorization-code",
        // Synthetic fixture signed over sorted pairs, with the host padding left literal.
        ["hmac"] = "eda4de7d2e9914fbc39294ca2ed6767d63a543c13a0f2fd54a9cee07cbeb3188",
    };

    // Uppercase hex, unlike the lowercase fixtures, so hex parsing is covered in both cases.
    private static string Sign(string message) => Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes("client-secret"), System.Text.Encoding.UTF8.GetBytes(message)));

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
        var client = new ShopifyOAuthClient(new TestHttpClient(_ =>
        {
            sent = true;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RefreshOfflineAccessTokenAsync("client-id", "client-secret", "evil.com", "refresh"));
        Assert.IsFalse(sent);
    }

    [TestMethod]
    public void AcceptsValidHmacRegardlessOfParameterOrder()
    {
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(ValidQuery(), "client-secret"));
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(ValidQuery().Reverse(), "client-secret"));
    }

    [TestMethod]
    public void RejectsHmacSignedOverFormEncodedValues()
    {
        var query = ValidQuery();
        query["hmac"] = "27ec35edf2c692e2095a0e5f9970ff9b6ffe9acb06bbcf967bdfb1b0c500156d";
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }

    [DataTestMethod]
    [DataRow("one", "two", "ids=[\"one\", \"two\"]")]
    [DataRow("", "a&%=b", "ids=[\"\", \"a%26%25=b\"]")]
    public void SignsArrayParametersInOriginalOrder(string first, string second, string message)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("ids[]", first), new("hmac", Sign(message)), new("ids[]", second)
        };
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query.AsEnumerable().Reverse(), "client-secret"));
        query[0] = new("ids[]", "tampered");
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }

    [TestMethod]
    public void AcceptsSingleElementArray()
    {
        var query = new Dictionary<string, string> { ["ids[]"] = "one", ["hmac"] = Sign("ids=[\"one\"]") };
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
        query["ids"] = "one";
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
        query.Remove("ids");
        query["ids[]"] = null;
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }

    [TestMethod]
    public void EscapesPercentWithoutEncodingOtherValueCharacters()
    {
        var query = ValidQuery();
        query["code"] = "a+b %/=~*-._\u00e9";
        // Independently signed with Node crypto: only % becomes %25 in this value.
        query["hmac"] = "7af0f2f479cf4a675fde53aaaee665bd75ae9a97b9500899431004e798b391e1";
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }

    [TestMethod]
    public void AcceptsSignedDelimitersInKeysAndValues()
    {
        var query = ValidQuery();
        query["state"] = "nonce&extra=value";
        query["extra&="] = "+/= ~*\u00e9";
        // Escape & in keys and values, and = in keys only.
        query["hmac"] = "6eb51503735241f316dac1d982f9d5b8ebfdebac213afc8bff79bace79a8a5e1";
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }

    [TestMethod]
    public void PreservesJsonStateWhenSigning()
    {
        var query = ValidQuery();
        query["state"] = """{"ReturnUrl":null,"AccountId":"br-test-plus-mathieu.myshopify.com","UserId":null}""";
        // Synthetic signature using the reported JSON shape and the test secret, not a live app secret.
        query["hmac"] = "5b2b32d47faae8c483a532efcd4ebedf0c736103ad9e87b298475b60b9dee442";
        Assert.IsTrue(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
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
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
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
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }

    [TestMethod]
    public void RejectsMissingHmacAndWrongSecret()
    {
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(ValidQuery(), "wrong-secret"));
        var query = ValidQuery();
        query.Remove("hmac");
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(null, "client-secret"));
    }

    [DataTestMethod]
    [DataRow("hmac")]
    [DataRow("shop")]
    public void RejectsDuplicateParameters(string key)
    {
        var query = ValidQuery();
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query.Append(new KeyValuePair<string, string>(key, query[key])), "client-secret"));
    }

    [TestMethod]
    public void RejectsAmbiguousQueryDelimiters()
    {
        var query = ValidQuery();
        query.Remove("host");
        query["code"] = "authorization-code&host=YWRtaW4=";
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));

        query.Remove("code");
        query["code=authorization-code&host"] = "YWRtaW4=";
        Assert.IsFalse(ShopifyOAuthClient.IsValidHmac(query, "client-secret"));
    }
}
