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
└── Altinn.Shared.sln
```

## Usage

Shared libraries are referenced by other components via project references.

## Note

This folder is currently a placeholder for future shared code. As the monorepo evolves, common code shared between API, Email, and SMS services will be extracted here.
