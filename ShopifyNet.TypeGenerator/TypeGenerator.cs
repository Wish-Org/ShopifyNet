using System.Text.Json;
using GraphQLSharp;
using ShopifySharp;

var options = new GraphQLTypeGeneratorOptions
{
    Namespace = "ShopifyNet.AdminTypes",
    ScalarNameTypeToTypeName = new Dictionary<string, string>
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
    EnumMembersAsString = true
};

var generator = new GraphQLTypeGenerator();
string csharpCode = await generator.GenerateTypesAsync(options, async query =>
{
    string shopId = Environment.GetEnvironmentVariable("SHOPIFYNET_SHOPID", EnvironmentVariableTarget.User)!;
    string token = Environment.GetEnvironmentVariable("SHOPIFYNET_TOKEN", EnvironmentVariableTarget.User)!;
    var res = await new GraphService(shopId, token, "2024-10").PostAsync(query);
    var doc = JsonDocument.Parse(res.ToString());
    return doc;
});

File.WriteAllText(@"../../../../ShopifyNet/AdminTypes.cs", csharpCode);
