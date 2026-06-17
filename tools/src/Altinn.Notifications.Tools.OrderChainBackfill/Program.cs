using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;

using Npgsql;

[assembly: ExcludeFromCodeCoverage]

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>(optional: true)
    .Build();

var credentials = config["PostgreSQLSettings:ConnectionString"]
    ?? throw new InvalidOperationException("PostgreSQLSettings:ConnectionString is required");

var batchSize = int.Parse(config["BackfillSettings:BatchSize"] ?? "1000");
var maxIterations = int.Parse(config["BackfillSettings:MaxIterations"] ?? "0");
var cursorFilePath = config["BackfillSettings:CursorFilePath"] ?? "backfill_cursor.txt";
var logFilePath = config["BackfillSettings:LogFilePath"] ?? "backfill_log.txt";

await using var dataSource = new NpgsqlDataSourceBuilder(credentials).Build();

// Resume from saved cursor or start from 0
long cursor = 0;
if (File.Exists(cursorFilePath))
{
    var saved = await File.ReadAllTextAsync(cursorFilePath);
    if (long.TryParse(saved.Trim(), out var parsed))
        cursor = parsed;
}

// Snapshot the upper limit once — new rows added during the run have higher _ids and don't need backfilling
long upperLimit;
await using (var conn = await dataSource.OpenConnectionAsync())
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT MAX(_id) FROM notifications.orderschain";
    var result = await cmd.ExecuteScalarAsync();
    upperLimit = result is DBNull or null ? 0L : (long)result;
}

Log($"[{Ts()}] Backfill starting — cursor: {cursor}, upper limit: {upperLimit}, batch size: {batchSize}, " +
    $"max iterations: {(maxIterations == 0 ? "unlimited (run to completion)" : maxIterations.ToString())}");

if (upperLimit == 0)
{
    Log($"[{Ts()}] orderschain table is empty. Nothing to do.");
    return;
}

if (cursor >= upperLimit)
{
    Log($"[{Ts()}] Cursor {cursor} is already at or beyond upper limit {upperLimit}. Nothing to do.");
    return;
}

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
        // Step 1: fetch the orderschain IDs in this batch window
        var chainIds = new List<long>();
        await using (var selectCmd = conn.CreateCommand())
        {
            selectCmd.Transaction = tx;
            selectCmd.CommandText = """
                SELECT _id FROM notifications.orderschain
                WHERE _id > @fromId AND _id <= @toId
                ORDER BY _id
                """;
            selectCmd.Parameters.AddWithValue("fromId", fromId);
            selectCmd.Parameters.AddWithValue("toId", toId);

            await using var reader = await selectCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                chainIds.Add(reader.GetInt64(0));
        }

        // Step 2: update orders whose alternateid matches any chain in this batch
        int rowsAffected = 0;
        if (chainIds.Count > 0)
        {
            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = """
                UPDATE notifications.orders o
                SET _orderchainid = oc._id
                FROM notifications.orderschain oc
                WHERE o._orderchainid IS NULL
                  AND oc._id = ANY(@chainIds)
                  AND (
                        o.alternateid = (oc.orderchain ->> 'OrderId')::UUID
                        OR EXISTS (
                            SELECT 1
                            FROM jsonb_array_elements(
                                CASE
                                    WHEN jsonb_typeof(oc.orderchain -> 'Reminders') = 'array'
                                    THEN oc.orderchain -> 'Reminders'
                                    ELSE '[]'::jsonb
                                END
                            ) AS reminder
                            WHERE (reminder ->> 'OrderId')::UUID = o.alternateid
                        )
                  )
                """;
            updateCmd.Parameters.AddWithValue("chainIds", chainIds.ToArray());

            rowsAffected = await updateCmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();

        cursor = toId;
        await File.WriteAllTextAsync(cursorFilePath, cursor.ToString());

        iteration++;
        totalUpdated += rowsAffected;

        Log($"[{Ts()}] Batch {iteration,4}: orderschain _id ({fromId,8}, {toId,8}] — " +
            $"{chainIds.Count,5} chains, {rowsAffected,5} orders updated  (total: {totalUpdated}, progress: {cursor}/{upperLimit})");
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        Log($"[{Ts()}] [ERROR] Batch {iteration + 1} failed — cursor was {cursor}: {ex.Message}");
        throw;
    }
}

if (cursor >= upperLimit)
    Log($"[{Ts()}] Backfill complete. All {upperLimit} orderschain rows processed. Total orders updated: {totalUpdated}");
else
    Log($"[{Ts()}] Stopped after {iteration} iterations. Cursor: {cursor}/{upperLimit}. Total updated: {totalUpdated}. Re-run to continue.");

string Ts() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

void Log(string message)
{
    Console.WriteLine(message);
    File.AppendAllText(logFilePath, message + Environment.NewLine);
}
