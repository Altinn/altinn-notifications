using Altinn.Notifications.Tools.PerfSnapshot.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets<PostgreSqlSettings>(optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args, new Dictionary<string, string>
    {
        ["--sendersRef"] = "PerfSnapshotSettings:SendersReference",
        ["--interval"] = "PerfSnapshotSettings:IntervalSeconds",
        ["--output"] = "PerfSnapshotSettings:OutputFilePath",
        ["--duration"] = "PerfSnapshotSettings:DurationMinutes",
    });

builder.Services.Configure<PostgreSqlSettings>(
    builder.Configuration.GetSection("PostgreSQLSettings"));

builder.Services.Configure<PerfSnapshotSettings>(
    builder.Configuration.GetSection("PerfSnapshotSettings"));

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<PostgreSqlSettings>>().Value;

    if (string.IsNullOrWhiteSpace(settings.ConnectionString))
    {
        throw new InvalidOperationException(
            "PostgreSQLSettings:ConnectionString is not configured. " +
            "Set it in appsettings.json or via user secrets.");
    }

    return new NpgsqlDataSourceBuilder(settings.ConnectionString).Build();
});

var host = builder.Build();

var perfSettings = host.Services.GetRequiredService<IOptions<PerfSnapshotSettings>>().Value;

if (string.IsNullOrWhiteSpace(perfSettings.SendersReference))
{
    Console.WriteLine("ERROR: no sendersReference configured. Pass --sendersRef <value> or set " +
        "PerfSnapshotSettings:SendersReference in appsettings.json / user secrets.");
    return 1;
}

if (perfSettings.IntervalSeconds <= 0)
{
    Console.WriteLine("ERROR: PerfSnapshotSettings:IntervalSeconds must be greater than 0.");
    return 1;
}

var dataSource = host.Services.GetRequiredService<NpgsqlDataSource>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Stopping — finishing current snapshot, then exiting...");
    cts.Cancel();
};

var fileExisted = File.Exists(perfSettings.OutputFilePath);
await using var writer = new StreamWriter(perfSettings.OutputFilePath, append: true) { AutoFlush = true };
if (!fileExisted)
{
    await writer.WriteLineAsync("timestamp_utc,elapsed_seconds,source,category,count");
}

var durationText = perfSettings.DurationMinutes is > 0
    ? $", stopping automatically after {perfSettings.DurationMinutes} minutes"
    : string.Empty;
Console.WriteLine($"Polling every {perfSettings.IntervalSeconds}s for sendersReference '{perfSettings.SendersReference}'{durationText}.");
Console.WriteLine($"Writing to {Path.GetFullPath(perfSettings.OutputFilePath)}. Press Ctrl+C to stop.");
Console.WriteLine();

var startedAt = DateTime.UtcNow;
var deadline = perfSettings.DurationMinutes is > 0
    ? startedAt.AddMinutes(perfSettings.DurationMinutes.Value)
    : (DateTime?)null;

while (!cts.IsCancellationRequested && (deadline is null || DateTime.UtcNow < deadline))
{
    var now = DateTime.UtcNow;
    var elapsedSeconds = (now - startedAt).TotalSeconds;

    List<(string Category, long Count)> orderCounts;
    List<(string Category, long Count)> emailCounts;
    var queryStartedAt = DateTime.UtcNow;

    try
    {
        orderCounts = await GetOrderStatusCountsAsync(
            dataSource, perfSettings.SendersReference, perfSettings.CommandTimeoutSeconds, cts.Token);
        emailCounts = await GetEmailResultCountsAsync(
            dataSource, perfSettings.SendersReference, perfSettings.CommandTimeoutSeconds, cts.Token);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"[{now:HH:mm:ss}] Snapshot failed: {ex.Message}");
        await WaitForNextTickAsync(perfSettings.IntervalSeconds, queryStartedAt, cts.Token);
        continue;
    }

    var queryDuration = DateTime.UtcNow - queryStartedAt;
    if (queryDuration.TotalSeconds > perfSettings.IntervalSeconds)
    {
        Console.WriteLine($"[{now:HH:mm:ss}] Note: queries took {queryDuration.TotalSeconds:F0}s, longer than " +
            $"the {perfSettings.IntervalSeconds}s interval — snapshots will be spaced further apart than requested.");
    }

    foreach (var (category, count) in orderCounts)
    {
        await writer.WriteLineAsync($"{now:O},{elapsedSeconds:F0},order,{category},{count}");
    }

    foreach (var (category, count) in emailCounts)
    {
        await writer.WriteLineAsync($"{now:O},{elapsedSeconds:F0},email,{category},{count}");
    }

    var orderSummary = orderCounts.Count > 0
        ? string.Join(" ", orderCounts.Select(c => $"{c.Category}={c.Count}"))
        : "(no orders yet)";
    var emailSummary = emailCounts.Count > 0
        ? string.Join(" ", emailCounts.Select(c => $"{c.Category}={c.Count}"))
        : "(none)";

    Console.WriteLine($"[{now:HH:mm:ss}] orders: {orderSummary} | emails: {emailSummary}");

    await WaitForNextTickAsync(perfSettings.IntervalSeconds, queryStartedAt, cts.Token);
}

Console.WriteLine("Stopped.");
return 0;

static async Task WaitForNextTickAsync(int intervalSeconds, DateTime queryStartedAt, CancellationToken cancellationToken)
{
    // Account for time already spent querying, so a slow query (which can take 1-2 minutes
    // once the tables under test have grown large) doesn't also get a full interval tacked
    // on afterwards — if the query alone already exceeded the interval, move on immediately.
    var remaining = TimeSpan.FromSeconds(intervalSeconds) - (DateTime.UtcNow - queryStartedAt);
    if (remaining <= TimeSpan.Zero)
    {
        return;
    }

    try
    {
        await Task.Delay(remaining, cancellationToken);
    }
    catch (OperationCanceledException)
    {
        // Expected on Ctrl+C — the outer loop checks cts.IsCancellationRequested next.
    }
}

static async Task<List<(string Category, long Count)>> GetOrderStatusCountsAsync(
    NpgsqlDataSource dataSource, string sendersReference, int commandTimeoutSeconds, CancellationToken cancellationToken)
{
    const string sql = """
        SELECT type::text, processedstatus::text, COUNT(_id)
        FROM notifications.orders
        WHERE sendersreference = $1
        GROUP BY type, processedstatus
        ORDER BY type, processedstatus;
        """;

    await using var command = dataSource.CreateCommand(sql);
    command.CommandTimeout = commandTimeoutSeconds;
    command.Parameters.AddWithValue(sendersReference);

    var results = new List<(string, long)>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        var type = reader.GetString(0);
        var status = reader.GetString(1);
        var count = reader.GetInt64(2);
        results.Add(($"{type}:{status}", count));
    }

    return results;
}

static async Task<List<(string Category, long Count)>> GetEmailResultCountsAsync(
    NpgsqlDataSource dataSource, string sendersReference, int commandTimeoutSeconds, CancellationToken cancellationToken)
{
    const string sql = """
        SELECT e.result::text, COUNT(e._id)
        FROM notifications.emailnotifications e
        INNER JOIN notifications.orders o ON o._id = e._orderid
        WHERE o.sendersreference = $1
        GROUP BY e.result
        ORDER BY e.result;
        """;

    await using var command = dataSource.CreateCommand(sql);
    command.CommandTimeout = commandTimeoutSeconds;
    command.Parameters.AddWithValue(sendersReference);

    var results = new List<(string, long)>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        var result = reader.GetString(0);
        var count = reader.GetInt64(1);
        results.Add((result, count));
    }

    return results;
}
