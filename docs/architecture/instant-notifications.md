# Instant notifications

Instant notifications provide a way to send a single SMS or email immediately, without scheduling, queues, or contact point resolution.
They are intended for callers who already have the recipient's address and need low-latency, fire-and-forget delivery.

## Endpoints

Handled by [InstantOrdersController](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications/Controllers/InstantOrdersController.cs) at the base route `notifications/api/v1/future/orders/instant`:

| Method | Path | Description |
|--------|------|-------------|
| POST | `/sms` | Send an instant SMS notification |
| POST | `/email` | Send an instant email notification |

## How it works

Implemented in [InstantOrderRequestService](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Core/Services/InstantOrderRequestService.cs), the flow for both channels is:

1. Validate the request — `400` if invalid
2. Extract the creator organisation from the HTTP context — `403` if missing
3. Check idempotency by creator + `IdempotencyId` — return `200` with existing tracking info if found
4. **Dispatch synchronously via HTTP** to the service component ([ShortMessageServiceClient](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Clients/ShortMessageServiceClient.cs) for SMS, [InstantEmailServiceClient](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Clients/InstantEmailServiceClient.cs) for email)
5. If the HTTP call fails — throw `PlatformDependencyException` → `500`. Nothing is persisted.
6. Persist the instant order record, a single `NotificationOrder` (status: `Sending`), and the notification to the database
7. Return `201 Created` with tracking information

The key distinction from regular orders: **dispatch happens before persistence, not via a queue**. If the service component is unreachable, the request fails with 500 and leaves no record.

![Instant notifications flow](diagrams/flowchart-instant-notifications-process.svg "Flow chart for instant email and SMS notifications")

## Constraints

Instant notifications intentionally omit several features available to scheduled orders:

| Feature | Instant notifications | Scheduled orders |
|---------|-----------------------|------------------|
| Recipient types | Direct only (phone number / email address) | All types (NIN, org number, external identity, direct) |
| Contact point resolution | Not performed | Performed via Profile and Register APIs |
| Authorization filtering | Not performed | Performed via Authorization API |
| Send conditions | Not supported | Supported via `ConditionEndpoint` |
| Reminders / order chains | Not supported | Supported |
| Send time window | Anytime | Configurable |
| Dispatch mechanism | Synchronous HTTP | ASB queue |
| ResourceId | Always `null` | Optional |

## What gets persisted

A successful instant notification creates exactly three records in the database — no order chain:

- One instant order record (for idempotency tracking)
- One `NotificationOrder` with status `Sending`
- One notification record (`SmsNotification` or `EmailNotification`)

Delivery reports are received asynchronously through the same path as scheduled notifications — via the Link Mobility webhook (SMS) or Azure Event Grid → ASB (email).

## SMS specifics

- Messages up to 160 characters are sent as a single SMS. Longer messages are split into segments of 134 characters (concatenated SMS), capped at 16 segments.
- A `TimeToLive` value can be specified in seconds. The order expires after `RequestedSendTime + TimeToLiveInSeconds`.
- If no sender is specified, the configured default sender number is used.

## Email specifics

- The email expires 48 hours after `RequestedSendTime`.
- If no `FromAddress` is specified, the configured default from-address is used.
- Supports plain text and HTML content types.
