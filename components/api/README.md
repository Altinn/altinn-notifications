# Altinn Notifications API

The main API component for Altinn Notifications, providing endpoints for creating and managing notification orders.

## Structure

```
components/api/
├── src/
│   ├── Altinn.Notifications/              # Controllers, Program.cs
│   ├── Altinn.Notifications.Core/         # Business logic, services
│   ├── Altinn.Notifications.Persistence/  # Data access
│   ├── Altinn.Notifications.Integrations/ # External systems (Kafka, etc.)
│   └── DbTools/                           # Database migrations/tools
├── test/
│   ├── Altinn.Notifications.Tests/        # Unit tests
│   ├── Altinn.Notifications.IntegrationTests/
│   └── Altinn.Notifications.Tools.Tests/
├── Altinn.Notifications.API.sln
└── Dockerfile
```

## Building

```bash
dotnet restore Altinn.Notifications.API.sln
dotnet build Altinn.Notifications.API.sln
```

## Testing

```bash
dotnet test Altinn.Notifications.API.sln
```

## Running Locally

```bash
dotnet run --project src/Altinn.Notifications/Altinn.Notifications.csproj
```

## Docker

```bash
docker build -t notifications-api -f Dockerfile ../..
```
