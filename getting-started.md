# Getting Started with Altinn Notifications Monorepo

This guide will help you set up your development environment and get started with the Altinn Notifications monorepo.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [Git](https://git-scm.com/)

## Repository Structure

```
altinn-notifications/
├── components/           # All service components
│   ├── api/             # Main Notifications API
│   ├── email-service/   # Email sending service
│   ├── sms-service/     # SMS sending service
│   └── shared/          # Shared contracts and utilities
├── docs/                # Documentation
├── tools/               # Build scripts and dev setup
└── .github/workflows/   # CI/CD workflows
```

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
dotnet restore components/api/Altinn.Notifications.API.sln
dotnet build components/api/Altinn.Notifications.API.sln

# Build Email Service
dotnet restore components/email-service/Altinn.Notifications.Email.sln
dotnet build components/email-service/Altinn.Notifications.Email.sln

# Build SMS Service
dotnet restore components/sms-service/Altinn.Notifications.Sms.sln
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

### 4. Build Docker Images

```bash
# Build API Docker image
docker build -t notifications-api -f components/api/Dockerfile .

# Build Email Service Docker image
docker build -t notifications-email -f components/email-service/Dockerfile .

# Build SMS Service Docker image
docker build -t notifications-sms -f components/sms-service/Dockerfile .
```

## Development Workflow

### Working on a Single Component

When working on a single component, open the component's solution file directly:

- **API**: `components/api/Altinn.Notifications.API.sln`
- **Email Service**: `components/email-service/Altinn.Notifications.Email.sln`
- **SMS Service**: `components/sms-service/Altinn.Notifications.Sms.sln`

### Local Development Setup

Use the Kafka setup script for local development:

```bash
docker-compose -f tools/dev-setup/setup-kafka.yml up -d
```

### Database Setup

Run the database setup script:

```bash
./tools/dev-setup/dbsetup.sh
```

## Configuration

### Central Package Management

NuGet package versions are centrally managed in `Directory.Packages.props` at the repository root. When adding a new package, define its version there.

### Build Properties

Common MSBuild properties are defined in `Directory.Build.props` at the repository root. This ensures consistent build settings across all components.

## Additional Resources

- [Architecture Documentation](docs/architecture/)
- [API Component README](components/api/README.md)
- [Email Service README](components/email-service/README.md)
- [SMS Service README](components/sms-service/README.md)
