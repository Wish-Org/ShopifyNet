using System.Net;
using System.Net.Http.Json;
using GraphQLSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class RequestAccessTokenInterceptorTests
{
    private const string ACCESS_TOKEN_HEADER = "X-Shopify-Access-Token";

    /// <summary>
    /// Replaces the X-Shopify-Access-Token header set by ShopifyClientOptions with ShopifyGraphQLRequest.AccessToken, when set.
    /// </summary>
    private class RequestAccessTokenInterceptor : IInterceptor<ShopifyGraphQLRequest, ShopifyClientOptions>
    {
        public Task<GraphQLResponse<TData>> InterceptRequestAsync<TData>(ShopifyGraphQLRequest request, ShopifyClientOptions options, CancellationToken cancellationToken, ExecuteRequestAsync<ShopifyGraphQLRequest, TData> executeAsync)
        {
            if (request.AccessToken == null)
                return executeAsync(request, null, cancellationToken);

            return executeAsync(request, headers =>
            {
                headers.Remove(ACCESS_TOKEN_HEADER);
                headers.Add(ACCESS_TOKEN_HEADER, request.AccessToken);
            }, cancellationToken);
        }
    }

    private static (ShopifyClient client, List<string[]> sentTokens) CreateClient()
    {
        var sentTokens = new List<string[]>();
        var httpClient = new TestHttpClient(msg =>
        {
            sentTokens.Add(msg.Headers.TryGetValues(ACCESS_TOKEN_HEADER, out var values) ? values.ToArray() : []);
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new GraphQLResponse())
            };
        });
        var options = new ShopifyClientOptions("test.myshopify.com", "options-token")
        {
            Interceptor = new RequestAccessTokenInterceptor(),
            HttpClient = httpClient,
        };
        return (new ShopifyClient(options), sentTokens);
    }

    [TestMethod]
    public async Task ShouldReplaceOptionsAccessTokenWithRequestAccessToken()
    {
        var (client, sentTokens) = CreateClient();

        await client.QueryAsync(new ShopifyGraphQLRequest
        {
            query = "query { shop { id } }",
            AccessToken = "request-token",
        });

        Assert.AreEqual(1, sentTokens.Count);
        CollectionAssert.AreEqual(new[] { "request-token" }, sentTokens[0]);
    }

    [TestMethod]
    public async Task ShouldKeepOptionsAccessTokenWhenRequestAccessTokenIsNull()
    {
        var (client, sentTokens) = CreateClient();

        await client.QueryAsync("query { shop { id } }");

        Assert.AreEqual(1, sentTokens.Count);
        CollectionAssert.AreEqual(new[] { "options-token" }, sentTokens[0]);
    }
}
