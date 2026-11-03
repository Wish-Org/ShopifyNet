[![NuGet](https://img.shields.io/nuget/v/ShopifyNet.svg)](https://www.nuget.org/packages/ShopifyNet)

ShopifyNet is a .NET library which enables you to interact with the Shopify GraphQL API with .NET.

## Installation

To install ShopifyNet, run the following command in your .NET project:

```bash
dotnet add package ShopifyNet
```

## Features

-   Strongly typed queries, mutations, input objects. All API types have been pregenerated and are available in the `ShopifyNet.Types` namespace.
-   Support for request interception. This enables centralized logic for logging, retries, rate limit policies, testing, error handling...
-   Built-in smart interceptor automatically handles retries and rate limits
-   The `ShopifyNet.ShopifyClient` is thread safe.
-   Ability to provide a custom `HttpClient` instance

### Example

```csharp
[TestMethod]
public async Task QuerySimple()
{
    var query = """
        query {
            products(first: 10)
            {
                nodes
                {
                    id
                    title
                }
            }
        }
        """;

    var response = await _client.QueryAsync(query);
    //response is strongly typed
    Assert.IsNotNull(response.data.products.nodes.FirstOrDefault()?.id);
    Assert.IsNull(response.errors);
}
```

See unit tests for more examples.

### OAuth access tokens

`ShopifyOAuthClient` retains only HTTP transport configuration. Supply the client ID and, where required, client secret to each operation. This avoids requiring unused credentials and lets one instance serve multiple apps. `BuildAuthorizationUrl` and `IsValidHmac` are static helpers.

**Breaking API change:** replace `new ShopifyOAuthClient(clientId, clientSecret, httpClient)` with `new ShopifyOAuthClient(httpClient)`, move credentials to the token method calls, and invoke the helpers on `ShopifyOAuthClient` as shown below. Token response types are unchanged.

#### Generate an application-only signing key pair

Generate a P-256 key pair for ES256 or an RSA-3072 key pair for RS256, following [Shopify's application-only access token guide](https://shopify.dev/docs/apps/build/authentication-authorization/application-only-access-tokens):

```sh
dotnet run --project ShopifyNet.KeyPairGenerator --framework net10.0 -- betterreports-2026-11 ES256
dotnet run --project ShopifyNet.KeyPairGenerator --framework net10.0 -- betterreports-rsa-2026-11 RS256
```

The console prints an unencrypted PKCS#8 PEM private key and a public JWK. ES256 keys contain `kty`, `crv`, `x`, and `y`; RS256 keys contain `kty`, `n`, and `e`. Both include `kid`, `alg`, and `use` metadata. Paste only the JWK into **Dev Dashboard > Settings > Credentials > Public keys > Add key**. Store the private key securely for signing client assertions; do not commit it. Choose a key ID that is unique among your app's active keys, at most 255 characters, without surrounding whitespace. Every run generates a new key pair.

You can also call the generator from your own console application without app credentials:

```csharp
var keys = ShopifyClientAssertionKeys.GenerateKeyPair("betterreports-2026-11", "ES256");
Console.WriteLine(keys.PrivateKeyPem);
Console.WriteLine(keys.PublicKeyJwk);
```

Both the library method and console command require two arguments: the key ID and signing algorithm (`ES256` or `RS256`, case-sensitive). This generates signing keys only; it does not register the public key or request an access token.

#### Run the application-only integration test

Install your app on a development store and register the public JWK in Shopify first. Set these variables in PowerShell, using the key ID registered with the matching P-256 or RSA private key:

```powershell
$env:SHOPIFYNET_RUN_OAUTH_INTEGRATION_TESTS = "1"
$env:SHOPIFYNET_SHOP_ID = "your-store.myshopify.com"
$env:SHOPIFYNET_CLIENT_ID = "your-client-id"
$env:SHOPIFYNET_KEY_ID = "your-registered-key-id"
$env:SHOPIFYNET_PRIVATE_KEY_PATH = "C:\secure\private.pem"
dotnet test ShopifyNet.Tests --framework net10.0 --filter FullyQualifiedName~ShopifyClientAssertionIntegrationTests
```

The test creates a fresh ES256 or RS256 assertion and exchanges it for an access token, then checks the token, scopes, and positive expiry. No client secret is needed. It skips unless explicitly enabled and does not print the key, assertion, or token. Remove the opt-in variable afterward with `Remove-Item Env:SHOPIFYNET_RUN_OAUTH_INTEGRATION_TESTS`.

#### Acquire, migrate and refresh tokens

Request an application-only access token using your registered key ID and securely stored P-256 or RSA private key. No client secret is needed. Credentials are passed to each operation; the constructor accepts only an optional `HttpClient`:

```csharp
var oauthClient = new ShopifyOAuthClient();
ShopifyClientAssertionAccessToken applicationToken = await oauthClient.RequestClientAssertionAccessTokenAsync(
    clientId, myShopifyDomain, keyId, privateKeyPem, cancellationToken);
```

`RequestClientAssertionAccessTokenAsync` creates a fresh ES256 or RS256 assertion internally, with the store's token endpoint as its audience, a 60-second lifetime, and a unique `jti`. It selects ES256 for P-256 keys and RS256 for RSA keys (2048–8192 bits), rejecting unsupported keys. Unencrypted PKCS#8, EC PRIVATE KEY, and RSA PRIVATE KEY PEM formats are supported. Supply exactly one PEM block with optional surrounding whitespace; encrypted keys, multiple blocks, and additional non-whitespace content are rejected. It sends the assertion using the `client_credentials` grant and supplied client ID, without a client secret or scope parameter, as described in [Shopify's guide](https://shopify.dev/docs/apps/build/authentication-authorization/application-only-access-tokens#step-5-request-an-access-token).

Use `applicationToken.access_token` for GraphQL requests and its returned `expires_in` to determine when to request a new token. There is no refresh token. The method does not retry automatically: call it again to retry with a fresh assertion. The default HTTP client rejects redirects; if supplying your own `HttpClient`, disable redirects and automatic retries and do not configure an Authorization header.

`ShopifyOAuthClient` calls the Shopify OAuth access token endpoint to acquire, migrate and refresh access tokens.
Public apps must use [expiring offline access tokens](https://shopify.dev/docs/apps/build/authentication-authorization/migrate-to-expiring-offline-access-tokens) for the GraphQL Admin API from January 1, 2027.

```csharp
var oauthClient = new ShopifyOAuthClient();

//migrate a non-expiring offline token. The old token is invalidated, so the new token must be persisted.
ShopifyOfflineAccessToken token = await oauthClient.MigrateToExpiringOfflineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, nonExpiringToken);

//refresh before expires_in seconds have elapsed since acquisition. The refresh token is rotated, so the new token must be persisted.
token = await oauthClient.RefreshOfflineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, token.refresh_token);

//exchange a validated ID (session) token for an expiring offline or an online access token
token = await oauthClient.ExchangeIdTokenForOfflineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, idToken);
ShopifyOnlineAccessToken onlineToken = await oauthClient.ExchangeIdTokenForOnlineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, idToken);

//exchange an authorization code from a validated OAuth callback for an expiring offline token
token = await oauthClient.ExchangeCodeForOfflineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, offlineCode, expiring: true);

//request a non-expiring offline token instead (using a separate authorization code)
token = await oauthClient.ExchangeCodeForOfflineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, nonExpiringOfflineCode, expiring: false);

//for an online token, the authorization URL must have included grant_options[]=per-user
onlineToken = await oauthClient.ExchangeCodeForOnlineAccessTokenAsync(clientId, clientSecret, myShopifyDomain, onlineCode);

var client = new ShopifyClient(myShopifyDomain, token.access_token);
```

Failures returned by Shopify throw a `ShopifyOAuthException` exposing the `StatusCode`, `Error` and `ErrorDescription`.

Online authorization-code exchange responses can omit `expires_in`. `ShopifyOnlineAccessToken.expires_in` is nullable and remains null when expiry metadata is absent. Null means the expiry is unknown, not that the token never expires. Callers are responsible for tracking acquisition time and persisting expiry dates calculated from the returned lifetimes.

For the [authorization code flow](https://shopify.dev/docs/apps/build/authentication-authorization/authenticate-standalone-apps), validate the callback's HMAC, state and shop before passing its code to these methods. The authorization request determines the token's access mode: omit `grant_options[]=per-user` for offline access and include it for online access. Each exchange needs its own authorization code. Pass `expiring: true` to send `expiring=1` and receive a refresh token alongside the access token. Pass `expiring: false` to omit that parameter and request a non-expiring token. Offline token expiry fields (`expires_in`, `refresh_token_expires_in`) are nullable; they and `refresh_token` are null for non-expiring tokens.

### Build an authorization URL

Use `BuildAuthorizationUrl` to start the [authorization code flow](https://shopify.dev/docs/apps/build/authentication-authorization/authenticate-standalone-apps). It validates the shop domain, URL-encodes the parameters and uses the supplied client ID. It makes no network request and never includes the client secret.

```csharp
var state = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
// Store state in the initiating browser's session before redirecting.
var authorizationUrl = ShopifyOAuthClient.BuildAuthorizationUrl(
    clientId, myShopifyDomain,
    scopes: "read_products,write_orders",
    redirectUri: "https://your-app.example.com/oauth/callback",
    state: state,
    online: false);
// Redirect the merchant to authorizationUrl.
```

Set `online: true` to include `grant_options[]=per-user` for an online token. Offline access is the default; choose expiring or non-expiring offline access when exchanging the resulting code. The callback URI must match one registered with Shopify. Validate and consume the stored state when handling the callback, together with the HMAC and shop checks below.

### OAuth callback validation

All OAuth token requests reject domains other than a plain `shop.myshopify.com` hostname before sending credentials. `ShopifyOAuthClient.IsValidShopDomain` also exposes this check for authorization redirects and callbacks.

`ShopifyOAuthClient.IsValidHmac` verifies the OAuth query signature using the supplied client secret and a constant-time comparison. Supply every URL-decoded query parameter, including `hmac`, without filtering fields or collapsing duplicates. It rejects missing, malformed or incorrect HMACs and duplicate keys. Before signing, it sorts the parameters and escapes `%` as `%25` and `&` as `%26` in keys and values, plus `=` as `%3D` in keys only. Other characters, including JSON punctuation, spaces and base64 padding, remain literal. This signing format is separate from URL encoding used to transport the callback. It does not validate the state nonce or timestamp freshness, and is not a webhook or app-proxy verifier.

For example, in an ASP.NET Core controller callback, with `expectedState` retrieved from the initiating browser's session:

```csharp
var parameters = Request.Query
    .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value)))
    .ToArray();
var shop = Request.Query["shop"].ToString();

if (!ShopifyOAuthClient.IsValidHmac(parameters, clientSecret) || !ShopifyOAuthClient.IsValidShopDomain(shop))
    return Unauthorized();

if (string.IsNullOrEmpty(expectedState) || Request.Query["state"].ToString() != expectedState)
    return Unauthorized();

// Consume the stored state so it cannot be reused, then exchange the signed code.
var token = await oauthClient.ExchangeCodeForOfflineAccessTokenAsync(
    clientId, clientSecret, shop, Request.Query["code"].ToString(), expiring: true);
```
