using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShopifyNet.Tests;

[TestClass]
public class ShopifyWebhookHmacTests
{
    private const string Secret = "client-secret";
    private const string Body = "{\"id\":820982911946154508,\"email\":\"jon@example.com\"}";
    // Computed independently: printf '%s' "$Body" | openssl dgst -sha256 -hmac 'client-secret' -binary | base64
    private const string BodyHmac = "Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/s=";
    // Computed independently: printf '' | openssl dgst -sha256 -hmac 'client-secret' -binary | base64
    private const string EmptyBodyHmac = "A8Bv4pNVz2gNhZFyYII6Wg0LPn0GT36FVD7hRp+Em20=";

    private static string Sign(byte[] body, string secret = Secret) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    [TestMethod]
    public void AcceptsValidHmacForStringBody()
    {
        Assert.IsTrue(ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, Body, Secret));
    }

    [TestMethod]
    public void AcceptsValidHmacForByteBody()
    {
        Assert.IsTrue(ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, Encoding.UTF8.GetBytes(Body), Secret));
    }

    [TestMethod]
    public void AcceptsValidHmacForEmptyBody()
    {
        Assert.IsTrue(ShopifyOAuthClient.IsValidWebhookHmac(EmptyBodyHmac, "", Secret));
        Assert.IsTrue(ShopifyOAuthClient.IsValidWebhookHmac(EmptyBodyHmac, ReadOnlySpan<byte>.Empty, Secret));
    }

    [TestMethod]
    public void EncodesStringBodyAndSecretAsUtf8()
    {
        const string body = "{\"title\":\"Café ☕ 日本\"}";
        const string secret = "sécret-🔑";
        var hmac = Sign(Encoding.UTF8.GetBytes(body), secret);
        Assert.IsTrue(ShopifyOAuthClient.IsValidWebhookHmac(hmac, body, secret));
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(hmac, Encoding.Latin1.GetBytes(body), secret));
    }

    [TestMethod]
    public void ValidatesRawBytesThatAreNotValidUtf8()
    {
        var body = new byte[] { 0x7B, 0xFF, 0xFE, 0x00, 0x7D };
        Assert.IsTrue(ShopifyOAuthClient.IsValidWebhookHmac(Sign(body), body, Secret));
    }

    [DataTestMethod]
    [DataRow("{\"id\":820982911946154508,\"email\":\"jon@example.org\"}")]
    [DataRow("{\"id\":820982911946154508,\"email\":\"jon@example.com\"}\n")]
    [DataRow(" {\"id\":820982911946154508,\"email\":\"jon@example.com\"}")]
    [DataRow("{\"email\":\"jon@example.com\",\"id\":820982911946154508}")]
    [DataRow("{ \"id\": 820982911946154508, \"email\": \"jon@example.com\" }")]
    [DataRow("")]
    public void RejectsTamperedOrReserializedBody(string body)
    {
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, body, Secret));
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, Encoding.UTF8.GetBytes(body), Secret));
    }

    [DataTestMethod]
    [DataRow("wrong-secret")]
    [DataRow("")]
    [DataRow("Client-Secret")]
    public void RejectsWrongSecret(string secret)
    {
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, Body, secret));
    }

    [TestMethod]
    public void RejectsHmacWithSingleFlippedBit()
    {
        var hash = Convert.FromBase64String(BodyHmac);
        for (var i = 0; i < hash.Length; i++)
        {
            var tampered = (byte[])hash.Clone();
            tampered[i] ^= 0x01;
            Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(Convert.ToBase64String(tampered), Body, Secret), $"byte {i}");
        }
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not base64!")]
    [DataRow("Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/s")] // missing padding
    [DataRow("Bl2MneNADkze0Byhd9z-bUTwNwuqta2avGGaZwoNh_s=")] // base64url alphabet
    [DataRow("065d8c9de3400e4cded01ca177dcfe6d44f0370baab5ad9abc619a670a0d87fb")] // hex encoding of the right hash
    [DataRow("Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoN")] // truncated to 30 bytes
    [DataRow("Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/sA")] // 33 bytes
    [DataRow("Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/s=,Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/s=")] // multiple header values
    [DataRow("Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/s=Bl2MneNADkze0Byhd9z+bUTwNwuqta2avGGaZwoNh/s=")] // concatenated
    public void RejectsMalformedHmacHeader(string hmacHeader)
    {
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(hmacHeader, Body, Secret));
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(hmacHeader, Encoding.UTF8.GetBytes(Body), Secret));
    }

    [TestMethod]
    public void RejectsHmacOfDifferentLengthEvenWhenPrefixMatches()
    {
        var hash = Convert.FromBase64String(BodyHmac);
        var longer = hash.Concat(new byte[] { 0 }).ToArray();
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(Convert.ToBase64String(longer), Body, Secret));
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(Convert.ToBase64String(hash[..^1]), Body, Secret));
    }

    [TestMethod]
    public void RejectsNullStringBody()
    {
        Assert.IsFalse(ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, (string)null, Secret));
    }

    [TestMethod]
    public void ThrowsOnNullSecret()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, Body, null));
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.IsValidWebhookHmac(BodyHmac, Encoding.UTF8.GetBytes(Body), null));
        // The secret is checked before the header and body, so misconfiguration is never masked as an invalid request.
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.IsValidWebhookHmac(null, (string)null, null));
        Assert.ThrowsException<ArgumentNullException>(() => ShopifyOAuthClient.IsValidWebhookHmac(null, ReadOnlySpan<byte>.Empty, null));
    }

    [TestMethod]
    public void ExposesShopifyHmacHeaderName()
    {
        Assert.AreEqual("X-Shopify-Hmac-SHA256", ShopifyOAuthClient.WEBHOOK_HMAC_HEADER);
    }
}
