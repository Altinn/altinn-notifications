using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;

using Npgsql;

[assembly: ExcludeFromCodeCoverage]

const string StagingTable = "notifications.orderchain_backfill_lookup";

var phase = args.FirstOrDefault()?.ToLowerInvariant() switch
{
    "fill"   => Phase.Fill,
    "update" => Phase.Update,
    null     => Phase.Both,
    var arg  => throw new ArgumentException($"Unknown argument '{arg}'. Valid arguments: fill, update (or omit to run both).")
};

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .Build();

var credentials = config["PostgreSQLSettings:ConnectionString"]
    ?? throw new InvalidOperationException("PostgreSQLSettings:ConnectionString is required");

var batchSize = int.Parse(config["BackfillSettings:BatchSize"] ?? "1000");
var maxIterations = int.Parse(config["BackfillSettings:MaxIterations"] ?? "0");
var fillCursorFilePath = config["BackfillSettings:FillCursorFilePath"] ?? "fill_cursor.txt";
var cursorFilePath = config["BackfillSettings:CursorFilePath"] ?? "backfill_cursor.txt";
var logFilePath = config["BackfillSettings:LogFilePath"] ?? "backfill_log.txt";

await using var dataSource = new NpgsqlDataSourceBuilder(credentials).Build();

// Verify staging table exists before doing anything
try
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT 1 FROM {StagingTable} LIMIT 0";
    await cmd.ExecuteNonQueryAsync();
}
catch (PostgresException ex) when (ex.SqlState == "42P01") // undefined_table
{
    Log($"[{Ts()}] [ERROR] Staging table '{StagingTable}' does not exist. Create it manually — see README.md.");
    return;
}

// Snapshot upper limit from orderschain once — new rows added during the run don't need backfilling
long upperLimit;
await using (var conn = await dataSource.OpenConnectionAsync())
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT MAX(_id) FROM notifications.orderschain";
    var result = await cmd.ExecuteScalarAsync();
    upperLimit = result is DBNull or null ? 0L : (long)result;
}

if (upperLimit == 0)
{
    Log($"[{Ts()}] orderschain table is empty. Nothing to do.");
    return;
}

Log($"[{Ts()}] Running phase: {phase}. Upper limit: {upperLimit}.");

// ── Phase 1: fill staging table ───────────────────────────────────────────────

