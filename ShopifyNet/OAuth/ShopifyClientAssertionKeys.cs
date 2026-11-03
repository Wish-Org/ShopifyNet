using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShopifyNet;

/// <summary>
/// Generates signing keys for Shopify client assertion authentication.
/// </summary>
public static class ShopifyClientAssertionKeys
{
    /// <summary>
    /// Generates a P-256 key pair for ES256 or an RSA-3072 key pair for RS256.
    /// Returns an unencrypted PKCS#8 PEM private key and an indented public JWK for Shopify's Dev Dashboard.
    /// Keep the private key secure; upload only the public JWK. Each call creates a new key pair.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/application-only-access-tokens
    /// </summary>
    public static (string PrivateKeyPem, string PublicKeyJwk) GenerateKeyPair(string keyId, string signingAlgorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (keyId.Length > 255 || keyId != keyId.Trim())
            throw new ArgumentException("Key ID must be at most 255 characters and have no leading or trailing whitespace.", nameof(keyId));

        if (signingAlgorithm is not ("ES256" or "RS256"))
            throw new ArgumentException("Signing algorithm must be ES256 or RS256.", nameof(signingAlgorithm));

        if (signingAlgorithm == "RS256")
        {
            using var rsa = RSA.Create(3072);
            var rsaPublicKey = rsa.ExportParameters(includePrivateParameters: false);
            var rsaJwk = JsonSerializer.Serialize(new
            {
                kty = "RSA",
                n = Base64Url.EncodeToString(rsaPublicKey.Modulus),
                e = Base64Url.EncodeToString(rsaPublicKey.Exponent),
                kid = keyId,
                alg = "RS256",
                use = "sig"
            }, new JsonSerializerOptions { WriteIndented = true });
            return (rsa.ExportPkcs8PrivateKeyPem(), rsaJwk);
        }

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportParameters(includePrivateParameters: false);
        var jwk = JsonSerializer.Serialize(new
        {
            kty = "EC",
            crv = "P-256",
            x = Base64Url.EncodeToString(publicKey.Q.X),
            y = Base64Url.EncodeToString(publicKey.Q.Y),
            kid = keyId,
            alg = "ES256",
            use = "sig"
        }, new JsonSerializerOptions { WriteIndented = true });

        return (key.ExportPkcs8PrivateKeyPem(), jwk);
    }
}
