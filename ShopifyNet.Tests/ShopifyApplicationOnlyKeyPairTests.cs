using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyApplicationOnlyKeyPairTests
{
    [TestMethod]
    public void GeneratedPublicJwkVerifiesPrivateKeySignature()
    {
        var (privateKeyPem, publicKeyJwk) = ShopifyApplicationOnlyKeys.GenerateKeyPair("betterreports");
        using var privateKey = ECDsa.Create();
        privateKey.ImportFromPem(privateKeyPem);
        Assert.AreEqual(256, privateKey.KeySize);
        StringAssert.StartsWith(privateKeyPem, "-----BEGIN PRIVATE KEY-----");

        using var document = JsonDocument.Parse(publicKeyJwk);
        var jwk = document.RootElement;
        Assert.AreEqual("EC", jwk.GetProperty("kty").GetString());
        Assert.AreEqual("P-256", jwk.GetProperty("crv").GetString());
        Assert.AreEqual("ES256", jwk.GetProperty("alg").GetString());
        Assert.AreEqual("sig", jwk.GetProperty("use").GetString());
        Assert.AreEqual("betterreports", jwk.GetProperty("kid").GetString());
        CollectionAssert.AreEquivalent(new[] { "kty", "crv", "x", "y", "kid", "alg", "use" },
            jwk.EnumerateObject().Select(p => p.Name).ToArray());

        var x = DecodeBase64Url(jwk.GetProperty("x").GetString());
        var y = DecodeBase64Url(jwk.GetProperty("y").GetString());
        Assert.AreEqual(32, x.Length);
        Assert.AreEqual(32, y.Length);
        using var publicKey = ECDsa.Create();
        publicKey.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y }
        });
        var data = Encoding.UTF8.GetBytes("test client assertion");
        var signature = privateKey.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Assert.AreEqual(64, signature.Length);
        Assert.IsTrue(publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(" key")]
    [DataRow("key\n")]
    public void RejectsInvalidKeyIds(string keyId)
    {
        Assert.ThrowsException<ArgumentException>(() => ShopifyApplicationOnlyKeys.GenerateKeyPair(keyId));
    }

    [TestMethod]
    public void RejectsNullAndOverlongKeyIds()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyApplicationOnlyKeys.GenerateKeyPair(null));
        Assert.ThrowsException<ArgumentException>(() => ShopifyApplicationOnlyKeys.GenerateKeyPair(new string('a', 256)));
    }

    private static byte[] DecodeBase64Url(string value)
    {
        Assert.IsFalse(value.Contains('='), "JWK integers must not have base64 padding.");
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }
}
