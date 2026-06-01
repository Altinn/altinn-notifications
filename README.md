# Altinn Notifications

[![.NET Analysis](https://github.com/Altinn/altinn-notifications/actions/workflows/build-and-analyze.yml/badge.svg)](https://github.com/Altinn/altinn-notifications/actions/workflows/build-and-analyze.yml)
[![Notifications scan](https://github.com/altinn/altinn-notifications/actions/workflows/container-scan.yml/badge.svg)](https://github.com/Altinn/altinn-notifications/actions/workflows/container-scan.yml)
[![CodeQL](https://github.com/Altinn/altinn-notifications/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/Altinn/altinn-notifications/actions/workflows/codeql-analysis.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

Altinn Notifications is a set of microservices designed to manage and deliver notification messages, including email and SMS.

## Components

| Component | Description | README |
| :--- | :--- | :--- |
| **API** | The primary Notifications API for registering and tracking notification orders | [components/api/](components/api/README.md) |
| **Email Service** | Handles the processing and delivery of email notifications | [components/email-service/](components/email-service/README.md) |
| **SMS Service** | Handles the processing and delivery of SMS notifications | [components/sms-service/](components/sms-service/README.md) |
| **Shared** | Shared ASB message contracts, Wolverine configuration, and test infrastructure | [components/shared/](components/shared/README.md) |

## Repository Structure

```
altinn-notifications/
├── Altinn.Notifications.slnx
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

## Runtime Highlights

- Azure Service Bus is used across components for messaging, via the Wolverine framework.
- API uses PostgreSQL and supports Azure Key Vault for secrets.
- Email and SMS services support Azure Key Vault for secrets and can emit telemetry to Application Insights via OpenTelemetry exporters.

## Contributing

Contributions are welcome! Please read the [Altinn contributing guidelines](https://github.com/Altinn/altinn-studio/blob/main/CONTRIBUTING.md) before submitting a pull request.

For local development setup, see [getting-started.md](getting-started.md).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
