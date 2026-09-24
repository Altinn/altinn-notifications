# Azure Service Bus

Azure Service Bus (ASB) is used as the message broker across the components that make up the Notifications solution.
Messaging is managed through the [Wolverine](https://wolverine.netlify.app/) framework, which handles declarative queue registration, handler wiring, and retry policy configuration across all three service components (API, email-service, sms-service).

Queues are provisioned automatically in production via Wolverine's `AutoProvision` capability.
In development environments, the [Azure Service Bus Emulator](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator) is used and queues are purged on startup (`AutoPurgeOnStartup`).

## Retry policy

Each queue has a configurable `QueueRetryPolicy` defining the retry strategy for transient failures.
The general pattern is:

1. **Immediate retry** — first attempt fails, Wolverine retries after a brief cooldown.
2. **Scheduled retry** — subsequent failures are re-enqueued with an increasing delay (scheduled delivery).
3. **Dead-letter queue** — messages that exhaust all retries are moved to the queue's dead-letter sub-queue for inspection.

The composed email send queue additionally treats `AttachmentDownloadException` (a transient SAS-URL download failure) as retryable under this same policy.

## Queue overview

### Emails

#### Email send queue

**Description:** Email send commands published by the API and consumed by the email service.

**Publisher:** Altinn Notifications API, [EmailCommandPublisher](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/Publishers/EmailCommandPublisher.cs)

**Handler:** Altinn Notifications Email, [SendEmailCommandHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/SendEmailCommandHandler.cs)

**Content:**
- Format: json
- Data structure: [Email](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Core/Models/Email.cs)
- Description: An email with all required properties present.

---

#### Composed email send queue

**Description:** Composed email send commands — subject, body, and recipient already fully composed by the caller, with optional file attachments — published by the API and consumed by the email service.

**Publisher:** Altinn Notifications API, [ComposedEmailCommandPublisher](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/Publishers/ComposedEmailCommandPublisher.cs)

**Handler:** Altinn Notifications Email, [SendComposedEmailCommandHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/SendComposedEmailCommandHandler.cs)

**Content:**
- Format: json
- Data structure: [SendComposedEmailCommand](https://github.com/Altinn/altinn-notifications/blob/main/components/shared/src/Altinn.Notifications.Shared/Commands/SendComposedEmailCommand.cs)
- Description: A fully-composed email with pre-rendered content and file attachments referenced by SAS URL.

**Notes:** Downstream status uses the *existing* email status check and email send result queues below — there is no dedicated status/result queue for composed email. Attachments are downloaded from their SAS URL at send time; a transient download failure (`AttachmentDownloadException`) is retried like any other infrastructure exception, while an invalid or expired SAS URL (`InvalidSasUrlException`) fails the notification immediately without a retry.

---

#### Email status check queue

**Description:** Commands that trigger polling of Azure Communication Services for the current status of an in-flight email send operation. The handler self-publishes back to this queue to continue polling until a terminal status is reached.

**Publisher:**
- Initial enqueue: Altinn Notifications Email, [SendEmailCommandHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/SendEmailCommandHandler.cs)
- Re-enqueue while polling: Altinn Notifications Email, [CheckEmailSendStatusHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/CheckEmailSendStatusHandler.cs)

**Handler:** Altinn Notifications Email, [CheckEmailSendStatusHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Integrations/Wolverine/Handlers/CheckEmailSendStatusHandler.cs)

**Content:**
- Format: json
- Data structure: [SendNotificationOperationIdentifier](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Core/SendNotificationOperationIdentifier.cs)
- Description: Groups notification id, ACS operation id, and timestamp of the last status check.

---

#### Email send result queue

**Description:** Results of email send operations published by the email service back to the API.

**Publisher:** Altinn Notifications Email, StatusService

**Handler:** Altinn Notifications API, [EmailSendResultHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/EmailSendResultHandler.cs)

**Content:**
- Format: json
- Data structure: [SendOperationResult](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Core/Status/SendOperationResult.cs)
- Description: Contains the [EmailSendResult](https://github.com/Altinn/altinn-notifications/blob/main/components/email-service/src/Altinn.Notifications.Email.Core/Status/EmailSendResult.cs) for a given notification and the ACS operation id.

---

#### Email delivery report queue

**Description:** ACS email delivery reports routed directly from Azure Event Grid to this ASB queue. The API consumes and processes the report to update notification status.

**Publisher:** Azure Event Grid (routes ACS delivery report events directly to ASB)

**Handler:** Altinn Notifications API, [EmailDeliveryReportHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/Handlers/EmailDeliveryReportHandler.cs)

**Content:**
- Format: json (Event Grid envelope decoded via [EventGridEnvelopeMapper](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/EventGridEnvelopeMapper.cs))
- Description: ACS delivery report for a sent email notification.

**Notes:** Azure Event Grid is configured to route ACS email delivery report events directly to this ASB queue, which the API consumes via Wolverine.

---

#### Email service rate limit queue

**Description:** Service availability updates published by the email service when Azure Communication Services rate limits are encountered.

**Publisher:** Altinn Notifications Email, SendingService

**Handler:** Altinn Notifications API, [EmailServiceRateLimitHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/Handlers/EmailServiceRateLimitHandler.cs)

**Content:**
- Format: json
- Data structure: [GenericServiceUpdate](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Core/Models/AltinnServiceUpdate/GenericServiceUpdate.cs)
- Description: An Altinn service update describing the rate limit state change.

---

### SMS

#### SMS send queue

**Description:** SMS send commands published by the API and consumed by the SMS service.

**Publisher:** Altinn Notifications API, [SendSmsCommandPublisher](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/Publishers/SendSmsCommandPublisher.cs)

**Handler:** Altinn Notifications SMS, [SendSmsCommandHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms.Integrations/Wolverine/Handlers/SendSmsCommandHandler.cs)

**Content:**
- Format: json
- Data structure: [Sms](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms.Core/Sending/Sms.cs)
- Description: An SMS with all required properties present.

---

#### SMS send result queue

**Description:** Results of SMS send operations published by the SMS service back to the API.

**Publisher:** Altinn Notifications SMS, StatusService

**Handler:** Altinn Notifications API, [SmsSendResultHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/SmsSendResultHandler.cs)

**Content:**
- Format: json
- Data structure: [SendOperationResult](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms.Core/Status/SendOperationResult.cs)
- Description: Contains the [SmsSendResult](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms.Core/Status/SmsSendResult.cs) and gateway reference to Link Mobility.

---

#### SMS delivery report queue

**Description:** SMS delivery reports from Link Mobility received by the SMS service and forwarded to the API via ASB.

**Publisher:** Altinn Notifications SMS, StatusService (after receiving delivery report via the [DeliveryReportController](https://github.com/Altinn/altinn-notifications/blob/main/components/sms-service/src/Altinn.Notifications.Sms/Controllers/DeliveryReportController.cs) webhook endpoint)

**Handler:** Altinn Notifications API, [SmsDeliveryReportHandler](https://github.com/Altinn/altinn-notifications/blob/main/components/api/src/Altinn.Notifications.Integrations/Wolverine/Handlers/SmsDeliveryReportHandler.cs)

**Content:**
- Format: json
- Description: Delivery report from Link Mobility containing the final delivery status.

---

## Development setup

An Azure Service Bus Emulator is available for local development and integration testing.
A [Docker Compose file](https://github.com/Altinn/altinn-notifications/blob/main/tools/asb-emulator/docker-compose.yaml) is provided to start the emulator locally.

Refer to [Getting Started](https://github.com/Altinn/altinn-notifications/blob/main/getting-started.md) for instructions on running the services locally.
