# LinkMobility proxy

A local stand-in for the LinkMobility SMS gateway, built on [WireMock.Net](https://github.com/wiremock/WireMock.Net). Point the SMS service at it to see exactly what is sent to LinkMobility and what comes back, without credentials or personal data ending up in the log.

It runs in one of two modes:

| Mode | When | What happens |
| :--- | :--- | :--- |
| Stub | `LinkMobilityProxySettings:Target` is empty (default) | Answers every request with `LOGON OK` / `STATUS OK`. Nothing leaves the machine and no credentials are needed. |
| Proxy | `LinkMobilityProxySettings:Target` is set, e.g. `https://xml-test.pswin.com` | Forwards every request unchanged to the target and returns its response. **Real SMS are sent.** |

In both modes every request and response is logged with:

* `<PW>` replaced by `***`
* `<RCV>` reduced to its last two digits, e.g. `+********56`
* `<TEXT>` replaced by its length, since messages can hold one-time codes

Only the log is masked. In proxy mode the gateway receives the payload unchanged.

Unlike the other tools this one has no menu: it is a server that runs until you stop it.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [Aspire app host](../../aspire/README.md) starts the proxy as the `linkmobility` resource and points the `sms` service at it. The masked payloads show up in the dashboard's structured logs, under the same trace as the SMS service's call.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Notifications.Tools.LinkMobilityProxy
```

Then start the SMS service with its gateway endpoint pointing at the proxy:

```bash
SmsGatewaySettings__Endpoint=http://localhost:5093 dotnet run --project components/sms-service/src/Altinn.Notifications.Sms
```

## Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `LinkMobilityProxySettings:Port` | `5093` | Port the proxy listens on, on localhost only |
| `LinkMobilityProxySettings:Target` | empty | Gateway to forward to. Empty means stub mode. |

The proxy has no secrets of its own. In proxy mode the SMS service still sends its own LinkMobility credentials, so set them in the **SMS service's** user secrets:

```bash
dotnet user-secrets set "SmsGatewaySettings:Username" "<username>" --project components/sms-service/src/Altinn.Notifications.Sms
dotnet user-secrets set "SmsGatewaySettings:Password" "<password>" --project components/sms-service/src/Altinn.Notifications.Sms
```
