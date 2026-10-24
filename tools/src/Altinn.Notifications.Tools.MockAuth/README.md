# Mock auth

A local token issuer, so the API can be called without a token from at23. It issues service owner and end user tokens and publishes the OpenID configuration and JWKS the API needs to validate them.

The tokens are signed with `jwtselfsignedcert.pfx`, the self-signed certificate the API's tests use, and carry the same claims as `PrincipalUtil.GetOrgToken` and `PrincipalUtil.GetUserToken` in those tests. The API validates the signature, lifetime and claims, not the issuer or audience, so it accepts them once `GeneralSettings:OpenIdWellKnownEndpoint` points here.

Unlike the other tools this one has no menu: it is a server that runs until you stop it.

## Endpoints

| Endpoint | What it does |
| :--- | :--- |
| `GET /api/GetEnterpriseToken?org=ttd&scopes=...` | A service owner token, same shape as the Altinn test token generator. Needs basic auth, `mock` / `mock` by default. `orgNumber` is optional. |
| `GET /api/GetPersonalToken?userId=...&partyId=...&pid=...&scopes=...` | An end user token, like the test token generator's personal token. Same basic auth. Every parameter is optional, and `authLvl` defaults to 3. |
| `GET /authentication/api/v1/openid/.well-known/openid-configuration` | OpenID configuration pointing to the JWKS |
| `GET /authentication/api/v1/openid/jwks` | The public signing key |

Scopes can be separated by commas or spaces, as with the test token generator. In the token they are separated by spaces.

## Tokens the API accepts

Each policy in `AddAuthorizationRulesAndHandlers` in the API's `Program.cs` checks one scope, with the scope handler from `Altinn.Common.PEP`. It reads `urn:altinn:scope`, or `scope` when there is none, so service owner tokens carry their scopes in `urn:altinn:scope` and end user tokens in `scope`, like the real ones.

| What | Token | Scope |
| :--- | :--- | :--- |
| Orders, status, service owner notification log | `GetEnterpriseToken` | `altinn:serviceowner/notifications.create` |
| Composed email (`orders/composed-email`) | `GetEnterpriseToken` | `altinn:serviceowner/notifications.composedemail.create` |
| Support dashboard (`dashboard/...`) | `GetEnterpriseToken` | `altinn:notifications.support.admin` |
| End user notification log (`enduser/log`) | `GetPersonalToken` | `altinn:portal/enduser`. The API also asks Dialogporten, with the same token, whether the user may see the dialog. The Aspire app host points it at the [Dialogporten mock](../Altinn.Notifications.Tools.PlatformMocks/README.md#dialogporten). |

Platform access tokens (`PrincipalUtil.GetAccessToken`), which other Altinn apps can use instead of the create scope, are not issued here. The API validates them against keys of its own.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [Aspire app host](../../aspire/README.md#authentication) starts it as the `auth` resource and points the API at it, unless `Auth:Mode` is `at23`.

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

There are no secrets. The certificate and its password are test-only and already in the repository.

## Bruno

With the `v2 aspire` environment, the token requests in `test-util` fetch from this issuer. In the other environments they use the Altinn test token generator from `.env`.

| Request | Stores | Used by |
| :--- | :--- | :--- |
| `new auth token` | `jwt`, with both create scopes | `v1`, `v2 (future)` and the other service owner folders |
| `dashboard-auth-token` | `dashboard_jwt` | `dashboard` |
| `end user dialog token` | `enduser_jwt`, for `AuthN_UserId`, `AuthN_PartyId` and `AuthN_Pid` | `enduser` |

The folders fetch their token themselves when it is missing, older than 15 minutes or from another environment.
