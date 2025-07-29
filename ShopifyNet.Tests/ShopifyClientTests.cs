﻿using System.Text.Json;
using GraphQLSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopifyNet.Types;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyClientTests
{
    private ShopifyClient _client;

    [TestInitialize]
    public void Initialize()
    {
        _client = new(GetClientOptions());
    }

    private static ShopifyClientOptions GetClientOptions()
    {
        string shopId = Environment.GetEnvironmentVariable("SHOPIFYNET_SHOP_ID", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("GRAPHQLSHARP_SHOP_ID");
        string token = Environment.GetEnvironmentVariable("SHOPIFNET_SHOP_TOKEN", EnvironmentVariableTarget.User) ?? Environment.GetEnvironmentVariable("GRAPHQLSHARP_SHOP_TOKEN");
        return new ShopifyClientOptions(shopId, token)
        {
            Interceptor = null
        };
    }

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

        //response is strongly typed
        var response = await _client.ExecuteQueryAsync(query);
        Assert.IsNotNull(response.data.products.nodes.FirstOrDefault()?.id);
    }

    [TestMethod]
    [ExpectedException(typeof(GraphQLErrorsException))]
    public async Task QuerySimpleWithError()
    {
        //size parameter is not valid for products query
        var query = """
            query {
                products(size: 10)
                {
                    nodes
                    {
                        id
                        title
                    }
                }
            }
            """;

        //response is strongly typed
        var response = await _client.ExecuteQueryAsync(query);
    }

    [TestMethod]
    public async Task MutationSimple()
    {
        var query = """
            mutation {
                    appSubscriptionTrialExtend(id: "gid://shopify/AppSubscription/123", days: 10) {
                        userErrors {
                        message
                        }
                    }
                }
            """;

        var response = await _client.ExecuteMutationAsync(query);
        Assert.IsTrue(response.data.appSubscriptionTrialExtend.userErrors.Any());
    }

    [TestMethod]
    [ExpectedException(typeof(GraphQLErrorsException))]
    public async Task MutationSimpleWithError()
    {
        //price is not a valid field for productCreate
        var query = """
            mutation {
                productCreate(input: { title: "New Product", price: 100 }) {
                    product {
                        id
                        title
                    }
                }
            }
            """;

        var response = await _client.ExecuteMutationAsync(query);
    }

    [TestMethod]
    public async Task QuerySimpleWithVariables()
    {
        var query = """
            query ($first: Int!){
                products(first: $first)
                {
                    nodes
                    {
                        id
                        title
                    }
                }
            }
            """;

        var request = new ShopifyGraphQLRequest
        {
            query = query,
            variables = new Dictionary<string, object>
            {
                { "first", 10 }
            }
        };

        //response is strongly typed
        var response = await _client.ExecuteQueryAsync(request);
        Assert.IsNotNull(response.data.products.nodes.FirstOrDefault()?.id);
    }

    [TestMethod]
    public async Task QueryWithMultipleOperations()
    {
        var query = """
            query myQuery($first: Int!) {
                products(first: $first)
                {
                    nodes
                    {
                        id
                        title
                    }
                }
            }
            query myQuery2 {
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

        var request = new ShopifyGraphQLRequest
        {
            query = query,
            operationName = "myQuery",
            variables = new Dictionary<string, object>
            {
                { "first", 10 }
            }
        };

        var response = await _client.ExecuteQueryAsync(request);
        Assert.IsNotNull(response.data.products.nodes.FirstOrDefault()?.id);
    }

    [TestMethod]
    public async Task QueryWithAliases()
    {
        var query = """
            query ($first: Int!) {
                myProducts: products(first: $first)
                {
                    nodes
                    {
                        id
                        title
                    }
                }
                myOrders: orders(first: $first)
                {
                    nodes
                    {
                        id
                        name
                    }
                }
            }
            """;

        var request = new ShopifyGraphQLRequest
        {
            query = query,
            variables = new Dictionary<string, object>
            {
                { "first", 10 }
            }
        };

        var response = await _client.ExecuteAsync(request);
        //response.data is JsonElement
        var myProducts = response.data.GetProperty("myProducts")
                                     .Deserialize<ProductConnection>(Serializer.Options);
        var myOrders = response.data.GetProperty("myOrders")
                                     .Deserialize<OrderConnection>(Serializer.Options);
        Assert.IsNotNull(myProducts.nodes.FirstOrDefault()?.title);
        Assert.IsNotNull(myOrders.nodes.FirstOrDefault()?.name);
    }

    [TestMethod]
    [ExpectedException(typeof(GraphQLErrorsException))]
    public async Task QueryWithSyntaxError()
    {
        var query = """
            query {
                products(first: 10)
                {
                    nodes
                    SYNTAX ERROR!!!
                        id
                        title
                    }
                }
            }
            """;

        var request = new ShopifyGraphQLRequest
        {
            query = query
        };

        var response = await _client.ExecuteQueryAsync(request);
    }

    [TestMethod]
    public async Task QueryWithSyntaxErrorNoThrow()
    {
        var query = """
            query {
                products(first: 10)
                {
                    nodes
                    SYNTAX ERROR!!!
                        id
                        title
                    }
                }
            }
            """;

        var request = new ShopifyGraphQLRequest
        {
            query = query
        };

        var options = GetClientOptions();
        options.ThrowOnGraphQLErrors = false;
        var response = await _client.ExecuteQueryAsync(request, options);
        Assert.IsNotNull(response.errors);
        Assert.IsTrue(response.errors.Count > 0);
    }
}