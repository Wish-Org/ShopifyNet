namespace ShopifyNet;

public partial class GraphQLClient
{
    protected override void ValidateOptions(AdminClientOptions defaultOptions, AdminClientOptions options)
    {
        _ = defaultOptions?.MyShopifyDomain ?? options?.MyShopifyDomain ?? throw new ArgumentNullException(nameof(options.MyShopifyDomain));
        _ = defaultOptions?.AccessToken ?? options?.AccessToken ?? throw new ArgumentNullException(nameof(options.AccessToken));
    }
}