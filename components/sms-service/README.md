# Altinn Notifications SMS Service

This component handles the functionality related to sending SMS through Altinn Notifications.

## Project Structure

### Altinn.Notifications.Sms

The host layer responsible for bootstrapping the service and exposing HTTP endpoints.

Relevant implementations:
- Program.cs
- Delivery report controller
- Instant message controller

### Altinn.Notifications.Sms.Core

The domain and application layer that implements the business logic of the system.

Relevant implementations:
- Interfaces for external dependencies implemented by infrastructure layer
- Domain models
- Services for handling sending of SMS

### Altinn.Notifications.Sms.Integrations

The infrastructure layer that implements the interfaces defined in _Altinn.Notifications.Sms.Core_ for integrations towards 3rd-party libraries and systems.

Relevant implementations:
- Client for external SMS delivery service (Link Mobility)
- ASB message publishers and handlers (Wolverine)

## 🏗 Building & Running

**Build:**
```bash
dotnet build Altinn.Notifications.Sms.slnx
```

**Run:**
```bash
cd src/Altinn.Notifications.Sms
dotnet run
```

**Test:**
```bash
dotnet test Altinn.Notifications.Sms.slnx
```

## 🐳 Containerization

To build the SMS Service container (from the repo root):

**Podman (Preferred):**
```bash
podman build -t notifications-sms -f components/sms-service/Dockerfile .
```

**Docker:**
```bash
docker build -t notifications-sms -f components/sms-service/Dockerfile .
```

## 📚 Additional Resources

For full setup instructions including database configuration and user secrets, see [getting-started.md](../../getting-started.md).
