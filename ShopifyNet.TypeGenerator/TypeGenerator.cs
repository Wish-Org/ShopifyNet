using System.Net.Http.Headers;
using System.Text.Json;
using GraphQLSharp;
using ShopifyNet;

var options = new GraphQLTypeGeneratorOptions
{
    NamespaceClient = "ShopifyNet",
    NamespaceTypes = "ShopifyNet.Types",
    ClientClassName = "ShopifyClient",
    ScalarTypeNameToDotNetTypeName = new Dictionary<string, string>
                {
                    { "UnsignedInt64", "ulong" },
                    { "Money", "decimal" },
                    { "Float", "decimal" },
                    { "Decimal", "decimal" },
                    { "DateTime", "DateTime" },
                    { "Date", "DateOnly" },
                    { "UtcOffset", "TimeSpan" },
                    { "URL", "string" },
                    { "HTML", "string" },
                    { "JSON", "string" },
                    { "FormattedString", "string" },
                    { "ARN", "string" },
                    { "StorefrontID", "string" },
                    { "Color", "string" },
                    { "BigInt", "long" },
                },
    GraphQLTypeToTypeNameOverride = new Dictionary<(string, string), string>
                {
                    { ("ShopifyPaymentsDispute", "evidenceDueBy"), "DateTime" },
                    { ("ShopifyPaymentsDispute", "evidenceSentOn"), "DateTime" },
                    { ("ShopifyPaymentsDispute", "finalizedOn"), "DateTime" },
                },
    EnumMembersAsString = true,
    GenerateMemberNames = true,
    ClientOptionsType = typeof(ShopifyClientOptions),
    GraphQLRequestType = typeof(ShopifyGraphQLRequest),
};

var generator = new GraphQLTypeGenerator();
string csharpCode = await generator.GenerateTypesAsync(options, async query =>
{
    string shopId = Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_ID", EnvironmentVariableTarget.User)!;
    string token = Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_TOKEN", EnvironmentVariableTarget.User)!;

    var shopifyOptions = new ShopifyClientOptions(shopId, token);

    static Uri GetUri<TOptions>(TOptions options, TOptions defaultOptions) where TOptions : GraphQLClientOptionsBase, IGraphQLClientOptions<TOptions>
                        => TOptions.GetUri(defaultOptions, options);
    static Action<HttpRequestHeaders> GetConfigureHttpRequestHeaders<TOptions>(TOptions options, TOptions defaultOptions) where TOptions : GraphQLClientOptionsBase, IGraphQLClientOptions<TOptions>
                        => TOptions.GetConfigureHttpRequestHeaders(defaultOptions, options);

    var uri = GetUri(shopifyOptions, null!);
    var options = new GraphQLClientOptions(uri)
    {
        ConfigureHttpRequestHeaders = GetConfigureHttpRequestHeaders(shopifyOptions, null!),
    };
    var res = await new GraphQLCLient(options).ExecuteAsync(query);
    var doc = JsonDocument.Parse(res.data.GetRawText());
    return doc;
});

File.WriteAllText(@"../ShopifyNet/Generated.cs", csharpCode);
