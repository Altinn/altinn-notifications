using System.Diagnostics;
using Altinn.Notifications.Persistence.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace StatusFeedBackfillTool;

/// <summary>
/// Service for backfilling missing status feed entries for orders.
/// Reuses existing C# repository logic (InsertStatusFeedForOrder) to ensure consistency with the application.
/// Supports filtering by order processing status, creator, date range, or providing a specific list of order IDs.
/// </summary>
public class StatusFeedBackfillService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly OrderRepository _orderRepository;
    private readonly BackfillSettings _settings;
    private readonly ILogger<StatusFeedBackfillService> _logger;

    private const string _getOldestStatusFeedDateSql = "SELECT COALESCE(MIN(created), '1900-01-01'::TIMESTAMPTZ) FROM notifications.statusfeed";

    private const string _getAffectedOrdersSql = @"
        WITH orders_identifiers AS (
            -- Get all orders referenced in orderschain (main orders)
            SELECT (OC.ORDERCHAIN ->> 'OrderId')::UUID AS orderid
            FROM NOTIFICATIONS.ORDERSCHAIN OC
            WHERE OC.ORDERCHAIN ? 'OrderId'
            
            UNION ALL
            
            -- Get all reminder orders referenced in orderschain
            SELECT (R ->> 'OrderId')::UUID AS orderid
            FROM NOTIFICATIONS.ORDERSCHAIN OC
            CROSS JOIN LATERAL JSONB_ARRAY_ELEMENTS(
                CASE
                    WHEN JSONB_TYPEOF(OC.ORDERCHAIN -> 'Reminders') = 'array'
                    THEN OC.ORDERCHAIN -> 'Reminders'
                    ELSE '[]'::JSONB
                END
            ) R
            WHERE R ? 'OrderId'
        )
        SELECT O.ALTERNATEID
        FROM NOTIFICATIONS.ORDERS O
        LEFT JOIN NOTIFICATIONS.STATUSFEED SF ON SF.ORDERID = O._ID
        INNER JOIN orders_identifiers OI ON OI.ORDERID = O.ALTERNATEID
        WHERE SF.ORDERID IS NULL
            AND O.PROCESSED >= @minProcessedDate
            AND (@creatorFilter IS NULL OR O.CREATORNAME = @creatorFilter)
            AND (@statusFilter IS NULL OR O.PROCESSEDSTATUS = @statusFilter::orderprocessingstate)
        ORDER BY O.PROCESSED ASC
        LIMIT @batchSize OFFSET @offset";

    private const string _filterOrdersWithoutStatusFeedSql = @"
        SELECT O.ALTERNATEID
        FROM NOTIFICATIONS.ORDERS O
        LEFT JOIN NOTIFICATIONS.STATUSFEED SF ON SF.ORDERID = O._ID
        WHERE O.ALTERNATEID = ANY(@orderIds)
            AND SF.ORDERID IS NULL";

    public StatusFeedBackfillService(
        NpgsqlDataSource dataSource,
        OrderRepository orderRepository,
        IOptions<BackfillSettings> settings,
        ILogger<StatusFeedBackfillService> logger)
    {
        _dataSource = dataSource;
        _orderRepository = orderRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task RunBackfill(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        LogBackfillSettings();

        var allOrders = await GetOrdersToProcess(cancellationToken);

        if (allOrders.Count == 0)
        {
            _logger.LogInformation("No orders found matching the criteria.");
            return;
        }

        _logger.LogInformation("Found {Count} orders to process", allOrders.Count);
        _logger.LogInformation("");

        var (totalProcessed, totalInserted, totalErrors) = await ProcessOrders(allOrders, cancellationToken);

        stopwatch.Stop();
        LogBackfillSummary(totalProcessed, totalInserted, totalErrors, stopwatch.Elapsed);
    }

    private void LogBackfillSettings()
    {
        var settings = $"Backfill Settings:{Environment.NewLine}" +
            $"  Batch Size: {_settings.BatchSize}{Environment.NewLine}" +
            $"  Dry Run: {_settings.DryRun}{Environment.NewLine}" +
            $"  Creator Filter: {_settings.CreatorNameFilter ?? "None (all creators)"}{Environment.NewLine}" +
            $"  Order Status Filter: {_settings.OrderProcessingStatusFilter?.ToString() ?? "None (all statuses)"}{Environment.NewLine}" +
            $"  Order IDs: {(_settings.OrderIds != null ? $"{_settings.OrderIds.Count} specific order(s)" : "None (using filters)")}";
        
        _logger.LogInformation("{Settings}", settings);
    }

    private async Task<List<Guid>> GetOrdersToProcess(CancellationToken cancellationToken)
    {
        // If specific order IDs are provided, use them directly
        if (_settings.OrderIds != null && _settings.OrderIds.Count > 0)
        {
            _logger.LogInformation("");
            _logger.LogInformation("Checking {Count} specific order(s)...", _settings.OrderIds.Count);
            return await FilterOrdersWithoutStatusFeed(_settings.OrderIds, cancellationToken);
        }
        else
        {
            // Otherwise, use filter-based approach
            DateTime minProcessedDate = await GetMinProcessedDate(cancellationToken);
            _logger.LogInformation("  Min Processed Date: {MinProcessedDate:yyyy-MM-dd HH:mm:ss}", minProcessedDate);
            _logger.LogInformation("");

            return await GetAllAffectedOrders(minProcessedDate, cancellationToken);
        }
    }

    private async Task<(int totalProcessed, int totalInserted, int totalErrors)> ProcessOrders(List<Guid> allOrders, CancellationToken cancellationToken)
    {
        int totalProcessed = 0;
        int totalInserted = 0;
        int totalErrors = 0;
        int currentOrder = 0;
        int totalOrderCount = allOrders.Count;

        foreach (var orderId in allOrders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested. Stopping processing.");
                break;
            }

            currentOrder++;

            if (currentOrder % 10 == 0 || currentOrder == 1)
            {
                _logger.LogInformation("Processing order {Current}/{Total}...", currentOrder, totalOrderCount);
            }

            try
            {
                if (!_settings.DryRun)
                {
                    await _orderRepository.InsertStatusFeedForOrder(orderId);
                    totalInserted++;
                }
                totalProcessed++;
            }
            catch (Exception ex)
            {
                totalErrors++;
                _logger.LogError(ex, "Error processing order {OrderId}: {Message}", orderId, ex.Message);
            }
        }

        return (totalProcessed, totalInserted, totalErrors);
    }

    private void LogBackfillSummary(int totalProcessed, int totalInserted, int totalErrors, TimeSpan elapsed)
    {
        var action = _settings.DryRun ? "Would Be Inserted" : "Inserted";
        var summary = $"{Environment.NewLine}========================================{Environment.NewLine}" +
            $"Backfill Summary{Environment.NewLine}" +
            $"========================================{Environment.NewLine}" +
            $"Total Orders Processed: {totalProcessed}{Environment.NewLine}" +
            $"Total Status Feed Entries {action}: {totalInserted}{Environment.NewLine}" +
            $"Total Errors: {totalErrors}{Environment.NewLine}" +
            $"Elapsed Time: {elapsed:hh\\:mm\\:ss}";
        
        _logger.LogInformation("{Summary}", summary);
        
        if (_settings.DryRun)
        {
            _logger.LogWarning("DRY RUN MODE - No changes were committed to the database");
        }
    }

    private async Task<DateTime> GetMinProcessedDate(CancellationToken cancellationToken)
    {
        if (_settings.MinProcessedDate.HasValue)
        {
            return _settings.MinProcessedDate.Value;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(_getOldestStatusFeedDateSql, connection);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (DateTime)result!;
    }

    private async Task<List<Guid>> GetAllAffectedOrders(DateTime minProcessedDate, CancellationToken cancellationToken)
    {
        var orders = new List<Guid>();
        int offset = 0;
        bool hasMore = true;

        while (hasMore)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(_getAffectedOrdersSql, connection);

            command.Parameters.AddWithValue("minProcessedDate", NpgsqlDbType.TimestampTz, minProcessedDate);
            command.Parameters.AddWithValue("creatorFilter", NpgsqlDbType.Text, (object?)_settings.CreatorNameFilter ?? DBNull.Value);

            command.Parameters.AddWithValue("statusFilter", NpgsqlDbType.Text, _settings.OrderProcessingStatusFilter.HasValue
                ? _settings.OrderProcessingStatusFilter.Value.ToString()
                : (object)DBNull.Value);

            command.Parameters.AddWithValue("batchSize", NpgsqlDbType.Integer, _settings.BatchSize);
            command.Parameters.AddWithValue("offset", NpgsqlDbType.Integer, offset);

            int batchCount = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                orders.Add(reader.GetGuid(0));
                batchCount++;
            }

            if (batchCount < _settings.BatchSize)
            {
                hasMore = false;
            }
            else
            {
                offset += _settings.BatchSize;
            }
        }

        return orders;
    }

    private async Task<List<Guid>> FilterOrdersWithoutStatusFeed(List<Guid> orderIds, CancellationToken cancellationToken)
    {
        var ordersWithoutStatusFeed = new List<Guid>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(_filterOrdersWithoutStatusFeedSql, connection);
        
        command.Parameters.AddWithValue("orderIds", orderIds.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ordersWithoutStatusFeed.Add(reader.GetGuid(0));
        }

        return ordersWithoutStatusFeed;
    }
}
