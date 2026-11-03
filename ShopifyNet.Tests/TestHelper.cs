namespace ShopifyNet.Tests;

public class TestHelper
{
    public static string ShopId => Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_ID", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_ID");
    public static string ClientId => Environment.GetEnvironmentVariable("SHOPIFYNET_CLIENT_ID", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SHOPIFYNET_CLIENT_ID");
    public static string PrivateKey => Environment.GetEnvironmentVariable("SHOPIFYNET_PRIVATE_KEY", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SHOPIFYNET_PRIVATE_KEY");
    public static string KeyId => Environment.GetEnvironmentVariable("SHOPIFYNET_KEY_ID", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SHOPIFYNET_KEY_ID");
    public static async Task<string> GetAccessTokenAsync()
    {
        var oauthClient = new ShopifyOAuthClient();
        var token = await oauthClient.RequestClientAssertionAccessTokenAsync(ClientId, ShopId, KeyId, PrivateKey);
        return token.access_token;
    }
}
