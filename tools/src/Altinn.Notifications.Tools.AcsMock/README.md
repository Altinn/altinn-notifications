# ACS mock

A local stand-in for Azure Communication Services (ACS) Email, so the email service can send emails without an ACS resource. It answers the two REST calls the `Azure.Communication.Email` SDK makes, checks that they are signed with the right access key, and sends the delivery reports the API waits for.

Unlike most tools this one has no menu: it is a server that runs until you stop it.

## How the email service uses ACS

1. `EmailServiceClient` sends with `EmailClient.SendAsync(WaitUntil.Started, …)`: `POST /emails:send?api-version=2025-09-01`. ACS answers 202 with the operation id.
2. `CheckEmailSendStatusHandler` polls the operation every 8 seconds: `GET /emails/operations/{id}?api-version=2025-09-01`, until it is `Succeeded` or `Failed`. The email service then sends `Succeeded` or a failure to the API.
3. Later ACS publishes an `EmailDeliveryReportReceived` event. In Azure, an Event Grid subscription puts it on the Service Bus queue `altinn.notifications.email.deliveryreports`. The API's `EmailDeliveryReportHandler` reads it and sets the final status, e.g. `Delivered` or `Failed_Bounced`.

The mock does all three. Delivery reports go straight onto the emulator's queue as the raw Event Grid event, the same body the Event Grid subscription delivers.

## Endpoints

| Endpoint | What it does |
| :--- | :--- |
| `POST /emails:send` | Accepts the email and returns 202 with `Operation-Location` and `operation-id`. Uses the SDK's `Operation-Id` header as the id, so a retried send returns the same operation. |
| `GET /emails/operations/{id}` | `Running` for `RunningSeconds`, then `Succeeded` or `Failed`. The first `Succeeded` poll queues the delivery report. |
| Anything else | Forwarded to `{FallbackUrl}acs/{path}` when `FallbackUrl` is set, otherwise 404 |

### Signature

The SDK signs every request with HMAC-SHA256 using the `accesskey` from the connection string, in `x-ms-date`, `x-ms-content-sha256` and `Authorization: HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature=…`. The mock checks the body hash and the signature against `AccessKey`, and answers 401 `Denied` when they do not match, like ACS. The log says why it was rejected.

The SDK accepts an `http` endpoint, so no certificate is needed: `endpoint=http://localhost:5095/;accesskey=<AccessKey>`.

## Scenarios

The local part of the first recipient that matches a keyword picks what happens. A `+tag` is ignored, so `bounce+1@example.com` works too. Any other address is delivered.

| Recipient | Send | Poll | Delivery report | Ends as |
| :--- | :--- | :--- | :--- | :--- |
| anything else | 202 | `Succeeded` | `Delivered` | `Email_Delivered` |
| `bounce@…` | 202 | `Succeeded` | `Bounced` | `Email_Failed_Bounced` |
| `quarantine@…` | 202 | `Succeeded` | `Quarantined` | `Email_Failed_Quarantined` |
| `spam@…` | 202 | `Succeeded` | `FilteredSpam` | `Email_Failed_FilteredSpam` |
| `noreport@…` | 202 | `Succeeded` | none | `Email_Succeeded` |
| `slow@…` | 202 | `Running` for `SlowSeconds` | `Delivered` | `Email_Delivered` |
| `fail@…` | 202 | `Failed` with `EmailSendFailed` | none | `Email_Failed` |
| `suppressed@…` | 202 | `Failed` with `EmailDroppedAllRecipientsSuppressed` | none | `Email_Failed_SuppressedRecipient` |
| `invalid@…` | 400 `Invalid format for email address.` | | none | `Email_Failed_InvalidFormat` |
| `throttle@…` | 429 with `Retry-After` on the first attempt, then like the default | | `Delivered` | `Email_Delivered` |

`throttle@` makes the email service pause all sending for `ThrottleRetryAfterSeconds`, as it does when ACS throttles.

### Logs

Every request is logged with the response. Recipient addresses keep only their first character and domain, e.g. `o***@example.com`. Subjects, bodies, display names and attachments are replaced with their length, since they can hold personal data or one-time codes. Under Aspire the logs and traces also go to the dashboard.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* The Service Bus emulator from `tools/asb-emulator` for delivery reports. Without it the mock still answers, but the sends stay at `Email_Succeeded`.

## Run with Aspire

The [Aspire app host](../../aspire/README.md#email-through-the-acs-mock) starts it as the `acs` resource and points the email service at it, unless `Acs:Mode` is `real`.

## Run on its own

```bash
dotnet run --project tools/src/Altinn.Notifications.Tools.AcsMock
```

Then start the email service with:

```bash
CommunicationServicesSettings__ConnectionString="endpoint=http://localhost:5095/;accesskey=YWx0aW5uLW5vdGlmaWNhdGlvbnMtYWNzLW1vY2stdGVzdC1rZXk=" dotnet run --project components/email-service/src/Altinn.Notifications.Email
```

## Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `AcsMockSettings:Port` | `5095` | Port the mock listens on, on localhost only |
| `AcsMockSettings:AccessKey` | a fixed test key | Base64 key requests must be signed with. It is test-only and already in the repository. |
| `AcsMockSettings:RunningSeconds` | `2` | How long a send stays `Running` |
| `AcsMockSettings:SlowSeconds` | `30` | How long a send to `slow@` stays `Running` |
| `AcsMockSettings:ThrottleRetryAfterSeconds` | `10` | `Retry-After` for `throttle@` |
| `AcsMockSettings:DeliveryReportDelaySeconds` | `2` | Delay between the first `Succeeded` poll and the delivery report |
| `AcsMockSettings:ServiceBusConnectionString` | the local emulator | Where delivery reports go. Empty turns them off. |
| `AcsMockSettings:DeliveryReportQueueName` | `altinn.notifications.email.deliveryreports` | The queue the API reads delivery reports from |
| `AcsMockSettings:FallbackUrl` | empty | Where unknown requests go, as `{FallbackUrl}acs/{path}`, e.g. `http://localhost:5096/intercept/` for the simulator |

There are no secrets.

## Limits

* Operations live in memory. After a restart of the mock, polls for older operations get 404, which the email service treats as failed.
* One delivery report is sent per recipient, all with the operation's scenario.
* Attachments are accepted but not checked, and there is no size limit or 413.
