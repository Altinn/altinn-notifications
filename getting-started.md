# Getting Started

This guide will help you set up your development environment and get started with the Altinn Notifications.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Podman](https://podman.io/) (preferred). [Docker](https://www.docker.com/get-started) is supported if you do not use Podman.
- [PostgreSQL](https://www.postgresql.org/download/)
- [pgAdmin](https://www.pgadmin.org/download/)
- [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)
- [Git](https://git-scm.com/)

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

The root `Altinn.Notifications.sln` provides an overview of the repo, while each component has its own solution for day-to-day work.

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/Altinn/altinn-notifications.git
cd altinn-notifications
```

### 2. Build a Component

Each component has its own solution file. To build a specific component:

```bash
# Build API
dotnet build components/api/Altinn.Notifications.API.sln

# Build Email Service
dotnet build components/email-service/Altinn.Notifications.Email.sln

# Build SMS Service
dotnet build components/sms-service/Altinn.Notifications.Sms.sln
```

### 3. Run Tests

```bash
# Test API
dotnet test components/api/Altinn.Notifications.API.sln

# Test Email Service
dotnet test components/email-service/Altinn.Notifications.Email.sln

# Test SMS Service
dotnet test components/sms-service/Altinn.Notifications.Sms.sln
```

### 4. Build Container Images

```bash
# Podman (preferred)
podman build -t notifications-api -f components/api/Dockerfile .
podman build -t notifications-email -f components/email-service/Dockerfile .
podman build -t notifications-sms -f components/sms-service/Dockerfile .

# Docker equivalents
docker build -t notifications-api -f components/api/Dockerfile .
docker build -t notifications-email -f components/email-service/Dockerfile .
docker build -t notifications-sms -f components/sms-service/Dockerfile .
```

## Development Workflow

### Working on a Single Component

When working on a single component, open the component's solution file directly:

- **API**: `components/api/Altinn.Notifications.API.sln`
- **Email Service**: `components/email-service/Altinn.Notifications.Email.sln`
- **SMS Service**: `components/sms-service/Altinn.Notifications.Sms.sln`

### Email Service Component

This component handles the functionality related to sending an email through Altinn Notifications.

Project organization:

- `Altinn.Notifications.Email`: API layer that consumes services provided by `Altinn.Notifications.Email.Core`
  - Relevant implementations: `Program.cs`, Kafka consumer implementation
- `Altinn.Notifications.Email.Core`: domain and application layer
  - Relevant implementations: interfaces for external dependencies, domain models, services for sending emails
- `Altinn.Notifications.Email.Integrations`: infrastructure layer
  - Relevant implementations: client for integrating with the e-mail service, Kafka producer implementation

### SMS Service Component

This component handles the functionality related to sending an SMS through Altinn Notifications.

Project organization:

- `Altinn.Notifications.Sms`: API layer that consumes services provided by `Altinn.Notifications.Sms.Core`
  - Relevant implementations: `Program.cs`
- `Altinn.Notifications.Sms.Core`: domain and application layer
  - Relevant implementations: interfaces for external dependencies, domain models, services for sending SMS
- `Altinn.Notifications.Sms.Integrations`: infrastructure layer
  - Relevant implementations: client for integrating with the SMS service

### Local Development Setup

Use the Kafka setup script for local development (Podman preferred):

```bash
podman compose -f tools/dev-setup/setup-kafka.yml up -d
```

If you are using Docker instead of Podman (Docker Compose):

```bash
docker compose -f tools/dev-setup/setup-kafka.yml up -d
```

Kafdrop is available at `http://localhost:9000`.

### PostgreSQL Setup

Ensure that both PostgreSQL and pgAdmin have been installed and start pgAdmin.

In pgAdmin:

- Create database `notificationsdb`
- Create the following users with password: `Password` (see privileges in parentheses)
  - `platform_notifications_admin` (superuser, canlogin)
  - `platform_notifications` (canlogin)

### Running the Application with .NET

The notifications components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

- Navigate to `components/api/src/Altinn.Notifications`, and build and run the code from there, or run the solution using your selected code editor.

```bash
cd components/api/src/Altinn.Notifications
dotnet run
```

The notifications solution is available locally at `http://localhost:5090/`. For Swagger, use `http://localhost:5090/swagger`.

### Azure Communication Services (Email Service)

If you need end-to-end functionality when working on Notifications Email, Azure Communication Services (ACS) must be set up. Create or use an existing ACS resource and copy the connection string from Azure Portal under **Settings** > **Keys**.

We recommend setting it up as a user secret:

```bash
cd components/email-service/src/Altinn.Notifications.Email
dotnet user-secrets init
dotnet user-secrets set "CommunicationServicesSettings:ConnectionString" "insert-connection-string"
```

### Running the Email Service with .NET

Navigate to `components/email-service/src/Altinn.Notifications.Email` and run the service:

```bash
cd components/email-service/src/Altinn.Notifications.Email
dotnet run
```

### SMS Gateway Credentials (SMS Service)

If you need end-to-end functionality when working on Notifications SMS, configure credentials for Link Mobility's SMS gateway. We recommend setting it up as user secrets:

```bash
cd components/sms-service/src/Altinn.Notifications.Sms
dotnet user-secrets init
dotnet user-secrets set "SmsGatewaySettings:Username" "insert-username"
dotnet user-secrets set "SmsGatewaySettings:Password" "insert-password"
dotnet user-secrets set "SmsDeliveryReportSettings:Username" "insert-username"
dotnet user-secrets set "SmsDeliveryReportSettings:Password" "insert-password"
```

### Running the SMS Service with .NET

Navigate to `components/sms-service/src/Altinn.Notifications.Sms` and run the service:

```bash
cd components/sms-service/src/Altinn.Notifications.Sms
dotnet run
```

### Testing

There is a Bruno collection in `components/api/test/bruno` with examples and testcases for the API.

Before running any tests, remember to prepare an `.env` file. See `components/api/test/bruno/.env.sample` for an example of how to set it up.

### Swagger

The Swagger generated by the application differs from what is published in the documentation and used in APIM. See [transformations](docs/swagger_transforms/transforms.md) for the steps to generate these artifacts.

## Configuration

### .NET SDK Version

The required .NET SDK version is pinned in `global.json` at the repository root. See also `tools/dev-setup/` for local development assets.

## Additional Resources

- [Architecture Documentation](docs/architecture/)
- [API Component README](components/api/README.md)
- [Email Service README](components/email-service/README.md)
- [SMS Service README](components/sms-service/README.md)
