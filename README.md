# Altinn Notifications

Altinn Notifications is a set of microservices designed to manage and deliver notification messages, including email and SMS. This monorepo contains the following components:

*   **API** – The primary Notifications API used to register and track notification orders
*   **Email Service** – Handles the processing and delivery of email notifications
*   **SMS Service** – Handles the processing and delivery of SMS notifications

## Repository Structure

```
altinn-notifications/
├── Altinn.Notifications.sln
├── components/           # All components
│   ├── api/              # Main Notifications API
│   ├── email-service/    # Email sending service
│   ├── sms-service/      # SMS sending service
│   └── shared/           # Shared utilities
├── docs/                 # Documentation
├── tools/                # Build scripts
└── .github/workflows/    # CI/CD workflows
```

## Documentation

- [getting-started.md](getting-started.md) – setup instructions and development workflow
- Architecture: [English](docs/architecture/_index.en.md) | [Norwegian](docs/architecture/_index.nb.md) – high-level design, domain models, and integrations

For complete instructions—including PostgreSQL configuration, test collections, and service‑specific setup—refer to [getting-started.md](getting-started.md).

## Runtime Highlights

- Kafka is used across components for messaging (producers/consumers).
- API uses PostgreSQL and supports Azure Key Vault for secrets.
- Email and SMS services support Azure Key Vault for secrets and can emit telemetry to Application Insights via OpenTelemetry exporters.

***

## API Component

The API component is built on clean architecture principles and structured into the following layers:

### Altinn.Notifications

The API layer responsible for exposing endpoints and consuming services from **Altinn.Notifications.Core**.

Key elements:

*   Controllers
*   Program.cs

### Altinn.Notifications.Core

The domain and application layer that defines and executes the business logic.

Key elements:

*   Interfaces for external dependencies (implemented by infrastructure and persistence layers)
*   Domain models
*   Core services

### Altinn.Notifications.Integrations

The infrastructure layer implementing integration-specific interfaces from **Altinn.Notifications.Core**.

Key elements:

*   Kafka producer and consumer implementations
*   Clients for communication with Altinn Platform

### Altinn.Notifications.Persistence

The persistence layer responsible for repository logic and data storage operations.

***

## Email Service Component

The email service follows the same clean architecture guidelines and consists of:

### Altinn.Notifications.Email

The API layer that consumes services from **Altinn.Notifications.Email.Core**.

Key elements:

*   Program.cs
*   Kafka consumer implementation

### Altinn.Notifications.Email.Core

The domain and application layer for email‑specific business logic.

Key elements:

*   Interfaces for infrastructure dependencies
*   Domain models
*   Email delivery services

### Altinn.Notifications.Email.Integrations

The infrastructure layer implementing email‑related integrations defined in the core layer.

Key elements:

*   Email service client
*   Kafka producer implementation

***

## SMS Service Component

The SMS service also adheres to clean architecture and includes:

### Altinn.Notifications.Sms

The API layer consuming services from **Altinn.Notifications.Sms.Core**.

Key elements:

*   Program.cs

### Altinn.Notifications.Sms.Core

The domain and application layer for SMS‑specific business logic.

Key elements:

*   Interfaces for external dependencies
*   Domain models
*   SMS delivery services

### Altinn.Notifications.Sms.Integrations

The infrastructure layer implementing SMS service integrations.

Key elements:

*   Client for external SMS delivery service