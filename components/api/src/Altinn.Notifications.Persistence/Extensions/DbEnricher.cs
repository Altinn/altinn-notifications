using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Altinn.Notifications.Persistence.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Altinn.Notifications.Persistence.Extensions;

/// <summary>
/// Provides methods to enrich telemetry data with database command information.
/// </summary>
public class DbEnricher
{
    private static readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(6)
    };

    private readonly ILogger<DbEnricher> _logger;
    private readonly IMemoryCache _sqlHashCache = new MemoryCache(new MemoryCacheOptions());
    private readonly PostgreSqlSettings _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbEnricher"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages from enrichment.</param>
    /// <param name="options">The PostgreSQL settings options.</param>
    public DbEnricher(ILogger<DbEnricher> logger, IOptions<PostgreSqlSettings> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Enriches the specified activity with the parameters of the given NpgsqlCommand.
    /// </summary>
    /// <param name="activity">The activity to enrich.</param>
    /// <param name="command">The NpgsqlCommand containing the parameters to add to the activity.</param>
    public void Enrich(Activity activity, NpgsqlCommand command)
    {
        var parameterWrapper = command.Parameters.Select((p, i) => new { Id = i, p.ParameterName, p.Value });
        activity.SetTag("Parameters", JsonSerializer.Serialize(parameterWrapper));

        activity.SetTag("db.connection_id", null);
        activity.SetTag("db.connection_string", null);
        activity.SetTag("db.name", null);
        activity.SetTag("db.user", null);
        activity.SetTag("net.peer.ip", null);
        activity.SetTag("net.peer.name", null);
        activity.SetTag("net.transport", null);
        activity.SetTag("db.query.text", null);

        (var hash, var firstHash) = GetCommandTextHash(command.CommandText);
        if (firstHash || _options.TelemetryAlwaysLogSqlText)
        {
            _logger.LogDebug("Enriching first observed SQL hash {DbHash}.", hash);
            if (firstHash)
            {
                activity.AddBaggage("firsthash", "true");
            }

            var commandSpan = command.CommandText.AsSpan();
            int maxTags = 8;
            int maxTagLength = 8192;
            for (int i = 0; i < maxTags; i++)
            {
                var commandFragment = commandSpan.Slice(i * maxTagLength, Math.Min(maxTagLength, commandSpan.Length - (i * maxTagLength))).ToString();
                activity.SetTag($"db.statement{i}", commandFragment);

                if (firstHash)
                {
                    // Also generate trace because postgres dependencies might be removed by DCR
                    _logger.LogWarning("Db decode {Index} for {DbHash}: {CommandFragment}.", i, hash, commandFragment);
                }

                if ((i + 1) * maxTagLength >= commandSpan.Length)
                {
                    break;
                }
            }
        }

        activity.AddTag("db.hash", hash.ToString());
    }

    private (ulong Hash, bool FirstHash) GetCommandTextHash(string commandText)
    {
        ulong hash = XxHash64Utf8(commandText);
        string cacheKey = $"S_{hash}";
        if (_sqlHashCache.TryGetValue(cacheKey, out _))
        {
            return (hash, false);
        }

        _sqlHashCache.Set(cacheKey, 0, _cacheOptions);
        return (hash, true);
    }

    private static ulong XxHash64Utf8(string parameter)
    {
        return System.IO.Hashing.XxHash64.HashToUInt64(MemoryMarshal.AsBytes(parameter.AsSpan()));
    }
}
