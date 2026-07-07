# Perf Snapshot

Small console tool that polls the notifications database on a fixed interval during a performance test run, and appends the results to a CSV file — so you don't have to sit and manually copy-paste query results while a test is running.

Each tick records:

- `notifications.orders` — count grouped by `type` and `processedstatus`, for a given `sendersreference`
- `notifications.emailnotifications` — count grouped by `result`, joined to the same `sendersreference`

## Prerequisites

- .NET 10 SDK
- Access to the notifications PostgreSQL database

## Configuration

`appsettings.json` ships with defaults for local development (localhost PostgreSQL). To target another environment, override the connection string via [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — never committed to source control:

```bash
cd tools/src/Altinn.Notifications.Tools.PerfSnapshot
dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "Host=<host>;Port=5432;Username=<user>;Password=<pwd>;Database=notificationsdb;"
```

The sendersReference, poll interval, output path, and an optional duration bound can be set in `appsettings.json` under `PerfSnapshotSettings`, or passed as command-line flags (these take precedence):

```bash
dotnet run -- --sendersRef k6-order-980ff11f --interval 60 --output perf-snapshot.csv --duration 90
```

- `--sendersRef` (required, or set via config) — the shared `sendersReference` for the test run's orders.
- `--interval` — polling interval in seconds (default 60). If a query takes longer than this (see note below), the tool moves on immediately rather than also waiting a full interval afterwards.
- `--output` — CSV output path (default `perf-snapshot.csv`, relative to the working directory).
- `--duration` — optional safety bound in minutes; omit to run until you stop it with Ctrl+C.

> **Note:** these queries scan `notifications.orders`/`notifications.emailnotifications` without a covering index on `sendersreference`, so they get noticeably slower — observed up to 1-2 minutes — once a test has grown the tables to hundreds of thousands of rows. `PerfSnapshotSettings:CommandTimeoutSeconds` (default 300s / 5 minutes) is set well above that so a slow query is never mistaken for a failure. Raise it further if you're running against an even larger dataset.

## Running

```bash
cd tools/src/Altinn.Notifications.Tools.PerfSnapshot
dotnet run -- --sendersRef k6-order-980ff11f
```

Prints a compact summary line to the console on every tick, and appends the same data as rows to the CSV. Stop anytime with Ctrl+C — the current in-flight snapshot finishes before it exits, and the CSV is flushed after every write, so no data is lost even if the process is killed abruptly (e.g. during a pod-restart test).

## CSV format

Long/tidy format — one row per `(source, category)` combination per tick, so it loads directly into a `pandas` DataFrame with `pivot_table` if you want a wide view later:

```
timestamp_utc,elapsed_seconds,source,category,count
2026-07-08T09:15:00.1234567Z,0,order,Notification:Registered,500000
2026-07-08T09:15:00.1234567Z,0,email,New,0
2026-07-08T09:16:00.2345678Z,60,order,Notification:Processing,1200
2026-07-08T09:16:00.2345678Z,60,order,Notification:Processed,35100
2026-07-08T09:16:00.2345678Z,60,email,New,35100
```

`source` is either `order` (from `notifications.orders`) or `email` (from `notifications.emailnotifications`). For orders, `category` combines type and status as `"{type}:{processedstatus}"` (e.g. `Notification:Processing`) since a test run may include both `Notification` and `Reminder` order types.
