# Platform mocks

Local stand-ins for the Altinn platform APIs the notifications API calls, built on [WireMock.Net](https://github.com/wiremock/WireMock.Net). Each system runs as its own server on its own port and can be turned off on its own:

| System | Port | Replaces |
| :--- | :--- | :--- |
| `profile` | 5131 | Contact point lookups in Altinn Profile |
| `register` | 5121 | The name lookup in Altinn Register |
| `authorization` | 5151 | The decision endpoint in Altinn Authorization |
| `conditions` | 5199 | Condition endpoints that orders point to |
| `maskinporten` | 5197 | The token endpoint the API's condition client gets its token from |

The answers are built from the request, so every person and organisation has contact points and a name. Use any 11-digit national identity number and 9-digit organisation number, including random ones from k6.

Only made-up test data passes through the mocks, so every request and response is logged in full. With Aspire the entries show up in the dashboard's structured logs, under the API's trace.

Unlike the other tools this one has no menu: it is a server that runs until you stop it.

## What the mocks answer

### Profile

| Endpoint | Answer |
| :--- | :--- |
| `POST /profile/api/v1/users/contactpoint/lookup` | Each person: email `<nin>@example.com`, mobile `MobileNumber`, not reserved |
| `POST /profile/api/v1/users/contactpoint/lookupsi` | `idporten-email` identities get the email from the identity. Others, like `legacy-selfidentified`, get `<username>@example.com` and `MobileNumber`. |
| `POST /profile/api/v1/organizations/notificationaddresses/lookup` | Each organisation: email `<orgno>@example.com`, mobile `MobileNumber` |
| `POST /profile/api/v1/units/contactpoint/lookup` | Each organisation has one user with registered contact points, with national identity number `<orgno>01` |

Party ids and user ids are made from the identifiers, so they stay the same across requests and restarts.

These identifiers are exceptions, set in `appsettings.json`:

| Identifier | Exception |
| :--- | :--- |
| `99999999901` | Reserved against electronic contact |
| `88888888801` | No email and no mobile number |
| `999999999` | No notification addresses and no users with registered contact points |

### Register

`POST /register/api/v1/parties/nameslookup` names persons `Testperson <nin>` and organisations `Testorganisasjon <orgno> AS`, so `$recipientName$` is filled in.

### Authorization

`POST /authorization/api/v1/decision` permits every request in a XACML multi-request, except for the user ids in `DeniedUsers`. Each result echoes the attributes the request marked with `IncludeInResult`, since the API matches results to users and parties by them.

### Conditions

| URL | Answer | The API |
| :--- | :--- | :--- |
| `http://local.altinn.cloud:5199/condition/true` | `{"sendNotification":true}` | Sends |
| `http://local.altinn.cloud:5199/condition/false` | `{"sendNotification":false}` | Does not send |
| `http://local.altinn.cloud:5199/condition/corrupt` | 200 with a body that is not JSON | Retries the order once, then sends anyway |
| `http://local.altinn.cloud:5199/condition/error` | 500 | Retries the order once, then sends anyway |

The API only accepts condition URLs with a top-level domain, so use `local.altinn.cloud`, which resolves to 127.0.0.1, not `localhost`.

The API calls condition endpoints with a Maskinporten token, which it gets from the Maskinporten mock. The condition mock does not check the token.

### Maskinporten

`POST /token` takes the JWT bearer grant `Altinn.ApiClients.Maskinporten` sends and answers with a token for the scope in the grant, valid for 120 seconds. It does not check the signature or the client, so any key and client id work.

The client library has the Maskinporten URLs built in. Pointing it here needs the `TokenEndpoint` and `AssertionAudience` settings from [Altinn/altinn-apiclient-maskinporten#92](https://github.com/Altinn/altinn-apiclient-maskinporten/pull/92), and the grant has to be signed, so the API also needs a `ClientId` and an `EncodedJwk`. The Aspire app host sets all four, with a new key for each run.

### Anything else

Requests no mapping matches get a 404. When `FallbackUrl` is set, they go to `{FallbackUrl}{system}/{path}` instead, e.g. `http://localhost:5096/intercept/profile/profile/api/v1/...`, so an operator can answer them in the simulator. The mapped endpoints are still answered by the mocks.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [Aspire app host](../../aspire/README.md#platform-mocks) starts the mocks as the `platform-mocks` resource and points the API at them. Turn them off with `Mocks:Enabled` or one by one with `Mocks:<System>:Enabled`.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Notifications.Tools.PlatformMocks
```

Then start the API with its endpoints pointing at the mocks:

```bash
PlatformSettings__ApiProfileEndpoint=http://localhost:5131/profile/api/v1/ \
PlatformSettings__ApiRegisterEndpoint=http://localhost:5121/register/api/v1/ \
PlatformSettings__ApiAuthorizationEndpoint=http://localhost:5151/authorization/api/v1/ \
SendConditionClient__MaskinportenSettings__TokenEndpoint=http://localhost:5197/token \
SendConditionClient__MaskinportenSettings__AssertionAudience=http://localhost:5197/ \
SendConditionClient__MaskinportenSettings__ClientId=altinn-notifications-local \
SendConditionClient__MaskinportenSettings__EncodedJwk=<a base64-encoded RSA JWK with its private key> \
dotnet run --project components/api/src/Altinn.Notifications
```

## Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `PlatformMocksSettings:FallbackUrl` | empty | Where unmatched requests go, as `{FallbackUrl}{system}/{path}`. Empty means a 404. |
| `PlatformMocksSettings:<System>:Enabled` | `true` | Starts the `Profile`, `Register`, `Authorization`, `Conditions` or `Maskinporten` server |
| `PlatformMocksSettings:<System>:Port` | `5131`, `5121`, `5151`, `5199`, `5197` | Port the server listens on, on localhost only |
| `PlatformMocksSettings:Profile:MobileNumber` | `+4799999999` | The mobile number everyone gets |
| `PlatformMocksSettings:Profile:ReservedPersons` | `["99999999901"]` | National identity numbers that are reserved |
| `PlatformMocksSettings:Profile:PersonsWithoutContactPoints` | `["88888888801"]` | National identity numbers with no email and no mobile number |
| `PlatformMocksSettings:Profile:OrganizationsWithoutContactPoints` | `["999999999"]` | Organisation numbers with no contact points |
| `PlatformMocksSettings:Authorization:DeniedUsers` | `[]` | User ids that get `Deny` |

There are no secrets.

If you send through a real SMS gateway, for example with the LinkMobility proxy in proxy mode, SMS go to `MobileNumber`. Set it to your own number first.
