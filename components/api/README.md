# Altinn Notifications API

The main API component for Altinn Notifications, providing endpoints for creating and managing notification orders.

## Project Structure

*   **Altinn.Notifications** - API Controllers, Program.cs, and Configuration.
*   **Altinn.Notifications.Core** - Domain logic, services, and interfaces.
*   **Altinn.Notifications.Persistence** - Database access and repositories (PostgreSQL).
*   **Altinn.Notifications.Integrations** - External integrations (ASB message publishers, Wolverine handlers, etc.).

## 🏗 Building & Running

**Build:**
```bash
dotnet build Altinn.Notifications.API.slnx
```

**Run:**
```bash
cd src/Altinn.Notifications
dotnet run
```

*The API will be available at `http://localhost:5090/`. Swagger: `http://localhost:5090/swagger`.*

**Test:**
```bash
dotnet test Altinn.Notifications.API.slnx
```

## Dynamic worker counts

Each API pod registers a heartbeat in PostgreSQL at startup and once per minute. A pod that has not refreshed for more than two minutes is removed from the count.

The `TaskCount` settings are cluster-wide. Each pod uses `TaskCount / activePodCount`, rounded down, so a group has no workers when its task count is lower than the number of active pods.

To temporarily override a task count, run the following as a database operator after migration `v1.17` has been applied:

```sql
INSERT INTO notifications.config (key, value)
VALUES ('NotificationConfig:PastDueOrdersProcessLoopConfig:TaskCount', '24')
ON CONFLICT (lower(key)) DO UPDATE SET value = EXCLUDED.value;
```

Keys are case-insensitive; only non-negative integer `TaskCount` values are supported. Delete the corresponding row to restore the static setting on the next refresh.

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

For full setup instructions including database and ASB emulator configuration, see [getting-started.md](../../getting-started.md).
