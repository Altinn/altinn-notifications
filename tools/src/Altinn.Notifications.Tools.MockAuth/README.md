# Mock auth

A local token issuer, so the API can be called without a token from at23. It issues service owner tokens and publishes the OpenID configuration and JWKS the API needs to validate them.

The tokens are signed with an RSA key that only exists in memory, and carry the same claims as `PrincipalUtil.GetOrgToken` in the API's tests. The API validates the signature, lifetime and claims, not the issuer or audience, so it accepts them once `GeneralSettings:OpenIdWellKnownEndpoint` points here.

Unlike the other tools this one has no menu: it is a server that runs until you stop it.

## Endpoints

| Endpoint | What it does |
| :--- | :--- |
| `GET /api/GetEnterpriseToken?org=ttd&scopes=...` | A service owner token, same shape as the Altinn test token generator. Needs basic auth, `mock` / `mock` by default. `orgNumber` is optional. |
| `GET /authentication/api/v1/openid/.well-known/openid-configuration` | OpenID configuration pointing to the JWKS |
| `GET /authentication/api/v1/openid/jwks` | The public signing key |

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [Aspire app host](../../aspire/README.md#authentication) starts it as the `mock-auth` resource and points the API at it, unless `Auth:Mode` is `at23`.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Notifications.Tools.MockAuth
```

Then start the API with:

```bash
GeneralSettings__OpenIdWellKnownEndpoint=http://localhost:5094/authentication/api/v1/openid/ dotnet run --project components/api/src/Altinn.Notifications
```

## Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `MockAuthSettings:Port` | `5094` | Port the issuer listens on, on localhost only |
| `MockAuthSettings:Username` | `mock` | Basic auth username for the token endpoint |
| `MockAuthSettings:Password` | `mock` | Basic auth password for the token endpoint |
| `MockAuthSettings:SigningKey` | empty | RSA key to sign with, as a base64-encoded PKCS#8 private key. Empty makes a new key each time the issuer starts. The Aspire app host makes one per run, so tokens stay valid when only the issuer restarts. |

There are no secrets in the repository. The signing key is made at startup, and the basic auth credentials only guard a local test issuer. Generating those per run as well is #2017.

## Bruno

Select the `v2 aspire` environment and run `test-util/new auth token`. It fetches a token from this issuer and stores it as `jwt` for the other requests.
