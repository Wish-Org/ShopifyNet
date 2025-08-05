[![NuGet](https://img.shields.io/nuget/v/ShopifyNet.svg)](https://www.nuget.org/packages/ShopifyNet)

ShopifyNet is a .NET library which enables you to interact with the Shopify GraphQL API with .NET.

## Installation

To install ShopifyNet, run the following command in your .NET project:

```bash
dotnet add package ShopifyNet
```

## Features

-   Strongly typed queries, mutations, input objects. All API types have been pregenerated and are available in the `ShopifyNet.Types` namespace.
-   Support for request interception. This enables centralized logic for logging, retries, rate limit policies, testing, error handling...
-   Built-in smart interceptor automatically handles retries and rate limits
-   The `ShopifyNet.ShopifyClient` is thread safe.
-   Ability to provide a custom `HttpClient` instance

### Example

```csharp
[TestMethod]
public async Task QuerySimple()
{
    var query = """
        query {
            products(first: 10)
            {
                nodes
                {
                    id
                    title
                }
            }
        }
        """;

    var response = await _client.QueryAsync(query);
    //response is strongly typed
    Assert.IsNotNull(response.data.products.nodes.FirstOrDefault()?.id);
    Assert.IsNull(response.errors);
}
```

See unit tests for more examples.
