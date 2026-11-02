using System.Buffers.Text;
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
        var (privateKeyPem, publicKeyJwk) = ShopifyClientAssertionKeys.GenerateKeyPair("betterreports", "ES256");
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

        var encodedX = jwk.GetProperty("x").GetString();
        var encodedY = jwk.GetProperty("y").GetString();
        Assert.IsFalse(encodedX.Contains('='), "JWK coordinates must not have base64 padding.");
        Assert.IsFalse(encodedY.Contains('='), "JWK coordinates must not have base64 padding.");
        var x = Base64Url.DecodeFromChars(encodedX);
        var y = Base64Url.DecodeFromChars(encodedY);
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
        Assert.ThrowsException<ArgumentException>(() => ShopifyClientAssertionKeys.GenerateKeyPair(keyId, "ES256"));
    }

    [TestMethod]
    public void RejectsNullAndOverlongKeyIds()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyClientAssertionKeys.GenerateKeyPair(null, "ES256"));
        Assert.ThrowsException<ArgumentException>(() => ShopifyClientAssertionKeys.GenerateKeyPair(new string('a', 256), "ES256"));
    }

    [TestMethod]
    public void GeneratesRsa3072PublicJwkMatchingPrivateKey()
    {
        var keys = ShopifyClientAssertionKeys.GenerateKeyPair("rsa-key", "RS256");
        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(keys.PrivateKeyPem);
        Assert.AreEqual(3072, privateKey.KeySize);
        StringAssert.StartsWith(keys.PrivateKeyPem, "-----BEGIN PRIVATE KEY-----");
        using var json = JsonDocument.Parse(keys.PublicKeyJwk);
        var jwk = json.RootElement;
        CollectionAssert.AreEquivalent(new[] { "kty", "n", "e", "kid", "alg", "use" },
            jwk.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual("RSA", jwk.GetProperty("kty").GetString());
        Assert.AreEqual("RS256", jwk.GetProperty("alg").GetString());
        Assert.AreEqual("rsa-key", jwk.GetProperty("kid").GetString());
        Assert.AreEqual("sig", jwk.GetProperty("use").GetString());
        using var publicKey = RSA.Create(new RSAParameters
        {
            Modulus = Base64Url.DecodeFromChars(jwk.GetProperty("n").GetString()),
            Exponent = Base64Url.DecodeFromChars(jwk.GetProperty("e").GetString())
        });
        var data = Encoding.UTF8.GetBytes("test assertion");
        var signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.IsTrue(publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("HS256")]
    [DataRow("ES384")]
    [DataRow("rs256")]
    [DataRow(" RS256 ")]
    public void RejectsUnsupportedAlgorithm(string algorithm)
    {
        Assert.ThrowsException<ArgumentException>(() => ShopifyClientAssertionKeys.GenerateKeyPair("key", algorithm));
    }
}
