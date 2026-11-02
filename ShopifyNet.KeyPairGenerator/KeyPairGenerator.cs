using ShopifyNet;

if (args.Length != 2)
{
    // betterreports-2026-11 is an example key ID format used for Shopify application-only keys. The key ID must be unique across your application keys.
    Console.Error.WriteLine("Usage: dotnet run --project ShopifyNet.KeyPairGenerator --framework net10.0 -- <key-id> <ES256|RS256>");
    Console.Error.WriteLine("Example: dotnet run --project ShopifyNet.KeyPairGenerator --framework net10.0 -- betterreports-2026-11 ES256");
    return 1;
}

try
{
    var (privateKeyPem, publicKeyJwk) = ShopifyClientAssertionKeys.GenerateKeyPair(args[0], args[1]);
    Console.WriteLine("Private key (keep secure; do not upload to Shopify):");
    Console.WriteLine(privateKeyPem);
    Console.WriteLine();
    Console.WriteLine("Public JWK (paste into Shopify Dev Dashboard > Settings > Credentials > Public keys):");
    Console.WriteLine(publicKeyJwk);
    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
