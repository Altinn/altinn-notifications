# OrderChainBackfill

Backfills `orders._orderchainid` for existing rows that pre-date the column addition. Processes `orderschain` in batches by sequential `_id`, committing each batch independently so the run is resumable after a crash or deliberate stop.

**This tool is temporary** — delete the branch once the backfill has been run in all environments.

---

## Setup

### 1. Set the database connection string via user secrets

```bash
cd tools/src/Altinn.Notifications.Tools.OrderChainBackfill

dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "Host=<host>;Port=5432;Username=platform_notifications;Password=<password>;Database=notificationsdb"
```

`appsettings.json` ships with a local development default (localhost, password `Password`). The user secret overrides it for any other environment.

### 2. Adjust settings in `appsettings.json` if needed

| Setting | Default | Description |
|---|---|---|
| `BackfillSettings:BatchSize` | `1000` | Number of `orderschain` rows per batch |
| `BackfillSettings:MaxIterations` | `0` | Maximum batches to process. `0` = run to completion |
| `BackfillSettings:CursorFilePath` | `backfill_cursor.txt` | File used to persist the cursor between runs |
| `BackfillSettings:LogFilePath` | `backfill_log.txt` | Append-only log of all batch results |

---

## Running

```bash
cd tools/src/Altinn.Notifications.Tools.OrderChainBackfill

dotnet run
```

### Run a limited number of batches (useful for testing)

Set `MaxIterations` to a small number in `appsettings.json`, e.g. `2`, to process only 2 batches and stop. Inspect the results, then set it back to `0` for the full run.

### Resume after a crash or deliberate stop

Just re-run `dotnet run`. The tool reads `backfill_cursor.txt` on startup and continues from where it left off.

### Start over from the beginning

Delete the cursor file before running:

```bash
rm backfill_cursor.txt
dotnet run
```

---

## Output

Progress is printed to the console and appended to `backfill_log.txt`:

```
[2026-06-17 10:01:23] Backfill starting — cursor: 0, upper limit: 45000, batch size: 1000, max iterations: unlimited (run to completion)
[2026-06-17 10:01:24] Batch    1: orderschain _id (       0,    1000] —  1000 chains,   312 orders updated  (total: 312, progress: 1000/45000)
[2026-06-17 10:01:25] Batch    2: orderschain _id (    1000,    2000] —  1000 chains,   289 orders updated  (total: 601, progress: 2000/45000)
...
[2026-06-17 10:03:41] Backfill complete. All 45000 orderschain rows processed. Total orders updated: 14203
```

Orders with no matching chain entry produce `0 orders updated` for their batch — this is expected for orders that were created outside of a chain.

---

## Safety

- Each batch is wrapped in a transaction. A crash mid-batch rolls back automatically; the cursor is only advanced after a successful commit.
- The UPDATE is idempotent (`WHERE _orderchainid IS NULL`), so re-running a batch that was already committed is safe and produces 0 updates.
- The upper limit is snapshotted from `MAX(_id)` at startup. Rows added during the run are not affected (they are written with `_orderchainid` set directly by the application).
