# LinkMobility proxy

A local stand-in for the LinkMobility SMS gateway, built on [WireMock.Net](https://github.com/wiremock/WireMock.Net). Point the SMS service at it to see exactly what is sent to LinkMobility and what comes back, without credentials or personal data ending up in the log, or, to debug, [byte for byte](#plain-payload-logging). In stub mode it also sends the delivery reports the SMS service waits for, so an SMS reaches its final status locally.

It runs in one of three modes:

| Mode | When | What happens |
| :--- | :--- | :--- |
| Stub | `Target` is empty (default) | Answers each send by the recipient's [scenario](#scenarios) and posts a delivery report to the SMS service afterwards. Nothing leaves the machine and no credentials are needed. |
| Interactive | `Target` is empty, `Interactive` is `true` and `FallbackUrl` is set | Every send goes to the [test harness control panel](../Altinn.Notifications.Tools.TestHarness/README.md), where an operator answers it. A delivery report follows for each message the answer accepts. |
| Proxy | `Target` is set, e.g. `https://xml-test.pswin.com` | Forwards every request unchanged to the target and returns its response. **Real SMS are sent.** LinkMobility sends the delivery reports to the endpoint registered for the account, not to the proxy. |

Unlike most tools this one has no menu: it is a server that runs until you stop it.

## How the SMS service uses LinkMobility

1. `SmsClient` sends through LinkMobility's `GatewayClient`, which posts a `SESSION` with the username, password and one `MSG` per SMS to the root of `SmsGatewaySettings:Endpoint`. LinkMobility answers `STATUS OK` with a gateway reference (`REF`) per message, or `STATUS FAIL` with a reason in `INFO`, e.g. `Invalid RCV`.
2. The SMS service sends `Accepted` with the reference, or the failure, to the API.
3. Later LinkMobility posts a delivery report, a `MSGLST` with the `REF`, the recipient (`RCV`) and a `STATE` such as `DELIVRD`, to `POST /notifications/sms/api/v1/reports` with basic auth. The SMS service checks the user and password against `SmsDeliveryReportSettings:UserSettings`, maps the state with `SmsSendResultMapper` and puts the result on the `altinn.notifications.sms.deliveryreports` queue, where the API sets the final status.

In stub mode the proxy does steps 1 and 3.

## Scenarios

In stub mode the last two digits of the recipient number pick what happens. Any other number is delivered, so the `+4799999999` the platform mocks give every person ends as `SMS_Delivered`.

| Recipient ends in | Send | Delivery report | SMS service result | Ends as |
| :--- | :--- | :--- | :--- | :--- |
| anything else, e.g. `00` | `OK` with a new `REF` | `DELIVRD` | `Delivered` | `SMS_Delivered` |
| `01` | `OK` | `UNDELIV` | `Failed_Undelivered` | `SMS_Failed_Undelivered` |
| `02` | `OK` | `EXPIRED` | `Failed_Expired` | `SMS_Failed_Expired` |
| `03` | `OK` | none | | `SMS_Accepted` |
| `04` | `FAIL` with `Invalid RCV` | none | `Failed_InvalidRecipient` | `SMS_Failed_InvalidRecipient` |
| `05` | `OK` | `REJECTD` | `Failed_Rejected` | `SMS_Failed_Rejected` |
| `06` | `OK` | `BARRED` | `Failed_BarredReceiver` | `SMS_Failed_BarredReceiver` |
| `07` | `OK` | `DELETED` | `Failed_Deleted` | `SMS_Failed_Deleted` |
| `08` | `OK` | `FAILED` | `Failed` | `SMS_Failed` |

The report comes `DeliveryReportDelaySeconds` after the send and continues the send's trace. If the SMS service does not answer 2xx, the proxy tries twice more, 2 seconds apart.

In interactive mode the scenario only picks the report's `STATE`: the operator decides whether the send is accepted, and a report follows for each message the answer gives `STATUS OK` and a `REF`, e.g. with the control panel's `OK with REF` template.

## Control panel fallback

With `FallbackUrl` set, requests the stub does not recognise, such as another path or a body that is not a `SESSION`, go to `{FallbackUrl}linkmobility/{path}`, e.g. `http://localhost:5096/intercept/linkmobility/unknown`. Without it they get 404.

## Copies to the control panel

With `MirrorUrl` set, e.g. to `http://localhost:5096/mirror/`, the stub sends a copy of each message it receives (sender, recipient, text, TTL, gateway reference and scenario) to the [test harness control panel](../Altinn.Notifications.Tools.TestHarness/README.md#phone)'s Phone tab, and the state of each delivery report it sends. The copies are unmasked, so only point `MirrorUrl` at localhost.

The copies are queued and posted in the background, two seconds at most each. When the control panel is slow or not running, they are dropped: a send is never held up or failed by them. Proxy mode sends no copies.

## Logs

By default every request and response is logged, in all modes, with:

* `<PW>` replaced by `***`
* `<RCV>` reduced to its last two digits, e.g. `+********56`, which are also the scenario digits
* `<TEXT>` replaced by its length, since messages can hold one-time codes

The log line also shows the scenario, e.g. `LinkMobility stub POST / -> 200 (Undelivered)`, and each delivery report is logged with its state and the masked report. Only the log is masked. In proxy and interactive mode the target receives the payload unchanged.

### Plain payload logging

To debug what reaches LinkMobility, e.g. special characters, control characters or line breaks that come out wrong, set `LogPayloads` to `Plain`. Each request and response is then logged with only `<PW>` masked, in three forms:

```text
LinkMobility stub POST / -> 200 (Delivered)
Request: text/xml; charset=utf-8, 294 bytes, decoded as utf-8
<?xml version="1.0" encoding="iso-8859-1"?><SESSION>
  ...
      <TEXT>Hei
linje
æøå x</TEXT>
  ...
Escaped: <?xml version="1.0" encoding="iso-8859-1"?><SESSION>\r\n  <CLIENT>user</CLIENT>\r\n  <PW>***</PW>\r\n ... <TEXT>Hei\r\nlinje\r\næøå[U+00A0]x</TEXT> ...
Hex: 3C 3F 78 6D 6C ... 48 65 69 0D 0A 6C 69 6E 6A 65 0D 0A C3 A6 C3 B8 C3 A5 C2 A0 78 3C 2F 54 45 58 54 3E ...
Response: text/xml, 213 bytes, decoded as utf-8
...
```

* The text is decoded by the charset in `Content-Type`, or UTF-8 without one.
* `Escaped` shows what cannot be seen: `\r`, `\n` and `\t`, other control characters as `\u0007`, and non-breaking and other special spaces, zero-width and other format characters, combining marks and `U+FFFD` as `[U+00A0]`. `U+FFFD` is what bytes that are not valid in the charset decode to. A backslash is `\\`.
* `Hex` is the bytes as they went over the wire, with only the content of `<PW>` replaced by `***`, so invalid bytes and a wrong charset survive. If the password cannot be found in the bytes, e.g. in a UTF-16 body, the hex is left out rather than shown with it.

With `PayloadFolder` set, each request and response body is also written to a file there, byte for byte with only `<PW>` masked, e.g. `20261026T101530123Z-0001-ref-5803fbdc-…-request.xml` and `…-response.xml`. The name has the time in UTC, a running number and the gateway references from the response, so the pair can be attached to a LinkMobility ticket. The log entry has their paths.

It works in all modes, and the target in proxy mode still receives the payload unchanged.

Plain logging puts recipient numbers and message texts in the log and the files, so:

* it is off by default, and the proxy logs a warning at startup while it is on
* the proxy refuses to start with it unless it listens on localhost only, so it cannot end up in a shared environment
* `PayloadFolder` needs `Plain`, and the proxy refuses to start with only the folder set

What the SMS service's gateway client sends, which the plain log makes visible:

* UTF-8 bytes with `Content-Type: text/xml; charset=utf-8`, under an XML declaration that says `iso-8859-1`
* indented XML, with line breaks as the machine's own: a line break in a message is sent as `\r\n` from Windows and as `\n` from Linux, whatever it was in the order
* no control characters XML 1.0 does not allow, such as `\u0007`: the client throws before sending. The stub cannot read a send with them either, and answers 404.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Run with Aspire

The [Aspire app host](../../aspire/README.md#sms-through-the-linkmobility-proxy) starts the proxy as the `wiremock-linkmobility` resource, points the `sms` service at it and gives both the same delivery report credentials. The masked payloads show up in the dashboard's structured logs, under the same trace as the SMS service's call.

## Run on its own

```bash
LinkMobilityProxySettings__DeliveryReportUrl=http://localhost:5092/notifications/sms/api/v1/reports dotnet run --project tools/src/Altinn.Notifications.Tools.LinkMobilityProxy
```

Then start the SMS service with its gateway endpoint pointing at the proxy:

```bash
SmsGatewaySettings__Endpoint=http://localhost:5093 dotnet run --project components/sms-service/src/Altinn.Notifications.Sms
```

The default delivery report user and password, `username` and `password`, match the SMS service's `appsettings.json`.

## Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `LinkMobilityProxySettings:Port` | `5093` | Port the proxy listens on, on localhost only |
| `LinkMobilityProxySettings:Target` | empty | Gateway to forward to. Empty means stub mode. |
| `LinkMobilityProxySettings:Interactive` | `false` | Sends every send to `FallbackUrl` instead of answering it. Needs `FallbackUrl`. |
| `LinkMobilityProxySettings:FallbackUrl` | empty | Where unknown requests go, as `{FallbackUrl}linkmobility/{path}`, e.g. `http://localhost:5096/intercept/` for the test harness control panel |
| `LinkMobilityProxySettings:DeliveryReportUrl` | empty | The SMS service's delivery report endpoint. Empty turns delivery reports off. |
| `LinkMobilityProxySettings:DeliveryReportUsername` | `username` | Basic auth user, the SMS service's `SmsDeliveryReportSettings:UserSettings:Username` |
| `LinkMobilityProxySettings:DeliveryReportPassword` | `password` | Basic auth password, the SMS service's `SmsDeliveryReportSettings:UserSettings:Password` |
| `LinkMobilityProxySettings:DeliveryReportDelaySeconds` | `2` | Delay between an accepted send and its delivery report |
| `LinkMobilityProxySettings:MirrorUrl` | empty | Where [copies](#copies-to-the-control-panel) of sends and delivery states go, e.g. `http://localhost:5096/mirror/`. Empty means none. |
| `LinkMobilityProxySettings:LogPayloads` | `Masked` | `Plain` logs payloads with only the password masked, see [Plain payload logging](#plain-payload-logging). Only on localhost. |
| `LinkMobilityProxySettings:PayloadFolder` | empty | With `Plain`, a folder that gets a byte-exact copy of each request and response body, e.g. `C:\data\logs\linkmobility` |

The proxy has no secrets of its own. In proxy mode the SMS service still sends its own LinkMobility credentials, so set them in the **SMS service's** user secrets:

```bash
dotnet user-secrets set "SmsGatewaySettings:Username" "<username>" --project components/sms-service/src/Altinn.Notifications.Sms
dotnet user-secrets set "SmsGatewaySettings:Password" "<password>" --project components/sms-service/src/Altinn.Notifications.Sms
```

## Limits

* References and pending reports live in memory. Reports that are still waiting when the proxy stops are lost.
* One report is sent per accepted message, with the final state only. LinkMobility can send intermediate states first.
