# Altinn Shared Libraries

This folder contains shared contracts and utilities used across multiple Altinn Notifications components.

## Structure

```
components/shared/
├── src/
│   ├── Altinn.Shared.Contracts/        # DTOs, interfaces
│   └── Altinn.Shared.Infrastructure/   # Common middleware, utils
├── test/
│   └── Altinn.Shared.Tests/
└── Altinn.Notifications.Shared.slnx
```

## Usage

Shared libraries are referenced by other components via project references.
