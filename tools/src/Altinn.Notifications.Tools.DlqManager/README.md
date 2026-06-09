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
dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "Host=<host>;Port=5432;Username=<user>;Password=<pwd>;Database=notificationsdb;"
```

Non-secret defaults (queue names, output file paths) live in `appsettings.json`.

## Running

```bash
dotnet run
```

The tool presents an interactive menu. Select a queue (1–9), then choose an operation from the queue-specific sub-menu.

## SMS send queue workflow

The typical operator workflow for `altinn.notifications.sms.send`:

1. **Inspect DLQ** — reads all DLQ messages via AMQP, cross-references the database, and writes three list files based on the notification's DB state:
   - `sms-send-dlq-sending-expired.json` — `result = 'Sending'`, `expirytime <= NOW()`
   - `sms-send-dlq-sending-pending.json` — `result = 'Sending'`, `expirytime > NOW()`
   - `sms-send-dlq-other.json` — any other DB result (already terminal, Accepted, etc.)

2. *(Optional)* Open any list file and remove rows you do **not** want to act on yet.

> **Warning**: Steps 3–5 purge DLQ messages permanently. Review list files carefully before proceeding.

3. **Process sending-expired list** — sets the DB result to `Accepted` for notifications still in `Sending` state, then purges their DLQ messages. The existing expiry-termination cron (`terminateexpirednotifications`) subsequently finalises them as `Failed_TTL` and completes the order lifecycle.

4. **Process sending-pending list** — reconstructs a clean `SendSmsCommand` for each entry and sends it back to the main queue (with Wolverine envelope headers preserved), then purges the DLQ message. Wolverine retries delivery from scratch with a fresh delivery count.

5. **Purge other-status list** — purges DLQ messages only. No DB changes are made. Use for messages whose DB result is already terminal or in an unexpected state.

6. **Query DB state** — run at any point to check the current `result`, `expirytime`, and `resulttime` for all notifications in a list file.

## Output files

| File | DB condition | Intended action |
|---|---|---|
| `sms-send-dlq-sending-expired.json` | `result = 'Sending'` AND `expirytime <= NOW()` | Mark DB `Accepted` → expiry cron finalises as `Failed_TTL` |
| `sms-send-dlq-sending-pending.json` | `result = 'Sending'` AND `expirytime > NOW()` | Resubmit to main queue |
| `sms-send-dlq-other.json` | anything else | Purge DLQ only |

All three files are gitignored (they may contain phone numbers and message bodies).

## Verifying DLQ counts

The tool's DLQ count (shown in the sub-menu header) and the Inspect operation both use a direct AMQP connection, which reflects the real-time queue state.

**Do not rely on the Azure Portal's "Peek from start" to verify results.** After large batch operations the portal's management layer can show stale counts and peek results for several minutes. Use the tool's own count or Grafana for reliable verification.
