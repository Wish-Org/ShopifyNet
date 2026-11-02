using System.Security.Cryptography;
using System.Text.Json;

namespace ShopifyNet;

/// <summary>
/// Generates signing keys for Shopify application-only authentication.
/// </summary>
public static class ShopifyApplicationOnlyKeys
{
    /// <summary>
    /// Generates a new P-256 elliptic-curve signing key pair for application-only access tokens using ES256.
    /// Returns an unencrypted PKCS#8 PEM private key and an indented public JWK for Shopify's Dev Dashboard.
    /// Keep the private key secure; upload only the public JWK. Each call creates a new key pair.
    /// See https://shopify.dev/docs/apps/build/authentication-authorization/application-only-access-tokens
    /// </summary>
    public static (string PrivateKeyPem, string PublicKeyJwk) GenerateKeyPair(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (keyId.Length > 255 || keyId != keyId.Trim())
            throw new ArgumentException("Key ID must be at most 255 characters and have no leading or trailing whitespace.", nameof(keyId));

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = key.ExportParameters(includePrivateParameters: false);
        var jwk = JsonSerializer.Serialize(new
        {
            kty = "EC",
            crv = "P-256",
            x = Convert.ToBase64String(publicKey.Q.X).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            y = Convert.ToBase64String(publicKey.Q.Y).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            kid = keyId,
            alg = "ES256",
            use = "sig"
        }, new JsonSerializerOptions { WriteIndented = true });

        return (key.ExportPkcs8PrivateKeyPem(), jwk);
    }
}
