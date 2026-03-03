# Altinn Notifications API

The main API component for Altinn Notifications, providing endpoints for creating and managing notification orders.

## Project Structure

*   **Altinn.Notifications** - API Controllers, Program.cs, and Configuration.
*   **Altinn.Notifications.Core** - Domain logic, services, and interfaces.
*   **Altinn.Notifications.Persistence** - Database access and repositories (PostgreSQL).
*   **Altinn.Notifications.Integrations** - External integrations (Kafka producers, etc.).

## 🏗 Building & Running

**Build:**
```bash
dotnet build Altinn.Notifications.API.sln
```

**Run:**
```bash
cd src/Altinn.Notifications
dotnet run
```

*The API will be available at `http://localhost:5090/`. Swagger: `http://localhost:5090/swagger`.*

**Test:**
```bash
dotnet test Altinn.Notifications.API.sln
```

## 🐳 Containerization

To run the API as a container (from the repo root):

**Podman (Preferred):**
```bash
podman build -t notifications-api -f components/api/Dockerfile .
```

**Docker:**
```bash
docker build -t notifications-api -f components/api/Dockerfile .
```

## 📚 Additional Resources

For full setup instructions including database and Kafka configuration, see [getting-started.md](../../getting-started.md).
