# Altinn Notifications SMS Service

This component handles the functionality related to sending SMS through Altinn Notifications.

## Project Structure

### Altinn.Notifications.Sms

The host layer responsible for bootstrapping the service and exposing HTTP endpoints.

Relevant implementations:
- Program.cs
- Delivery report controller
- Instant message controller

### Altinn.Notifications.Sms.Core

The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure layer
- Domain models
- Services for handling sending of SMS

### Altinn.Notifications.Sms.Integrations

The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Sms.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Client for external SMS delivery service (Link Mobility)
- ASB message publishers and handlers (Wolverine)

## 🏗 Building & Running

**Build:**
```bash
dotnet build Altinn.Notifications.Sms.slnx
```

**Run:**
```bash
cd src/Altinn.Notifications.Sms
dotnet run
```

**Test:**
```bash
dotnet test Altinn.Notifications.Sms.slnx
```

## 🔍 Logging LinkMobility payloads

To see exactly what the service sends to and gets back from LinkMobility, e.g. to track down special characters, control
characters or line breaks in SMS texts, set `SmsGatewaySettings:LogPayloads`:

| Value | Effect |
|---|---|
| `Off` (default) | Nothing is logged. |
| `Plain` | Each call is logged as one entry in curl -v style. |

In a deployed environment, set it as an environment variable on the container and restart the service:

```bash
SmsGatewaySettings__LogPayloads=Plain
```

Each entry has the request line and headers (`>`), the response status line and headers (`<`), the elapsed time, and for each body:

- the text decoded by the charset in its `Content-Type`
- `Escaped:` the same text with invisible characters made visible: `\r`, `\n`, `\t`, other control characters as `\u0007`,
  and special spaces, format characters, combining marks and the replacement character for invalid bytes as `[U+00A0]`, `[U+FFFD]`
- `Hex:` the exact bytes, so line endings and the encoding are as the service sends them on Linux

Only the content of `<PW>` and any `Authorization` header are masked. `<PW>` is masked in the bytes, and the hex is left out if it
cannot be found there. Headers that the socket handler adds below the HTTP client pipeline, such as `Host` and `Connection`,
are not shown. The bytes that are sent are not changed.

The entries are logged at Warning, so they pass the Warning filter on OpenTelemetry in Staging, with the category
`Altinn.Notifications.Sms.Integrations.LinkMobility.PayloadLoggingHandler`. In Application Insights:

```kusto
traces
| where message startswith "LinkMobility payload"
| order by timestamp desc
```

> [!WARNING]
> `Plain` logs recipient phone numbers and message texts, which can hold one-time codes, to Application Insights.
> Use it only in an AT environment, for as short a time as possible, and set it back to `Off` afterwards.
> The service refuses to start with `Plain` when the environment is Production, and logs a warning at startup when it is on.

## 🐳 Containerization

To build the SMS Service container (from the repo root):

**Podman (Preferred):**
```bash
podman build -t notifications-sms -f components/sms-service/Dockerfile .
```

**Docker:**
```bash
docker build -t notifications-sms -f components/sms-service/Dockerfile .
```

## 📚 Additional Resources

For full setup instructions including database configuration and user secrets, see [getting-started.md](../../getting-started.md).
