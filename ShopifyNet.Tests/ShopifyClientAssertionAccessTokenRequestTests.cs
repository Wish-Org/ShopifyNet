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
    [DataTestMethod]
    [DataRow("ES256", false)]
    [DataRow("RS256", false)]
    [DataRow("ES256", true)]
    [DataRow("RS256", true)]
    public async Task CreatesFreshSignedAssertionForEveryTokenRequest(string algorithm, bool useTraditionalPem)
    {
        var keys = ShopifyClientAssertionKeys.GenerateKeyPair("test-key", algorithm);
        using AsymmetricAlgorithm signingKey = algorithm == "ES256" ? ECDsa.Create() : RSA.Create();
        signingKey.ImportFromPem(keys.PrivateKeyPem);
        using AsymmetricAlgorithm verificationKey = signingKey is ECDsa ec
            ? ECDsa.Create(ec.ExportParameters(false)) : RSA.Create(((RSA)signingKey).ExportParameters(false));
        var pem = useTraditionalPem
            ? signingKey is ECDsa ecKey ? ecKey.ExportECPrivateKeyPem() : ((RSA)signingKey).ExportRSAPrivateKeyPem()
            : keys.PrivateKeyPem;
        var assertionIds = new HashSet<string>();
        long issuedAt = 0;
        var clientId = "client-id";
        using var http = new TestHttpClient(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://test.myshopify.com/admin/oauth/access_token", request.RequestUri.AbsoluteUri);
            var form = HttpUtility.ParseQueryString(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.AreEqual(4, form.Count);
            Assert.AreEqual("client_credentials", form["grant_type"]);
            Assert.AreEqual(clientId, form["client_id"]);
            Assert.AreEqual("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", form["client_assertion_type"]);
            Assert.IsNull(form["client_secret"]);
            var parts = form["client_assertion"].Split('.');
            Assert.AreEqual(3, parts.Length);
            using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
            Assert.AreEqual(algorithm, header.RootElement.GetProperty("alg").GetString());
            Assert.AreEqual("test-key", header.RootElement.GetProperty("kid").GetString());
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            Assert.AreEqual(clientId, payload.RootElement.GetProperty("iss").GetString());
            Assert.AreEqual(clientId, payload.RootElement.GetProperty("sub").GetString());
            Assert.AreEqual(request.RequestUri.AbsoluteUri, payload.RootElement.GetProperty("aud").GetString());
            issuedAt = payload.RootElement.GetProperty("iat").GetInt64();
            Assert.AreEqual(60L, payload.RootElement.GetProperty("exp").GetInt64() - issuedAt);
            Assert.IsTrue(assertionIds.Add(payload.RootElement.GetProperty("jti").GetString()), "Each call must use a new assertion ID.");
            var data = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
            var signature = Base64Url.DecodeFromChars(parts[2]);
            Assert.IsTrue(verificationKey is ECDsa ecVerifier
                ? ecVerifier.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                : ((RSA)verificationKey).VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"test-token","scope":"read_products","expires_in":3600}""")
            };
        });
        var client = new ShopifyOAuthClient(http);
        for (var i = 0; i < 2; i++)
        {
            clientId = $"client-{i}";
            var beforeRequest = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var token = await client.RequestClientAssertionAccessTokenAsync(clientId, "test.myshopify.com", "test-key", pem);
            var afterRequest = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.IsTrue(issuedAt >= beforeRequest && issuedAt <= afterRequest,
                "Assertion iat must be the current UTC time in Unix seconds.");
            Assert.AreEqual("test-token", token.access_token);
            Assert.AreEqual(3600, token.expires_in);
        }
        Assert.AreEqual(2, assertionIds.Count);
    }

    [TestMethod]
    public async Task RejectsUnsupportedKeysBeforeSending()
    {
        using var http = new TestHttpClient(_ => throw new AssertFailedException("Invalid key must not be sent."));
        var client = new ShopifyOAuthClient(http);
        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using var smallRsa = RSA.Create(1024);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RequestClientAssertionAccessTokenAsync(
            "client-id", "test.myshopify.com", "key", wrongCurve.ExportPkcs8PrivateKeyPem()));
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => client.RequestClientAssertionAccessTokenAsync(
            "client-id", "test.myshopify.com", "key", smallRsa.ExportPkcs8PrivateKeyPem()));
    }

    [DataTestMethod]
    [DataRow("ES256")]
    [DataRow("RS256")]
    public async Task RejectsPublicOnlyKeysBeforeSending(string algorithm)
    {
        using AsymmetricAlgorithm key = algorithm == "ES256" ? ECDsa.Create(ECCurve.NamedCurves.nistP256) : RSA.Create(2048);
        using var http = new TestHttpClient(_ => throw new AssertFailedException("Public-only key must not be sent."));
        var client = new ShopifyOAuthClient(http);
        try
        {
            await client.RequestClientAssertionAccessTokenAsync(
                "client-id", "test.myshopify.com", "key", key.ExportSubjectPublicKeyInfoPem());
            Assert.Fail("A public-only key must fail signing.");
        }
        catch (CryptographicException)
        {
            // Accept platform-specific subclasses, including OpenSSL exceptions on Linux.
        }
    }
}
