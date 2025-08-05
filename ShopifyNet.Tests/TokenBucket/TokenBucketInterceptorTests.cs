using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GraphQLSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopifyNet.Types;

namespace ShopifyNet.Tests;

[TestClass]
public class TokenBucketInterceptorTests
{
    private TokenBucketInterceptor _interceptor;

    [TestInitialize]
    public void Initialize()
    {
        _interceptor = new TokenBucketInterceptor();
    }

    private ShopifyClientOptions CreateClientOptions(HttpStatusCode status, object response, IInterceptor interceptor, bool throwOnGraphQLErrors = true)
    {
        return new ShopifyClientOptions(TestHelper.ShopId, TestHelper.Token)
        {
            Interceptor = interceptor,
            ThrowOnGraphQLErrors = throwOnGraphQLErrors,
            HttpClient = new TestHttpClient(msg => new HttpResponseMessage
            {
                StatusCode = status,
                Content = JsonContent.Create(response)
            })
        };
    }

    private ShopifyClientOptions CreateClientOptions(HttpClient httpClient, IInterceptor interceptor, bool throwOnGraphQLErrors = true)
    {
        return new ShopifyClientOptions(TestHelper.ShopId, TestHelper.Token)
        {
            Interceptor = interceptor,
            ThrowOnGraphQLErrors = throwOnGraphQLErrors,
            HttpClient = httpClient
        };
    }


    private async Task<GraphQLResponse<QueryRoot>> QueryProductsAsync(ShopifyClientOptions options = null)
    {
        var client = new ShopifyClient(options);
        var query = """
            query {
                products(first: 10) {
                    nodes {
                        id
                        title
                    }
                }
            }
            """;
        return await client.QueryAsync(query);
    }

    private Dictionary<string, JsonElement> CreateExtensionsWithCost(Cost cost)
    {
        return new Dictionary<string, JsonElement>
        {
            { "cost", JsonSerializer.SerializeToElement(cost, Serializer.Options) }
        };
    }


    private GraphQLResponse CreateGraphQLErrorResponse(Cost cost = null)
    {
        return CreateGraphQLResponse(cost, error: "Some error", throttled: false);
    }

    private GraphQLResponse CreateGraphQLThrottledResponse(Cost cost = null)
    {
        return CreateGraphQLResponse(cost, error: "Throttled, please wait", throttled: true);
    }

    private GraphQLResponse CreateGraphQLResponse(Cost cost = null, string error = null, bool throttled = false)
    {
        return new GraphQLResponse
        {
            errors = error == null ? null : new()
            {
                 new GraphQLError
                 {
                    message = error,
                    extensions = !throttled ? new () : new ()
                        {
                            { "code", JsonSerializer.SerializeToElement("THROTTLED", Serializer.Options) }
                        }
                 }
        },
            extensions = cost == null ? null : CreateExtensionsWithCost(cost)
        };
    }

    [TestMethod]
    public async Task ShouldNotRetryOnHttpError()
    {
        var testInterceptor = new TestInterceptor(_interceptor);
        var options = CreateClientOptions(HttpStatusCode.InternalServerError, "Internal Server Error", testInterceptor);

        bool caughtException = false;
        try
        {
            var response = await QueryProductsAsync(options);
        }
        catch (GraphQLHttpException)
        {
            caughtException = true;
        }
        Assert.IsTrue(caughtException);
        Assert.AreEqual(1, testInterceptor.CallCount);
    }

    [TestMethod]
    public async Task ShouldNotRetryOnGraphQLError()
    {
        var testInterceptor = new TestInterceptor(_interceptor);
        var options = CreateClientOptions(HttpStatusCode.OK, CreateGraphQLErrorResponse(), testInterceptor);

        bool caughtException = false;
        try
        {
            var response = await QueryProductsAsync(options);
        }
        catch (GraphQLErrorsException)
        {
            caughtException = true;
        }
        Assert.IsTrue(caughtException);
        Assert.AreEqual(1, testInterceptor.CallCount);
    }

    [TestMethod]
    public async Task ShouldNotRetryOnGraphQLErrorNoThrow()
    {
        var testInterceptor = new TestInterceptor(_interceptor);
        var cost = new Cost
        {
            requestedQueryCost = 100,
            actualQueryCost = 100,
            throttleStatus = new Cost.ThrottleStatus
            {
                maximumAvailable = 1000,
                currentlyAvailable = 900,
                restoreRate = 50
            }
        };
        var options = CreateClientOptions(HttpStatusCode.OK, CreateGraphQLErrorResponse(cost), testInterceptor, throwOnGraphQLErrors: false);

        bool caughtException = false;
        try
        {
            var response = await QueryProductsAsync(options);
        }
        catch (GraphQLErrorsException)
        {
            caughtException = true;
        }
        Assert.IsFalse(caughtException);
        Assert.AreEqual(1, testInterceptor.CallCount);
    }

    [TestMethod]
    public async Task ShouldRetryTwiceWhenThrottled()
    {
        var testInterceptor = new TestInterceptor(_interceptor);
        var cost = new Cost
        {
            requestedQueryCost = 100,
            actualQueryCost = 100,
            throttleStatus = new Cost.ThrottleStatus
            {
                maximumAvailable = 1000,
                currentlyAvailable = 50,
                restoreRate = 50
            }
        };
        var options = CreateClientOptions(HttpStatusCode.OK, CreateGraphQLThrottledResponse(cost), testInterceptor, throwOnGraphQLErrors: false);

        var response = await QueryProductsAsync(options);
        Assert.AreEqual(3, testInterceptor.CallCount);
        Assert.IsTrue(response.IsThrottled());
    }

    [TestMethod]
    public async Task ShouldRetryThrottledResponse()
    {
        var testInterceptor = new TestInterceptor(_interceptor);
        var cost = new Cost
        {
            requestedQueryCost = 500,
            actualQueryCost = 500,
            throttleStatus = new Cost.ThrottleStatus
            {
                maximumAvailable = 1000,
                currentlyAvailable = 100,
                restoreRate = 50
            }
        };
        var httpClient = new TestHttpClient(msg => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(testInterceptor.CallCount == 1 ? CreateGraphQLThrottledResponse(cost) : CreateGraphQLResponse(cost))
        });
        var options = CreateClientOptions(httpClient, testInterceptor, throwOnGraphQLErrors: false);

        var expectedWaitSeconds = (double)(((decimal)cost.actualQueryCost - cost.throttleStatus.currentlyAvailable) / cost.throttleStatus.restoreRate);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await QueryProductsAsync(options);
        sw.Stop();
        Assert.AreEqual(2, testInterceptor.CallCount);
        Assert.IsFalse(response.IsThrottled());
        Assert.IsTrue(sw.Elapsed.TotalSeconds > expectedWaitSeconds * 0.9);
        Assert.IsTrue(sw.Elapsed.TotalSeconds < expectedWaitSeconds * 1.1);
    }
}