if (phase is Phase.Fill or Phase.Both)
{
    long fillCursor = 0;
    if (File.Exists(fillCursorFilePath))
    {
        var saved = await File.ReadAllTextAsync(fillCursorFilePath);
        if (long.TryParse(saved.Trim(), out var parsed))
            fillCursor = parsed;
    }

    if (fillCursor >= upperLimit)
    {
        Log($"[{Ts()}] Phase 1: Already complete (fill cursor {fillCursor} >= upper limit {upperLimit}). Skipping fill.");
    }
    else
    {
        Log($"[{Ts()}] Phase 1: Filling staging table — fill cursor: {fillCursor}, batch size: {batchSize}, " +
            $"max iterations: {(maxIterations == 0 ? "unlimited (run to completion)" : maxIterations.ToString())}");

        int fillIteration = 0;
        long totalInserted = 0;

        while (fillCursor < upperLimit && (maxIterations == 0 || fillIteration < maxIterations))
        {
            long fromId = fillCursor;
            long toId = Math.Min(fillCursor + batchSize, upperLimit);

            await using var conn = await dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"""
                    INSERT INTO {StagingTable} (orderid, creatorname, orderchainid)

                    -- main order
                    SELECT (orderchain ->> 'OrderId')::UUID, creatorname, _id
                    FROM notifications.orderschain
                    WHERE _id > @fromId AND _id <= @toId
                      AND orderchain ->> 'OrderId' IS NOT NULL

                    UNION ALL

                    -- reminder orders
                    SELECT (reminder ->> 'OrderId')::UUID, oc.creatorname, oc._id
                    FROM notifications.orderschain oc,
                         jsonb_array_elements(
                             CASE WHEN jsonb_typeof(oc.orderchain -> 'Reminders') = 'array'
                                  THEN oc.orderchain -> 'Reminders'
                                  ELSE '[]'::jsonb END
                         ) AS reminder
                    WHERE oc._id > @fromId AND oc._id <= @toId
                      AND reminder ->> 'OrderId' IS NOT NULL

                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("fromId", fromId);
                cmd.Parameters.AddWithValue("toId", toId);

                var inserted = await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();

                fillCursor = toId;
                await File.WriteAllTextAsync(fillCursorFilePath, fillCursor.ToString());

                fillIteration++;
                totalInserted += inserted;

                Log($"[{Ts()}] Fill  {fillIteration,4}: orderschain _id ({fromId,8}, {toId,8}] — " +
                    $"{inserted,5} rows inserted  (total: {totalInserted}, progress: {fillCursor}/{upperLimit})");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                Log($"[{Ts()}] [ERROR] Fill batch {fillIteration + 1} failed — fill cursor was {fillCursor}: {ex.Message}");
                throw;
            }
        }

        if (fillCursor >= upperLimit)
            Log($"[{Ts()}] Phase 1 complete. Total rows inserted into staging table: {totalInserted}");
        else
        {
            Log($"[{Ts()}] Phase 1 stopped after {fillIteration} iterations. Fill cursor: {fillCursor}/{upperLimit}. Re-run to continue.");
            return;
        }
    }
}

// ── Phase 2: batch update orders from staging table ───────────────────────────

if (phase is Phase.Update or Phase.Both)
{
    long cursor = 0;
    if (File.Exists(cursorFilePath))
    {
        var saved = await File.ReadAllTextAsync(cursorFilePath);
        if (long.TryParse(saved.Trim(), out var parsed))
            cursor = parsed;
    }

    if (cursor >= upperLimit)
    {
        Log($"[{Ts()}] Phase 2: Already complete (cursor {cursor} >= upper limit {upperLimit}).");
        return;
    }

    Log($"[{Ts()}] Phase 2: Updating orders — cursor: {cursor}, batch size: {batchSize}, " +
        $"max iterations: {(maxIterations == 0 ? "unlimited (run to completion)" : maxIterations.ToString())}");

    int iteration = 0;
    long totalUpdated = 0;

    while (cursor < upperLimit && (maxIterations == 0 || iteration < maxIterations))
    {
        long fromId = cursor;
        long toId = Math.Min(cursor + batchSize, upperLimit);

        await using var conn = await dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"""
                UPDATE notifications.orders o
                SET _orderchainid = l.orderchainid
                FROM {StagingTable} l
                WHERE o._orderchainid IS NULL
                  AND o.alternateid = l.orderid
                  AND o.creatorname = l.creatorname
                  AND l.orderchainid > @fromId AND l.orderchainid <= @toId
                """;
            cmd.Parameters.AddWithValue("fromId", fromId);
            cmd.Parameters.AddWithValue("toId", toId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            await tx.CommitAsync();

            cursor = toId;
            await File.WriteAllTextAsync(cursorFilePath, cursor.ToString());

            iteration++;
            totalUpdated += rowsAffected;

            Log($"[{Ts()}] Batch {iteration,4}: orderchainid ({fromId,8}, {toId,8}] — " +
                $"{rowsAffected,5} orders updated  (total: {totalUpdated}, progress: {cursor}/{upperLimit})");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            Log($"[{Ts()}] [ERROR] Batch {iteration + 1} failed — cursor was {cursor}: {ex.Message}");
            throw;
        }
    }

    if (cursor >= upperLimit)
        Log($"[{Ts()}] Phase 2 complete. Total orders updated: {totalUpdated}");
    else
        Log($"[{Ts()}] Stopped after {iteration} iterations. Cursor: {cursor}/{upperLimit}. Total updated: {totalUpdated}. Re-run to continue.");
}

string Ts() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

void Log(string message)
{
    Console.WriteLine(message);
    File.AppendAllText(logFilePath, message + Environment.NewLine);
}

enum Phase { Fill, Update, Both }
