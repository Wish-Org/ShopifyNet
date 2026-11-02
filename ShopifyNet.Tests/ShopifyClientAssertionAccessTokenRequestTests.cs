using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyClientAssertionAccessTokenRequestTests
{
    [TestMethod]
    public async Task CreatesFreshSignedAssertionForEveryTokenRequest()
    {
        var keys = ShopifyApplicationOnlyKeys.GenerateKeyPair("test-key");
        using var signingKey = ECDsa.Create();
        signingKey.ImportFromPem(keys.PrivateKeyPem);
        using var verificationKey = ECDsa.Create(signingKey.ExportParameters(false));
        var assertionIds = new HashSet<string>();
        long issuedAt = 0;
        using var http = new TestHttpClient(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://test.myshopify.com/admin/oauth/access_token", request.RequestUri.AbsoluteUri);
            var form = HttpUtility.ParseQueryString(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.AreEqual(4, form.Count);
            Assert.AreEqual("client_credentials", form["grant_type"]);
            Assert.AreEqual("client-id", form["client_id"]);
            Assert.AreEqual("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", form["client_assertion_type"]);
            Assert.IsNull(form["client_secret"]);
            var parts = form["client_assertion"].Split('.');
            Assert.AreEqual(3, parts.Length);
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
            Assert.AreEqual("ES256", header.RootElement.GetProperty("alg").GetString());
            Assert.AreEqual("test-key", header.RootElement.GetProperty("kid").GetString());
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            Assert.AreEqual("client-id", payload.RootElement.GetProperty("iss").GetString());
            Assert.AreEqual("client-id", payload.RootElement.GetProperty("sub").GetString());
            Assert.AreEqual(request.RequestUri.AbsoluteUri, payload.RootElement.GetProperty("aud").GetString());
            issuedAt = payload.RootElement.GetProperty("iat").GetInt64();
            Assert.AreEqual(60L, payload.RootElement.GetProperty("exp").GetInt64() - issuedAt);
            Assert.IsTrue(assertionIds.Add(payload.RootElement.GetProperty("jti").GetString()), "Each call must use a new assertion ID.");
            Assert.IsTrue(verificationKey.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),
                Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"test-token","scope":"read_products","expires_in":3600}""")
            };
        });
        var client = new ShopifyOAuthClient("client-id", "unused-secret", http);
        for (var i = 0; i < 2; i++)
        {
            var beforeRequest = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var token = await client.RequestClientAssertionAccessTokenAsync("test.myshopify.com", "test-key", keys.PrivateKeyPem);
            var afterRequest = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.IsTrue(issuedAt >= beforeRequest && issuedAt <= afterRequest,
                "Assertion iat must be the current UTC time in Unix seconds.");
            Assert.AreEqual("test-token", token.access_token);
            Assert.AreEqual(3600, token.expires_in);
        }
        Assert.AreEqual(2, assertionIds.Count);
    }
}
