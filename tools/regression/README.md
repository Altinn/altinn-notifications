# Regression Test Suite

End-to-end regression tests for Altinn Notifications, running the full stack in Docker containers with Bruno API tests.

## Quick Start

```bash
bash tools/regression/run-regression.sh                 # Kafka mode (default)
bash tools/regression/run-regression.sh --asynch=asb    # ASB/Wolverine mode
bash tools/regression/run-regression.sh --help
```

This builds and starts the services, runs 146 Bruno API tests, and writes results to `TestResults/<date>_<seq>/`.

### Prerequisites

- Docker Desktop (or Podman with compose plugin)
- Node.js / npx (for Bruno CLI)
- `reportgenerator` dotnet tool (for coverage reports): `dotnet tool install -g dotnet-reportgenerator-globaltool`

### What it does

1. Builds container images for API, Email, SMS, and Mock Services
2. Starts the full stack: PostgreSQL, Kafka, ASB emulator, mock services
3. Waits for API health check
4. Fetches a JWT from the mock token generator
5. Runs the Bruno `regression/` test folder (146 requests, 244 assertions)
6. Stops containers, extracts code coverage, generates report

### Output

```
TestResults/
  2026-04-16_001/
    bruno/results.txt        # Full Bruno CLI output
    code-coverage/
      index.html             # HTML coverage report
      Summary.txt            # Text summary
      *.cobertura.xml        # Raw coverage data
```

Each run gets a sequenced directory (`_001`, `_002`, ...) so results are never overwritten.

## Architecture

```
                        Host (Bruno CLI)
                             |
                        port 5090
                             |
  +------------------------------------------------------------------+
  |  Docker Compose Network                                          |
  |                                                                  |
  |  +-----------+  +-------+  +----------+  +-------------------+  |
  |  | PostgreSQL|  | Kafka |  | Zookeeper|  | ASB Emulator      |  |
  |  | :5432     |  | :29092|  | :2181    |  | (MSSQL + SB emu) |  |
  |  +-----------+  +-------+  +----------+  | :5672 (AMQP)     |  |
  |       |              |                    +-------------------+  |
  |       |              |                                           |
  |  +---------+   +-------------+   +-----------+                  |
  |  |   API   |   | Email Svc   |   |  SMS Svc  |                  |
  |  | :5090   |   | :5190       |   | :5170     |                  |
  |  +---------+   +-------------+   +-----------+                  |
  |       |              |                 |                         |
  |  +-----------------------------------------------------+       |
  |  | Mock Services (WireMock + Token/OIDC + Triggers)     |       |
  |  | Profile:5030  Register:5020  Auth:5050  Cond:5199    |       |
  |  | Token/OIDC:5101  Services:5092                       |       |
  |  +-----------------------------------------------------+       |
  +------------------------------------------------------------------+
```

## Configuration

### Mock providers

Email and SMS mock providers are controlled by dedicated flags (not tied to `ASPNETCORE_ENVIRONMENT`):

```
MockSettings__EnableMockEmailProvider=true
MockSettings__EnableMockSmsProvider=true
```

### Async transport mode

The `--asynch=` flag picks which message bus exercises the async flows:

| Mode | Flag | What starts | Transport |
|---|---|---|---|
| Kafka (default) | `--asynch=kafka` or no flag | 7 containers (no MSSQL, no ASB emulator) | All Wolverine flags `false` — everything flows over Kafka |
| ASB | `--asynch=asb` | 9 containers (adds `mssql` + `servicebus-emulator`) | Wolverine enabled; email send commands and email/SMS delivery reports flow over Azure Service Bus |

The switch sets a single host env var (`ENABLE_WOLVERINE`) which `docker-compose.yml` substitutes into all nine `WolverineSettings__*` flags across the three services. The ASB emulator services are profile-gated (`profiles: ["asb"]`) so Kafka-mode runs skip ~60s of MSSQL + emulator startup.

### Database

PostgreSQL starts with a clean RAM-backed database. The `pg-init/01-create-roles.sql` script creates the `platform_notifications` role needed by migration scripts. Yuniql migrations run automatically on API startup.

## Known Issues / Next Steps

### Wolverine + ASB emulator startup

Enable via `--asynch=asb`. The script starts the emulator stack first and waits for AMQP port 5672 before bringing up the app services, which avoids earlier races where Wolverine's bootstrap in the API tried to connect before the emulator was listening. If ASB mode hangs or fails, inspect `$COMPOSE logs api` and `$COMPOSE logs servicebus-emulator` — the `WolverineOptionsExtensions.ConfigureNotificationsDefaults` shortens `TryTimeout` to 3s in Development so connection issues surface quickly rather than hanging for 60s.

### 14 failing tests (422 Unprocessable Entity)

Tests using `recipientPerson` or `recipientOrganization` with mock data return 422. Likely caused by a validation change in main after the tests were written. The mock WireMock mappings may need updating to match current API expectations.

### Kafka topic race condition (fixed)

Email/SMS `CommonProducer.EnsureTopicsExist()` now catches `TopicAlreadyExists` instead of crashing. This matches the pattern already used in the API's `KafkaProducer`.
