using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyAuthorizationUrlTests
{
    private readonly ShopifyOAuthClient _client = new("client+id", "secret-never-in-url");

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BuildsEncodedAuthorizationUrl(bool online)
    {
        const string redirectUri = "https://app.example/callback?next=%2Forders&source=shopify";
        var url = _client.BuildAuthorizationUrl("test-shop.myshopify.com", "read_products,write_orders", redirectUri, "nonce+/= %", online);
        var uri = new Uri(url);
        var query = HttpUtility.ParseQueryString(uri.Query);

        Assert.AreEqual("https", uri.Scheme);
        Assert.AreEqual("test-shop.myshopify.com", uri.Host);
        Assert.AreEqual("/admin/oauth/authorize", uri.AbsolutePath);
        Assert.AreEqual("", uri.Fragment);
        Assert.AreEqual("client+id", query["client_id"]);
        Assert.AreEqual("read_products,write_orders", query["scope"]);
        Assert.AreEqual(redirectUri, query["redirect_uri"]);
        Assert.AreEqual("nonce+/= %", query["state"]);
        Assert.AreEqual(online ? "per-user" : null, query["grant_options[]"]);
        Assert.AreEqual(online ? 5 : 4, query.Count);
        Assert.IsFalse(url.Contains("secret-never-in-url"));
    }

    [TestMethod]
    public void DefaultsToOfflineAccess()
    {
        var url = _client.BuildAuthorizationUrl("shop.myshopify.com", "read_products", "http://localhost:3000/callback", "nonce");
        Assert.IsNull(HttpUtility.ParseQueryString(new Uri(url).Query)["grant_options[]"]);
    }

    [DataTestMethod]
    [DataRow("evil.com")]
    [DataRow("shop.myshopify.com.evil.com")]
    [DataRow("https://shop.myshopify.com")]
    public void RejectsInvalidShop(string shop)
    {
        Assert.ThrowsException<ArgumentException>(() => _client.BuildAuthorizationUrl(shop, "read_products", "https://app.example/callback", "nonce"));
    }

    [DataTestMethod]
    [DataRow("/callback")]
    [DataRow("javascript:alert(1)")]
    [DataRow("https://app.example/callback#fragment")]
    [DataRow("https://user:password@app.example/callback")]
    public void RejectsInvalidRedirectUri(string redirectUri)
    {
        Assert.ThrowsException<ArgumentException>(() => _client.BuildAuthorizationUrl("shop.myshopify.com", "read_products", redirectUri, "nonce"));
    }

    [TestMethod]
    public void RequiresScopesRedirectAndState()
    {
        Assert.ThrowsException<ArgumentNullException>(() => _client.BuildAuthorizationUrl("shop.myshopify.com", null, "https://app.example/callback", "nonce"));
        Assert.ThrowsException<ArgumentException>(() => _client.BuildAuthorizationUrl("shop.myshopify.com", " ", "https://app.example/callback", "nonce"));
        Assert.ThrowsException<ArgumentNullException>(() => _client.BuildAuthorizationUrl("shop.myshopify.com", "read_products", null, "nonce"));
        Assert.ThrowsException<ArgumentException>(() => _client.BuildAuthorizationUrl("shop.myshopify.com", "read_products", "https://app.example/callback", ""));
        Assert.ThrowsException<ArgumentNullException>(() => _client.BuildAuthorizationUrl("shop.myshopify.com", "read_products", "https://app.example/callback", null));
    }

    [TestMethod]
    public void EncodesStateContainingQueryDelimiters()
    {
        var url = _client.BuildAuthorizationUrl("shop.myshopify.com", "read_products", "https://app.example/callback", "nonce&extra=value");
        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.AreEqual("nonce&extra=value", query["state"]);
        Assert.IsNull(query["extra"]);
    }
}
