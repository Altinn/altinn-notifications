# Altinn Shared Libraries

This folder contains shared contracts, configuration helpers, and test infrastructure used across multiple Altinn Notifications components.

## Structure

```
components/shared/
├── src/
│   └── Altinn.Notifications.Shared/
│       ├── Commands/       # Cross-service ASB message contracts
│       ├── Configuration/  # Wolverine settings base classes and retry policy
│       └── Extensions/     # Wolverine options extensions
├── test/
│   ├── Altinn.Notifications.Shared.TestInfrastructure/  # Shared test infrastructure (TestContainers, web app factory base)
│   └── Altinn.Notifications.Shared.Tests/               # Unit tests
└── Altinn.Notifications.Shared.slnx
```

## Usage

Shared libraries are referenced by other components via project references.
