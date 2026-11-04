using System.Net;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyOAuthCredentialsTests
{
    [DataTestMethod]
    [DataRow("migration")]
    [DataRow("refresh")]
    [DataRow("offline-id")]
    [DataRow("online-id")]
    [DataRow("offline-code")]
    [DataRow("online-code")]
    public async Task UsesCredentialsFromEachCall(string flow)
    {
        var credentials = new[] { (Id: "app-a", Secret: "secret-a"), (Id: "app-b", Secret: "secret-b") };
        var requestCount = 0;
        using var http = new TestHttpClient(request =>
        {
            var form = HttpUtility.ParseQueryString(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var expected = credentials[requestCount++];
            Assert.AreEqual(expected.Id, form["client_id"]);
            Assert.IsTrue(expected.Secret == form["client_secret"], "Request must use this call's secret.");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token","expires_in":3600,"refresh_token":"refresh","refresh_token_expires_in":86400}""")
            };
        });
        var client = new ShopifyOAuthClient(http);

        foreach (var (id, secret) in credentials)
        {
            const string shop = "test.myshopify.com";
            Task request = flow switch
            {
                "migration" => client.MigrateToExpiringOfflineAccessTokenAsync(id, secret, shop, "old-token"),
                "refresh" => client.RefreshOfflineAccessTokenAsync(id, secret, shop, "refresh"),
                "offline-id" => client.ExchangeIdTokenForOfflineAccessTokenAsync(id, secret, shop, "id-token"),
                "online-id" => client.ExchangeIdTokenForOnlineAccessTokenAsync(id, secret, shop, "id-token"),
                "offline-code" => client.ExchangeCodeForOfflineAccessTokenAsync(id, secret, shop, "code", expiring: true),
                "online-code" => client.ExchangeCodeForOnlineAccessTokenAsync(id, secret, shop, "code"),
                _ => throw new AssertFailedException("Unknown flow.")
            };
            await request;
        }

        Assert.AreEqual(credentials.Length, requestCount);
    }

    [DataTestMethod]
    [DataRow(null, "secret", "clientId")]
    [DataRow("client-id", null, "clientSecret")]
    public async Task RejectsMissingCredentialsBeforeSending(string clientId, string clientSecret, string parameter)
    {
        using var http = new TestHttpClient(_ => throw new AssertFailedException("Missing credentials must not be sent."));
        var client = new ShopifyOAuthClient(http);
        var exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
            client.RefreshOfflineAccessTokenAsync(clientId, clientSecret, "test.myshopify.com", "refresh"));
        Assert.AreEqual(parameter, exception.ParamName);
    }

    [TestMethod]
    public void StaticHelpersRejectMissingCredentials()
    {
        var idError = Assert.ThrowsException<ArgumentNullException>(() =>
            ShopifyOAuthClient.BuildAuthorizationUrl(null, "test.myshopify.com", "read_products", "https://app.example/callback", "nonce"));
        Assert.AreEqual("clientId", idError.ParamName);
        var secretError = Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.IsValidHmac([], null));
        Assert.AreEqual("clientSecret", secretError.ParamName);
    }
}
