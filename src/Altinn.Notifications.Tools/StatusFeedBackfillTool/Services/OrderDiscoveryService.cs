using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using StatusFeedBackfillTool.Configuration;

namespace StatusFeedBackfillTool.Services;

/// <summary>
/// Service responsible for discovering orders that are missing status feed entries.
/// Can discover orders using filters or validate a manually provided list.
/// </summary>
public class OrderDiscoveryService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly DiscoverySettings _settings;
    private readonly ILogger<OrderDiscoveryService> _logger;

    private const string _getOldestStatusFeedDateSql = "SELECT COALESCE(MIN(created), '1900-01-01'::TIMESTAMPTZ) FROM notifications.statusfeed";

    private const string _getAffectedOrdersSql = @"
        SELECT O.ALTERNATEID
        FROM NOTIFICATIONS.ORDERS O
        WHERE O.PROCESSED >= @minProcessedDate
            AND (O.PROCESSEDSTATUS = 'Completed' OR O.PROCESSEDSTATUS = 'SendConditionNotMet')
            AND (@creatorFilter IS NULL OR O.CREATORNAME = @creatorFilter)
            AND (@statusFilter IS NULL OR O.PROCESSEDSTATUS = @statusFilter::orderprocessingstate)
            AND NOT EXISTS (
                SELECT 1
                FROM NOTIFICATIONS.STATUSFEED SF
                WHERE SF.ORDERID = O._ID
            )
        LIMIT @maxOrders";

    public OrderDiscoveryService(
        NpgsqlDataSource dataSource,
        IOptions<DiscoverySettings> settings,
        ILogger<OrderDiscoveryService> logger)
    {
        _dataSource = dataSource;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var settings = $"=== DISCOVER MODE ===\n\n" +
            $"Tool Settings:\n" +
            $"  Mode: Discover\n" +
            $"  Order IDs File: {_settings.OrderIdsFilePath}\n" +
            $"  Creator Filter: {_settings.CreatorNameFilter ?? "None (all creators)"}\n" +
            $"  Order Status Filter: {_settings.OrderProcessingStatusFilter?.ToString() ?? "None (all statuses)"}\n";
        
        _logger.LogInformation("{Settings}", settings);

        var affectedOrders = await DiscoverAffectedOrders(cancellationToken);

        stopwatch.Stop();

        if (affectedOrders.Count == 0)
        {
            _logger.LogInformation("No affected orders found.\nTotal elapsed time: {Elapsed:hh\\:mm\\:ss}", stopwatch.Elapsed);
            return;
        }
        
        await SaveOrdersToFile(affectedOrders, cancellationToken);
        
        _logger.LogInformation("Found {Count} affected orders\nAffected orders saved to: {FilePath}\nTo process these orders, run again in Backfill mode\nTotal elapsed time: {Elapsed:hh\\:mm\\:ss}", 
            affectedOrders.Count, _settings.OrderIdsFilePath, stopwatch.Elapsed);
    }

    private async Task<List<Guid>> DiscoverAffectedOrders(CancellationToken cancellationToken)
    {
        DateTime minProcessedDate = await GetMinProcessedDate(cancellationToken);
        _logger.LogInformation("  Min Processed Date: {MinProcessedDate:yyyy-MM-dd HH:mm:ss}\nDiscovering affected orders...", 
            minProcessedDate);

        return await GetAllAffectedOrders(minProcessedDate, cancellationToken);
    }

    private async Task SaveOrdersToFile(List<Guid> orders, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settings.OrderIdsFilePath, json, cancellationToken);
    }

    private async Task<DateTime> GetMinProcessedDate(CancellationToken cancellationToken)
    {
        if (_settings.MinProcessedDateFilter.HasValue)
        {
            return _settings.MinProcessedDateFilter.Value;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(_getOldestStatusFeedDateSql, connection);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (DateTime)result!;
    }

    private async Task<List<Guid>> GetAllAffectedOrders(DateTime minProcessedDate, CancellationToken cancellationToken)
    {
        var orders = new List<Guid>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(_getAffectedOrdersSql, connection);

        command.Parameters.AddWithValue("minProcessedDate", NpgsqlDbType.TimestampTz, minProcessedDate);
        command.Parameters.AddWithValue("creatorFilter", NpgsqlDbType.Text, (object?)_settings.CreatorNameFilter ?? DBNull.Value);

        command.Parameters.AddWithValue("statusFilter", NpgsqlDbType.Text, _settings.OrderProcessingStatusFilter.HasValue
            ? _settings.OrderProcessingStatusFilter.Value.ToString()
            : DBNull.Value);

        command.Parameters.AddWithValue("maxOrders", NpgsqlDbType.Integer, _settings.MaxOrders);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            orders.Add(reader.GetGuid(0));
        }

        return orders;
    }
}
