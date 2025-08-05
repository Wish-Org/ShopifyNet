namespace ShopifyNet.Tests;

public class TestHelper
{
    public static string ShopId => Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_ID", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_ID");
    public static string Token => Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_TOKEN", EnvironmentVariableTarget.User) ??
                        Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_TOKEN");
}