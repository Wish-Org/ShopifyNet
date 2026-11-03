using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

// Uses a registered public JWK and its matching private key against an installed development store.
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public class ShopifyClientAssertionIntegrationTests
{
    [TestMethod]
    public async Task RequestClientAssertionAccessTokenAsync_ReturnsExpiringToken()
    {
        if (GetEnvironmentVariable("SHOPIFYNET_RUN_OAUTH_INTEGRATION_TESTS") != "1")
            Assert.Inconclusive("Set SHOPIFYNET_RUN_OAUTH_INTEGRATION_TESTS=1 to enable live OAuth tests.");

        var shop = RequireEnvironmentVariable("SHOPIFYNET_SHOP_ID");
        Assert.IsTrue(ShopifyOAuthClient.IsValidShopDomain(shop), "SHOPIFYNET_SHOP_ID must be a plain store.myshopify.com hostname.");
        var clientId = RequireEnvironmentVariable("SHOPIFYNET_CLIENT_ID");
        var keyId = RequireEnvironmentVariable("SHOPIFYNET_KEY_ID");
        var privateKeyPath = RequireEnvironmentVariable("SHOPIFYNET_PRIVATE_KEY_PATH");
        var privateKeyPem = await File.ReadAllTextAsync(privateKeyPath);

        // Application-only authentication does not use the client secret.
        var client = new ShopifyOAuthClient(clientId, clientSecret: string.Empty);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = await client.RequestClientAssertionAccessTokenAsync(shop, keyId, privateKeyPem, timeout.Token);

        Assert.IsNotNull(token);
        // Boolean assertions avoid printing the assertion, private key or returned token on failure.
        Assert.IsFalse(string.IsNullOrWhiteSpace(token.access_token), "Missing access token.");
        Assert.IsNotNull(token.scope, "Missing granted scopes.");
        Assert.IsTrue(token.expires_in > 0, "Token lifetime must be positive.");
    }

    private static string RequireEnvironmentVariable(string name)
    {
        var value = GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            Assert.Inconclusive($"Set {name} to run this live OAuth test.");
        return value;
    }

    private static string GetEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) ??
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
}
