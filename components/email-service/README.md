# Altinn Notifications Email Service

This component handles the functionality related to sending emails through Altinn Notifications.

## Project Structure

### Altinn.Notifications.Email

The host layer responsible for bootstrapping the service and exposing HTTP endpoints.

Relevant implementations:
- Program.cs
- Instant email controller

### Altinn.Notifications.Email.Core

The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure layer
- Domain models
- Services for handling sending of emails

### Altinn.Notifications.Email.Integrations

The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Email.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Client for integrating with email service (ACS)
- ASB message publishers and handlers (Wolverine)

## 🏗 Building & Running

**Build:**
```bash
dotnet build Altinn.Notifications.Email.slnx
```

**Run:**
```bash
cd src/Altinn.Notifications.Email
dotnet run
```

**Test:**
```bash
dotnet test Altinn.Notifications.Email.slnx
```

## 🐳 Containerization

To build the Email Service container (from the repo root):

**Podman (Preferred):**
```bash
podman build -t notifications-email -f components/email-service/Dockerfile .
```

**Docker:**
```bash
docker build -t notifications-email -f components/email-service/Dockerfile .
```

## 📚 Additional Resources

For full setup instructions including database configuration and user secrets, see [getting-started.md](../../getting-started.md).

## Building

```bash
dotnet restore Altinn.Notifications.Email.slnx
dotnet build Altinn.Notifications.Email.slnx
```

## Testing

```bash
dotnet test Altinn.Notifications.Email.slnx
```
