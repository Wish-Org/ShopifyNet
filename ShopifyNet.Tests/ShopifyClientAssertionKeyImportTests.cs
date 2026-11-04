using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyClientAssertionKeyImportTests
{
    [DataTestMethod]
    [DataRow(2048)]
    [DataRow(8192)]
    public async Task AcceptsRsaSizeBoundaries(int keySize)
    {
        using var key = RSA.Create(keySize);
        using var http = new TestHttpClient(request =>
        {
            var form = HttpUtility.ParseQueryString(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var jwt = form["client_assertion"].Split('.');
            Assert.IsTrue(key.VerifyData(Encoding.ASCII.GetBytes(jwt[0] + "." + jwt[1]),
                Base64Url.DecodeFromChars(jwt[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"token","scope":"read_products","expires_in":3600}""")
            };
        });
        var client = new ShopifyOAuthClient(http);
        var token = await client.RequestClientAssertionAccessTokenAsync("client", "test.myshopify.com", "key", key.ExportPkcs8PrivateKeyPem());
        Assert.AreEqual("token", token.access_token);
    }

    [TestMethod]
    public async Task RejectsRsaAboveMaximumBeforeSigning()
    {
        // Only the modulus size matters for this guard. Avoid generating an oversized private key.
        var modulus = new byte[1025]; // 8200 bits: the next byte-aligned size above 8192.
        RandomNumberGenerator.Fill(modulus);
        modulus[0] |= 0x80;
        modulus[^1] |= 1;
        using var key = RSA.Create(new RSAParameters { Modulus = modulus, Exponent = new byte[] { 1, 0, 1 } });
        Assert.AreEqual(8200, key.KeySize);
        var error = await RejectPem(key.ExportSubjectPublicKeyInfoPem());
        StringAssert.Contains(error.Message, "2048 and 8192");
    }

    [TestMethod]
    public async Task PreservesBothImportErrorsForMalformedRsa()
    {
        var pem = PemEncoding.WriteString("RSA PRIVATE KEY", new byte[] { 1, 2, 3 });
        var error = await RejectPem(pem);
        StringAssert.Contains(error.Message, "unencrypted");
        Assert.AreEqual("privateKeyPem", error.ParamName);
        Assert.IsInstanceOfType(error.InnerException, typeof(AggregateException));
        Assert.AreEqual(2, ((AggregateException)error.InnerException).InnerExceptions.Count);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RejectsMixedAlgorithmPemBundles(bool usePkcs8)
    {
        using var rsa = RSA.Create(2048);
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rsaPem = usePkcs8 ? rsa.ExportPkcs8PrivateKeyPem() : rsa.ExportRSAPrivateKeyPem();
        var ecPem = usePkcs8 ? ec.ExportPkcs8PrivateKeyPem() : ec.ExportECPrivateKeyPem();
        foreach (var pem in new[] { rsaPem + "\n" + ecPem, ecPem + "\n" + rsaPem })
        {
            var error = await RejectPem(pem);
            StringAssert.Contains(error.Message, "exactly one");
        }
    }

    [TestMethod]
    public async Task RejectsEncryptedPemWithActionableError()
    {
        using var key = RSA.Create(2048);
        var pem = key.ExportEncryptedPkcs8PrivateKeyPem("test-password",
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000));
        var error = await RejectPem(pem);
        StringAssert.Contains(error.Message, "unencrypted");
    }

    [DataTestMethod]
    [DataRow("not a PEM")]
    [DataRow("-----BEGIN PRIVATE KEY-----\ninvalid!\n-----END PRIVATE KEY-----")]
    public async Task RejectsMalformedPem(string pem)
    {
        var error = await RejectPem(pem);
        StringAssert.Contains(error.Message, "exactly one");
    }

    private static async Task<ArgumentException> RejectPem(string pem)
    {
        using var http = new TestHttpClient(_ => throw new AssertFailedException("Invalid PEM must not trigger a request."));
        var client = new ShopifyOAuthClient(http);
        return await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            client.RequestClientAssertionAccessTokenAsync("client", "test.myshopify.com", "key", pem));
    }
}
