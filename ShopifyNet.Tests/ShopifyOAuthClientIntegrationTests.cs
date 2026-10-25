using System.Security.Permissions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

// These tests consume real credentials. Use a disposable development app/store.
[TestClass]
[Ignore("Local testing only: requires live Shopify credentials. Temporarily remove this attribute to run locally.")]
[TestCategory("Integration")]
[DoNotParallelize]
public class ShopifyOAuthClientIntegrationTests
{
    private ShopifyOAuthClient _client;
    private string _shop;

    [TestInitialize]
    public void Initialize()
    {
        if (GetEnvironmentVariable("SHOPIFYNET_RUN_OAUTH_INTEGRATION_TESTS") != "1")
            Assert.Inconclusive("Set SHOPIFYNET_RUN_OAUTH_INTEGRATION_TESTS=1 to enable live OAuth tests.");

        _shop = RequireEnvironmentVariable("SHOPIFYNET_SHOP_ID");
        Assert.IsTrue(Uri.CheckHostName(_shop) == UriHostNameType.Dns &&
                      _shop.EndsWith(".myshopify.com", StringComparison.OrdinalIgnoreCase),
                      "SHOPIFYNET_SHOP_ID must be a plain store.myshopify.com hostname.");
        _client = new ShopifyOAuthClient(
            RequireEnvironmentVariable("SHOPIFYNET_CLIENT_ID"),
            RequireEnvironmentVariable("SHOPIFYNET_CLIENT_SECRET"));
    }

    [TestMethod]
    public async Task MigrateToExpiringOfflineAccessTokenAsync_ReturnsExpiringToken()
    {
        var original = RequireEnvironmentVariable("SHOPIFYNET_NON_EXPIRING_ACCESS_TOKEN");
        var startedAt = DateTimeOffset.UtcNow;

        var token = await _client.MigrateToExpiringOfflineAccessTokenAsync(_shop, original);

        AssertOfflineToken(token, startedAt, DateTimeOffset.UtcNow);
        Assert.IsFalse(token.access_token == original, "Migration should issue a new access token.");
    }

    [TestMethod]
    public async Task RefreshOfflineAccessTokenAsync_ReturnsRotatedToken()
    {
        var original = RequireEnvironmentVariable("SHOPIFYNET_REFRESH_TOKEN");
        var startedAt = DateTimeOffset.UtcNow;

        var token = await _client.RefreshOfflineAccessTokenAsync(_shop, original);

        AssertOfflineToken(token, startedAt, DateTimeOffset.UtcNow);
        // Use a boolean assertion so a failure cannot print either secret.
        Assert.IsFalse(token.refresh_token == original, "Refresh should rotate the refresh token.");
    }

    [TestMethod]
    public async Task ExchangeIdTokenForOfflineAccessTokenAsync_ReturnsExpiringToken()
    {
        var idToken = RequireEnvironmentVariable("SHOPIFYNET_ID_TOKEN");
        var startedAt = DateTimeOffset.UtcNow;

        var token = await _client.ExchangeIdTokenForOfflineAccessTokenAsync(_shop, idToken);

        AssertOfflineToken(token, startedAt, DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public async Task ExchangeIdTokenForOnlineAccessTokenAsync_ReturnsUserToken()
    {
        var idToken = RequireEnvironmentVariable("SHOPIFYNET_ID_TOKEN");
        var startedAt = DateTimeOffset.UtcNow;

        var token = await _client.ExchangeIdTokenForOnlineAccessTokenAsync(_shop, idToken);

        AssertOnlineToken(token, startedAt, DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public async Task ExchangeCodeForOfflineAccessTokenAsync_ReturnsExpiringToken()
    {
        // Use a fresh code from a validated callback without grant_options[]=per-user.
        var code = RequireEnvironmentVariable("SHOPIFYNET_OFFLINE_AUTHORIZATION_CODE");
        var startedAt = DateTimeOffset.UtcNow;

        var token = await _client.ExchangeCodeForOfflineAccessTokenAsync(_shop, code, true);

        AssertOfflineToken(token, startedAt, DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public async Task ExchangeCodeForOfflineAccessTokenAsync_ReturnsNonExpiringToken()
    {
        var code = RequireEnvironmentVariable("SHOPIFYNET_NON_EXPIRING_OFFLINE_AUTHORIZATION_CODE");

        var token = await _client.ExchangeCodeForOfflineAccessTokenAsync(_shop, code, expiring: false);

        Assert.IsNotNull(token);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token.access_token), "Missing access token.");
        Assert.IsNotNull(token.scope);
        Assert.IsNull(token.expires_in);
        Assert.IsNull(token.ExpiresAt);
        Assert.IsNull(token.refresh_token);
        Assert.IsNull(token.refresh_token_expires_in);
        Assert.IsNull(token.RefreshTokenExpiresAt);
    }

    [TestMethod]
    public async Task ExchangeCodeForOnlineAccessTokenAsync_ReturnsUserToken()
    {
        // Use a separate fresh code from a validated callback with grant_options[]=per-user.
        var code = RequireEnvironmentVariable("SHOPIFYNET_ONLINE_AUTHORIZATION_CODE");
        var startedAt = DateTimeOffset.UtcNow;

        var token = await _client.ExchangeCodeForOnlineAccessTokenAsync(_shop, code);

        AssertOnlineToken(token, startedAt, DateTimeOffset.UtcNow);
    }

    private static void AssertOnlineToken(ShopifyOnlineAccessToken token, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        Assert.IsNotNull(token);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token.access_token), "Missing access token.");
        Assert.IsNotNull(token.scope);
        if (token.expires_in.HasValue)
            AssertExpiry(token.expires_in, token.ExpiresAt, startedAt, completedAt);
        else
            Assert.IsNull(token.ExpiresAt, "An expiry date must not be inferred when expires_in is absent.");
        Assert.IsNotNull(token.associated_user);
        Assert.IsTrue(token.associated_user.id > 0, "Missing associated user ID.");
        Assert.IsNotNull(token.associated_user_scope);
    }

    private static void AssertOfflineToken(ShopifyOfflineAccessToken token, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        Assert.IsNotNull(token);
        Assert.IsFalse(string.IsNullOrWhiteSpace(token.access_token), "Missing access token.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(token.refresh_token), "Missing refresh token.");
        Assert.IsNotNull(token.scope);
        AssertExpiry(token.expires_in, token.ExpiresAt, startedAt, completedAt);
        AssertExpiry(token.refresh_token_expires_in, token.RefreshTokenExpiresAt, startedAt, completedAt);
    }

    private static void AssertExpiry(int? lifetimeSeconds, DateTimeOffset? expiresAt, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        Assert.IsTrue(lifetimeSeconds.HasValue, "Missing token lifetime.");
        Assert.IsTrue(expiresAt.HasValue, "Missing token expiry.");
        Assert.IsTrue(lifetimeSeconds > 0, "Token lifetime must be positive.");
        Assert.IsTrue(expiresAt >= startedAt.AddSeconds(lifetimeSeconds.Value) &&
                      expiresAt <= completedAt.AddSeconds(lifetimeSeconds.Value),
                      "Expiry must be computed from the request time and returned lifetime.");
        Assert.IsTrue(expiresAt > completedAt, "Returned token has already expired.");
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
