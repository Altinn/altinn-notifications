# Aspire app host

Runs the whole Notifications stack locally from one command, with logs, traces and metrics from all services in the Aspire dashboard.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Aspire CLI](https://aspire.dev/get-started/install-cli/)
* [Podman](https://podman.io/) or Docker (see [Settings](#settings))
* A container named `notifications-db` with the database and roles from [Database Setup](../../getting-started.md#3-database-setup-postgresql), or a local PostgreSQL (see [Settings](#settings)). For example (replace `podman` with `docker` if you use Docker):

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

The dashboard opens on `https://localhost:17193` and needs a trusted .NET development certificate (`aspire certs trust`). To skip the certificate, run it over plain HTTP with the `http` profile from `apphost.run.json`:

```bash
aspire run -lp http
```

The dashboard is then on `http://localhost:15193` with no login token. The services already use plain HTTP, so only the dashboard changes. It shows an "Endpoint is unsecured" banner, which is expected: anything on the machine can send telemetry to it or read it, so use the profile on your own machine only.

## Settings

Override `appsettings.json` in a git-ignored `appsettings.Local.json`.

| Setting | Default | Description |
| :--- | :--- | :--- |
| `ContainerRuntime` | `podman` | `podman` or `docker`, used to start the containers and tail their logs |
| `Postgres:UseContainer` | `true` | `false` uses a local PostgreSQL instead of starting a container |
| `Postgres:ContainerName` | `notifications-db` | The Postgres container to start |
| `Postgres:Port` | `5432` | Points the API at another port, e.g. to keep a separate database next to the one you use for development |
| `ServiceBus:HealthPort` | `5300` | Host port for the emulator's health endpoint. Change it if 5300 is taken, e.g. by Altinn localtest. The services only use 5672. |
| `Auth:Mode` | `mock` | `mock` or `at23`, see [Authentication](#authentication) |
| `Auth:Port` | `5094` | Port of the `mock-auth` resource |
| `Acs:Mode` | `mock` | `mock` or `real`, see [Email through the ACS mock](#email-through-the-acs-mock) |
| `Acs:Port` | `5095` | Port of the `mock-acs` resource |
| `Mocks:Enabled` | `true` | `false` turns all [platform mocks](#platform-mocks) off |
| `Mocks:<System>:Enabled` | `true` | Turns one mock off: `Profile`, `Register`, `Authorization`, `Conditions` or `Maskinporten` |
| `Mocks:<System>:Port` | `5131`, `5121`, `5151`, `5199`, `5197` | Port of each mock |

For example, with Docker, a separate database on 5442 and localtest running:

```json
{
  "ContainerRuntime": "docker",
  "Postgres": {
    "ContainerName": "notifications-aspire-db",
    "Port": 5442
  },
  "ServiceBus": {
    "HealthPort": 5301
  }
}
```

Before the dashboard starts, the app host builds the API, Email and SMS projects one by one. Starting them all at once makes their builds of the shared project collide.

## What it starts

| Resource | What it is | Port |
| :--- | :--- | :--- |
| `api` | `components/api/src/Altinn.Notifications` | 5090 |
| `email` | `components/email-service/src/Altinn.Notifications.Email` | 5091 |
| `sms` | `components/sms-service/src/Altinn.Notifications.Sms` | 5092 |
| `mock-auth` | The [mock token issuer](../src/Altinn.Notifications.Tools.MockAuth/README.md) the API trusts. Not started when `Auth:Mode` is `at23`. | 5094 |
| `mock-acs` | The [ACS mock](../src/Altinn.Notifications.Tools.AcsMock/README.md) that `email` sends to instead of Azure Communication Services. Not started when `Acs:Mode` is `real`. | 5095 |
| `wiremock-platform` | The [platform mocks](#platform-mocks) for Profile, Register, Authorization, Maskinporten and condition endpoints. Not started when `Mocks:Enabled` is `false`. | 5131, 5121, 5151, 5199, 5197 |
| `postgres` | Starts the `notifications-db` container and shows its logs | 5432 |
| `servicebus` | Starts the Service Bus emulator from `tools/asb-emulator` and shows its logs | 5672 |

* The services run with `ASPNETCORE_ENVIRONMENT=Development` on fixed ports, so the URLs in `appsettings*.json` keep working.
* Each service has a **Swagger** link in the URLs column.
* The services wait until `postgres` is running and the emulator reports healthy.
* The containers live in Podman or Docker, not in Aspire. Stopping Aspire stops the services, but `notifications-db` and the emulator keep running.

## Authentication

By default (`Auth:Mode` = `mock`) the API trusts tokens from the local `mock-auth` resource instead of at23, so you need no at23 account or token generator credentials. To get a token in Bruno, select the `v2 aspire` environment and run `test-util/new auth token`. The other requests then use it. Or fetch one yourself:

```bash
curl -u mock:mock "http://localhost:5094/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create"
```

Set `Auth:Mode` to `at23` to keep the API's own settings and use tokens from the Altinn test token generator, e.g. with the `v2 local` environment in Bruno.

## Email through the ACS mock

By default (`Acs:Mode` = `mock`) `email` sends to the local `mock-acs` resource instead of Azure Communication Services, so no ACS connection string is needed and nothing leaves the machine. The app host gives `email` a connection string with a test access key, and the mock rejects requests that are not signed with it.

The mock answers the send and the status polls, and then puts a delivery report on the emulator's `altinn.notifications.email.deliveryreports` queue, so an email ends as `Email_Delivered` like in Azure. The recipient address picks other outcomes, e.g. `bounce@example.com` ends as `Email_Failed_Bounced`. See the [scenarios](../src/Altinn.Notifications.Tools.AcsMock/README.md#scenarios).

To try it, fetch a token and send an instant email:

```bash
TOKEN=$(curl -s -u mock:mock "http://localhost:5094/api/GetEnterpriseToken?org=ttd&scopes=altinn:serviceowner/notifications.create")
curl -X POST http://localhost:5090/notifications/api/v1/future/orders/instant/email   -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json"   -d '{"idempotencyId":"acs-1","sendersReference":"acs-test","recipientEmail":{"emailAddress":"ola.nordmann@example.com","emailSettings":{"subject":"Test","body":"Test","contentType":"Plain","senderEmailAddress":"noreply@altinn.no"}}}'
```

The `mock-acs` log shows each request and response with recipient addresses, subjects and bodies masked, and each delivery report it sends.

Set `Acs:Mode` to `real` to keep the email service's own `CommunicationServicesSettings:ConnectionString`, e.g. from its user secrets. **Use cluster** on `email` bypasses the mock, since the cluster's email service uses its own ACS.

With `Simulator:Enabled`, requests the mock does not recognise go to the simulator on port 5096 instead of getting a 404.

## Platform mocks

The API looks up contact points in Profile, names in Register and access in Authorization, and calls the condition endpoint of an order before sending, with a token from Maskinporten. By default the `wiremock-platform` resource answers all of these locally, so orders to persons and organisations, with conditions and reminders, are processed without at23. See the [tool's README](../src/Altinn.Notifications.Tools.PlatformMocks/README.md) for what each mock answers.

| System | Port | What the app host sets on the API |
| :--- | :--- | :--- |
| Profile | 5131 | `PlatformSettings:ApiProfileEndpoint` |
| Register | 5121 | `PlatformSettings:ApiRegisterEndpoint` |
| Authorization | 5151 | `PlatformSettings:ApiAuthorizationEndpoint` |
| Conditions | 5199 | Nothing. Orders point to the condition URLs themselves, which are listed in the URLs column. They use `local.altinn.cloud`, which resolves to 127.0.0.1, since the API does not accept `localhost` in condition URLs. |
| Maskinporten | 5197 | `SendConditionClient:MaskinportenSettings:TokenEndpoint`, `AssertionAudience`, `ClientId`, and an `EncodedJwk` made for each run |

* Any national identity number or organisation number gets contact points, so k6 can use random ones.
* A system that is turned off keeps the endpoint from the API's own `appsettings.json`, for example `localhost:5030` for a local altinn-profile.
* The Maskinporten settings need `Altinn.ApiClients.Maskinporten` with configurable endpoints ([Altinn/altinn-apiclient-maskinporten#92](https://github.com/Altinn/altinn-apiclient-maskinporten/pull/92)). Until the API uses that version, the condition client still goes to test.maskinporten.no, gets no token, and the API treats every condition check as failed: it retries the order once and then sends it.
* With `Simulator:Enabled`, requests a mock does not recognise go to the simulator on 5096 instead of a 404.

To run Bruno and k6 against the mocks, see [Bruno and k6](#bruno-and-k6).

The services do not send by themselves locally, since cron jobs call the trigger endpoints in the cluster. Call `POST /notifications/api/v1/trigger/sendsmsanytime`, `sendsms` or `sendemail` on the API to send what is waiting.

## Bruno and k6

Both run against the stack with the default `Auth:Mode` = `mock` (the `mock-auth` resource) and the platform mocks (`wiremock-platform`) on. No at23 account, token generator credentials or secrets are needed.

### Bruno

`components/api/test/bruno/aspire.env` has test data that matches the platform mocks.

* **Bruno app** (`Bruno.exe`): copy `aspire.env` to `.env` in the collection folder, open the collection, select the `v2 aspire` environment and run `test-util/new auth token`. The other requests then use the token.
* **Bruno CLI in Docker**: mount the collection read-only, or the CLI writes the token into `environments/v2 aspire.bru`, and point it at the host:

  ```bash
  docker run --rm -v "<repo>/components/api/test/bruno:/bruno:ro" --env-file "<repo>/components/api/test/bruno/aspire.env"     usebruno/cli:latest run "test-util/new-auth-token.bru" "instant-demo" "v2 (future)" -r     --env "v2 aspire" --env-var host=http://host.docker.internal:5090 --env-var token_generator_url=http://host.docker.internal:5094
  ```

  The CLI warns that it cannot save the token to the read-only environment file. That is expected: it keeps the token for the rest of the run.

The condition URLs in `aspire.env` stay on `local.altinn.cloud` also when Bruno runs in Docker, since the API calls them.

Instant SMS and email, future orders to phone numbers, email addresses, persons and organisations, orders with conditions and reminders, and the shipment and status feed lookups all pass. These do not:

* `composed-email`: `new auth token` only asks for `altinn:serviceowner/notifications.create`, not the `notifications.composedemail.create` scope, so they get `403` in every environment. The happy paths also need SAS URLs to real blobs.
* `notification-log`: need `dialogporten_dialog_id` and `dialogporten_transmission_id`, which no environment sets.
* `validation-demo-contract`: its `ValidationError` test compares the JSON body with a string, so it fails in every environment.

### k6

Run the tests with `-e altinn_env=local`, see [Running against the local Aspire stack](../../components/api/test/k6/ReadMe.md#running-against-the-local-aspire-stack) in the k6 README.

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
