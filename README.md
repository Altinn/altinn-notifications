# Altinn Notifications

Altinn platform microservices for handling notifications (email, SMS, etc). This monorepo contains:

- **API** - Main Notifications API for registering and managing notification orders
- **Email Service** - Service for sending email notifications
- **SMS Service** - Service for sending SMS notifications

## Repository Structure

```
altinn-notifications/
├── components/
│   ├── api/                    # Main Notifications API
│   ├── email-service/          # Email sending service
│   ├── sms-service/            # SMS sending service
│   └── shared/                 # Shared contracts and utilities
├── docs/                       # Documentation
├── tools/                      # Build scripts and dev setup
└── .github/workflows/          # CI/CD workflows
```

For detailed setup instructions, see [getting-started.md](getting-started.md).

## Architecture
Detailed architecture documentation can be found in the [docs/architecture](docs/architecture) folder.

## API Component
The API component follows clean architecture principles and is organized into:

### Altinn.Notifications
The API layer that consumes services provided by _Altinn.Notifications.Core_

Relevant implementations:
- Controllers
- Program.cs

### Altinn.Notifications.Core
The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure and repository layer
- Domain models
- Services

### Altinn.Notifications.Integrations
The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Kafka producer and consumer implementation
- Clients for communicating with Altinn Platform components

### Altinn.Notifications.Persistence
The persistence layer that implements repository logic.

## Getting started

See [getting-started.md](getting-started.md) for detailed setup instructions.

