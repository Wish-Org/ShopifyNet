using System.Diagnostics;
using System.Text.Json;
using GraphQLSharp;
using ShopifyNet;

var options = new GraphQLTypeGeneratorOptions
{
    Namespace = "ShopifyNet.AdminTypes",
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
    ClientOptionsType = typeof(AdminClientOptions),
};

var generator = new GraphQLTypeGenerator();
string csharpCode = await generator.GenerateTypesAsync(options, async query =>
{
    string shopId = Environment.GetEnvironmentVariable("SHOPIFYNET_SHOPID", EnvironmentVariableTarget.User)!;
    string token = Environment.GetEnvironmentVariable("SHOPIFYNET_TOKEN", EnvironmentVariableTarget.User)!;

    var options = new AdminClientOptions
    {
        MyShopifyDomain = shopId,
        AccessToken = token,
    };

    var res = await new GraphQLCLient(options).ExecuteAsync(query);
    var doc = JsonDocument.Parse(res.data.GetRawText());
    return doc;
});

File.WriteAllText(@"../ShopifyNet/AdminTypes.cs", csharpCode);
