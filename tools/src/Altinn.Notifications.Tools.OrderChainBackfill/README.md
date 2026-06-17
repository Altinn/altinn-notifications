# OrderChainBackfill

Backfills `orders._orderchainid` for existing rows that pre-date the column addition. Works in two phases:

1. **Fill** — reads `orderschain` in batches and populates a staging table with `(orderid, creatorname, orderchainid)` for every main order and reminder in each chain.
2. **Update** — reads the staging table in batches and updates `orders._orderchainid`, joining on the existing `(alternateid, creatorname)` composite index.

Both phases are resumable. Each batch is committed independently and the cursor is persisted to a file after every commit, so a crash or deliberate stop can be continued with a re-run.

**This tool is temporary** — delete the branch once the backfill has been run in all environments.

---

## Prerequisites

### 1. Create the staging table manually in each environment

```sql
CREATE TABLE notifications.orderchain_backfill_lookup (
    orderid      UUID   NOT NULL,
    creatorname  TEXT   NOT NULL,
    orderchainid BIGINT NOT NULL,
    PRIMARY KEY (orderchainid, orderid)
);
```

The composite primary key index on `(orderchainid, orderid)` covers both the batch range filter in phase 2 and duplicate protection on re-runs. Run this before starting the tool.

### 2. Set the database connection string via user secrets

```bash
cd tools/src/Altinn.Notifications.Tools.OrderChainBackfill

dotnet user-secrets set "PostgreSQLSettings:ConnectionString" "Host=<host>;Port=5432;Username=platform_notifications;Password=<password>;Database=notificationsdb"
```

`appsettings.json` ships with a local development default (localhost, password `Password`). The user secret overrides it for any other environment.

### 3. Adjust settings in `appsettings.json` if needed

| Setting | Default | Description |
|---|---|---|
| `BackfillSettings:BatchSize` | `1000` | Rows per batch (applies to both phases) |
| `BackfillSettings:MaxIterations` | `0` | Max batches per phase. `0` = run to completion |
| `BackfillSettings:FillCursorFilePath` | `fill_cursor.txt` | Cursor file for phase 1 |
| `BackfillSettings:CursorFilePath` | `backfill_cursor.txt` | Cursor file for phase 2 |
| `BackfillSettings:LogFilePath` | `backfill_log.txt` | Append-only log of all batch results |

---

## Running

The tool accepts an optional argument to control which phase runs:

```bash
# Phase 1 only: fill the staging table
dotnet run -- fill

# Phase 2 only: update orders from staging table
dotnet run -- update

# Both phases in sequence (phase 2 only starts once phase 1 is complete)
dotnet run
```

**Recommended workflow:**

1. Run phase 1 to fill the staging table:
   ```bash
   dotnet run -- fill
   ```

2. Inspect the staging table to verify the data looks correct before applying updates:
   ```sql
   SELECT * FROM notifications.orderchain_backfill_lookup LIMIT 50;
   SELECT COUNT(*) FROM notifications.orderchain_backfill_lookup;
   ```

3. Run phase 2 to apply the updates:
   ```bash
   dotnet run -- update
   ```

### Test run with a limited number of batches

Set `MaxIterations` to a small number (e.g. `2`) in `appsettings.json` to process only a few batches and inspect the results before committing to the full run.

### Resume after a crash or deliberate stop

Re-run the same command. The tool reads the cursor file(s) on startup and continues from where it left off.

### Start a phase over from the beginning

Delete the relevant cursor file before running:

```bash
rm fill_cursor.txt    # restart phase 1
rm backfill_cursor.txt  # restart phase 2
```

---

## Cleanup

Once the backfill is confirmed complete in an environment, drop the staging table:

```sql
DROP TABLE notifications.orderchain_backfill_lookup;
```

---

## Output

Progress is printed to the console and appended to `backfill_log.txt`:

```
[2026-06-17 10:00:01] Running phase: Both. Upper limit: 45000.
[2026-06-17 10:00:01] Phase 1: Filling staging table — fill cursor: 0, batch size: 1000, max iterations: unlimited (run to completion)
[2026-06-17 10:00:02] Fill     1: orderschain _id (       0,    1000] —  1100 rows inserted  (total: 1100, progress: 1000/45000)
[2026-06-17 10:00:03] Fill     2: orderschain _id (    1000,    2000] —   950 rows inserted  (total: 2050, progress: 2000/45000)
...
[2026-06-17 10:03:00] Phase 1 complete. Total rows inserted into staging table: 48200
[2026-06-17 10:03:00] Phase 2: Updating orders — cursor: 0, batch size: 1000, max iterations: unlimited (run to completion)
[2026-06-17 10:03:01] Batch    1: orderchainid (       0,    1000] —   312 orders updated  (total: 312, progress: 1000/45000)
...
[2026-06-17 10:05:30] Phase 2 complete. Total orders updated: 14203
```

The fill row count can exceed the number of orderschain rows because each chain with reminders produces multiple entries (one per reminder plus one for the main order). Orders with no matching chain entry produce `0 orders updated` for their batch — this is expected for orders created outside of a chain.

---

## Safety

- Each batch is wrapped in a transaction. A crash mid-batch rolls back automatically; the cursor is only advanced after a successful commit.
- The fill INSERT uses `ON CONFLICT DO NOTHING` against the composite primary key, so re-running a batch that was already committed is safe — duplicate rows are silently skipped.
- The UPDATE is idempotent (`WHERE _orderchainid IS NULL`), so re-running a batch that was already committed is safe and produces 0 updates.
- The upper limit is snapshotted from `MAX(_id)` at startup. Rows added during the run are not affected (they are written with `_orderchainid` set directly by the application).
