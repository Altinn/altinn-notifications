# DLQ Manager

Interactive console tool for inspecting and remediating messages stuck on Azure Service Bus Dead Letter Queues (DLQs) in the Altinn Notifications system.

## Prerequisites

- .NET 10 SDK
- Access to the Azure Service Bus namespace (connection string)
- Access to the notifications PostgreSQL database

## Configuration

`appsettings.json` ships with defaults for local development (ASB emulator + localhost PostgreSQL). To target any other environment, override the two connection strings via [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — these are never committed to source control.

```bash
cd tools/src/Altinn.Notifications.Tools.DlqManager
dotnet user-secrets set "AsbSettings:ConnectionString" "<your-asb-connection-string>"
dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "Host=<host>;Port=5432;Username=<user>;Password=<pwd>;Database=notificationsdb;Ssl Mode=Require;"
```

Non-secret defaults (queue names, output file paths) live in `appsettings.json`.

## Running

```bash
dotnet run
```

The tool presents an interactive menu. Select a queue (1–9), then choose an operation from the queue-specific sub-menu.

## SMS send queue workflow

The typical operator workflow for `altinn.notifications.sms.send`:

1. **Inspect DLQ** — reads all DLQ messages, cross-references the database, and writes two list files:
   - `sms-send-dlq-expired.json` — notifications past their `expirytime`
   - `sms-send-dlq-pending.json` — notifications not yet expired

2. *(Optional)* Open either file in a text editor and remove any rows you do **not** want to act on.

3. **Process expired list** — sets the DB result to `Accepted` for non-terminal notifications, then purges their DLQ messages. The existing expiry-termination cron job (`terminateexpirednotifications`) will subsequently finalise them as `Failed_TTL` and complete the order lifecycle.

4. **Process pending list** — reconstructs a clean `SendSmsCommand` message for each entry and sends it to the main queue, then purges the DLQ message. Wolverine will retry delivery from scratch.

5. **Query DB state** — run at any point to check the current `result`, `expirytime`, and `resulttime` for all notifications in either list file.

## Output files

| File | Contents |
|---|---|
| `sms-send-dlq-expired.json` | DLQ items whose `expirytime` is in the past |
| `sms-send-dlq-pending.json` | DLQ items whose `expirytime` is in the future |

Both files are gitignored (they may contain phone numbers and message bodies).
