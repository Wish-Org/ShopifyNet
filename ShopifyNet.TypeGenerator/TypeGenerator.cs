// See https://aka.ms/new-console-template for more information
using System.Text;
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
    var res = await new GraphService(Environment.GetEnvironmentVariable("SHOPIFYNET_SHOPID"),
                                    Environment.GetEnvironmentVariable("SHOPIFYNET_TOKEN"),
                                    Environment.GetEnvironmentVariable("SHOPIFYNET_API_VERSION"))
                                    .PostAsync(query);
    var doc = JsonDocument.Parse(res.ToString());
    return doc;
});

var strCode = new StringBuilder().AppendLine(csharpCode);

File.WriteAllText(@"../../../../ShopifyNet/AdminTypes.cs", strCode.ToString());
