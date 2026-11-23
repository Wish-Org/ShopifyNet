using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyAuthorizationUrlTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BuildsEncodedAuthorizationUrl(bool online)
    {
        const string redirectUri = "https://app.example/callback?next=%2Forders&source=shopify";
        var url = ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "test-shop.myshopify.com", ["read_products", "write_orders"], redirectUri, "nonce+/= %", online);
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
        var url = ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products"], "http://localhost:3000/callback", "nonce");
        Assert.IsNull(HttpUtility.ParseQueryString(new Uri(url).Query)["grant_options[]"]);
    }

    [DataTestMethod]
    [DataRow("admin.shopify.com")]
    [DataRow("ADMIN.SHOPIFY.COM")]
    public void AcceptsAdminDomainForStoreSelection(string domain)
    {
        var url = ShopifyOAuthClient.BuildAuthorizationUrl("client+id", domain, ["read_products"], "https://app.example/callback", "nonce");
        StringAssert.StartsWith(url, $"https://{domain}/admin/oauth/authorize?");
    }

    [DataTestMethod]
    [DataRow("evil.com")]
    [DataRow("shop.myshopify.com.evil.com")]
    [DataRow("https://shop.myshopify.com")]
    [DataRow("shopify.com")]
    [DataRow("evil.shopify.com")]
    [DataRow("sub.admin.shopify.com")]
    [DataRow("admin.shopify.com.evil.com")]
    [DataRow("admin.shopify.com.")]
    [DataRow("admin.shopify.com/store/shop")]
    public void RejectsInvalidShop(string shop)
    {
        Assert.ThrowsException<ArgumentException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", shop, ["read_products"], "https://app.example/callback", "nonce"));
    }

    [DataTestMethod]
    [DataRow("/callback")]
    [DataRow("javascript:alert(1)")]
    [DataRow("https://app.example/callback#fragment")]
    [DataRow("https://user:password@app.example/callback")]
    public void RejectsInvalidRedirectUri(string redirectUri)
    {
        Assert.ThrowsException<ArgumentException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products"], redirectUri, "nonce"));
    }

    [TestMethod]
    public void RequiresScopesRedirectAndState()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", null, "https://app.example/callback", "nonce"));
        Assert.ThrowsException<ArgumentException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", [" "], "https://app.example/callback", "nonce"));
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products"], null, "nonce"));
        Assert.ThrowsException<ArgumentException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products"], "https://app.example/callback", ""));
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products"], "https://app.example/callback", null));
    }

    [TestMethod]
    public void JoinsScopeListWithCommas()
    {
        var url = ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", new List<string> { "read_products", "write_orders", "read_customers" }, "https://app.example/callback", "nonce");
        var uri = new Uri(url);
        Assert.AreEqual("read_products,write_orders,read_customers", HttpUtility.ParseQueryString(uri.Query)["scope"]);
        StringAssert.Contains(uri.Query, "scope=read_products%2Cwrite_orders%2Cread_customers");
    }

    [TestMethod]
    public void RejectsEmptyScopeList()
    {
        var error = Assert.ThrowsException<ArgumentException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", [], "https://app.example/callback", "nonce"));
        Assert.AreEqual("scopeList", error.ParamName);
    }

    [TestMethod]
    public void RejectsNullScopeListWithParameterName()
    {
        var error = Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", null, "https://app.example/callback", "nonce"));
        Assert.AreEqual("scopeList", error.ParamName);
    }

    [TestMethod]
    public void ValidatesClientIdBeforeScopeList()
    {
        var error = Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.BuildAuthorizationUrl(null, "shop.myshopify.com", null, "https://app.example/callback", "nonce"));
        Assert.AreEqual("clientId", error.ParamName);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(null)]
    public void RejectsBlankScopeInList(string blank)
    {
        var error = Assert.ThrowsException<ArgumentException>(() => ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products", blank], "https://app.example/callback", "nonce"));
        Assert.AreEqual("scopeList", error.ParamName);
    }

    [TestMethod]
    public void EnumeratesScopeListOnce()
    {
        var enumerations = 0;
        IEnumerable<string> Scopes()
        {
            enumerations++;
            yield return "read_products";
            yield return "write_orders";
        }

        var url = ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", Scopes(), "https://app.example/callback", "nonce");
        Assert.AreEqual("read_products,write_orders", HttpUtility.ParseQueryString(new Uri(url).Query)["scope"]);
        Assert.AreEqual(1, enumerations);
    }

    [TestMethod]
    public void EncodesStateContainingQueryDelimiters()
    {
        var url = ShopifyOAuthClient.BuildAuthorizationUrl("client+id", "shop.myshopify.com", ["read_products"], "https://app.example/callback", "nonce&extra=value");
        var query = HttpUtility.ParseQueryString(new Uri(url).Query);
        Assert.AreEqual("nonce&extra=value", query["state"]);
        Assert.IsNull(query["extra"]);
    }
}
