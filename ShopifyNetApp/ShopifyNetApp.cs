// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using GraphQLSharp;

var options = new GraphQLTypeGeneratorOptions
{
    Namespace = "shopify",
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
var shopifyDoc = JsonDocument.Parse(File.OpenRead(@"./shopify.json"));
var code = generator.GenerateTypes(options, shopifyDoc);
File.WriteAllText("../../../../ShopifyNet/AdminTypes/Shopify.cs", code);
