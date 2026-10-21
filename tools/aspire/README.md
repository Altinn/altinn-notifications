# Aspire app host

Runs the whole Notifications stack locally from one command, with logs, traces and metrics from all services in the Aspire dashboard.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Aspire CLI](https://aspire.dev/get-started/install-cli/)
* [Podman](https://podman.io/)
* A Podman container named `notifications-db` with the database and roles from [Database Setup](../../getting-started.md#3-database-setup-postgresql), or a local PostgreSQL (see [Settings](#settings)). For example:

  ```bash
  podman run -d --name notifications-db -p 5432:5432 \
    -e POSTGRES_USER=platform_notifications_admin \
    -e POSTGRES_PASSWORD=Password \
    -e POSTGRES_DB=notificationsdb \
    -v notifications-db-data:/var/lib/postgresql \
    docker.io/library/postgres:18-alpine

  podman exec notifications-db psql -U platform_notifications_admin -d notificationsdb -c "CREATE ROLE platform_notifications WITH LOGIN PASSWORD 'Password';"
  ```

* `kubectl`, logged in to a cluster, only if you want to use [Use cluster](#use-cluster--use-local).

## Run

```bash
cd tools/aspire
aspire run
```

## Settings

Override `appsettings.json` in a git-ignored `appsettings.Local.json`. Set `Postgres:UsePodman` to `false` to use a local PostgreSQL on port 5432 instead of the `notifications-db` container.

Before the dashboard starts, the app host builds the API, Email and SMS projects one by one. Starting them all at once makes their builds of the shared project collide.

## What it starts

| Resource | What it is | Port |
| :--- | :--- | :--- |
| `api` | `components/api/src/Altinn.Notifications` | 5090 |
| `email` | `components/email-service/src/Altinn.Notifications.Email` | 5091 |
| `sms` | `components/sms-service/src/Altinn.Notifications.Sms` | 5092 |
| `postgres` | Starts the `notifications-db` container and shows its logs | 5432 |
| `servicebus` | Starts the Service Bus emulator from `tools/asb-emulator` and shows its logs | 5672 |

* The services run with `ASPNETCORE_ENVIRONMENT=Development` on fixed ports, so the URLs in `appsettings*.json` keep working.
* Each service has a **Swagger** link in the URLs column.
* The services wait until `postgres` is running and the emulator reports healthy.
* The containers live in Podman, not in Aspire. Stopping Aspire stops the services, but `notifications-db` and the emulator keep running.

## Dashboard actions

### Run tests

Each service has a **Run tests** action. It runs `dotnet test` on that component's solution, e.g. `components/api/Altinn.Notifications.API.slnx`.

* The test resource is hidden until its first run. After that it shows up under the service with its own log.
* The state shows **Passed** (green) or **Failed** (red) based on the exit code.
* The API's integration tests use the same `notificationsdb` as the running `api`. Stop `api` before running `api-tests`, or the API's background jobs can change test data while the tests run.
* The `*IntegrationTestsASB` projects use Testcontainers, which needs access to Podman (for example `DOCKER_HOST` pointing to the Podman socket).

### Change alert

When a `.cs`, `.csproj` or `.json` file changes under a service's `src` folder, the dashboard shows a yellow notification with a **Restart** button. A change under `components/shared/src` alerts for all three services.

`.sql` files are ignored, because the API build regenerates one on every run. Restart `api` yourself after changing migration scripts.

### Use cluster / Use local

**Use cluster** stops the local service and runs `kubectl port-forward` against the same port instead, so `localhost:<port>` reaches the service in your current kube context. The state changes to **Cluster** and the kubectl output goes to the service's log. **Use local** stops the forward and starts the local service again.

| Resource | Cluster service |
| :--- | :--- |
| `api` | `service/altinn-notifications` (5090 → 80) |
| `email` | `service/altinn-notifications-email` (5091 → 80) |
| `sms` | `service/altinn-notifications-sms` (5092 → 80) |

* It uses whatever `kubectl config current-context` and namespace you are on.
* A port-forward only carries HTTP. Service Bus messages still go to the local emulator, so a forwarded service does not see messages from your local services, and the other way round.
* Use **Use local** to switch back. The normal **Start** or **Restart** fails while the forward holds the port.

## Troubleshooting

* **`MSB4166: Child node exited prematurely` when a service starts**: an MSBuild worker crashed. Restart the service.
* **`no container with name or ID "notifications-db"`**: create the container as shown in [Prerequisites](#prerequisites).
* **Services start before the emulator is ready**: they wait for `servicebus` to be healthy. If it never becomes healthy, check its log and that nothing else is using ports 5672 or 5300.
