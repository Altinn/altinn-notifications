using System.Collections.Immutable;
using System.Data;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of the <see cref="IShipmentRepository"/> providing persistence operations for notification shipment data.
/// </summary>
public class ShipmentRepository : IShipmentRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _statusColumnName = "status";
    private const string _referenceColumnName = "reference";
    private const string _lastUpdateColumnName = "last_update";
    private const string _destinationColumnName = "destination";

    private const string _defaultStatus = "Unknown";
    private const string _defaultShipmentType = "Notification";

    private const string _sqlGetShipmentTracking = "SELECT * FROM notifications.get_shipment_tracking($1, $2)"; // (_alternateid, _creatorname)

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentRepository"/> class.
    /// </summary>
    public ShipmentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IShipmentDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(_sqlGetShipmentTracking);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, alternateid);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, creatorName);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync(cancellationToken);

        return await ReadShipmentDeliveryManifestAsync(reader, alternateid, cancellationToken);
    }

    /// <summary>
    /// Reads and constructs a <see cref="ShipmentDeliveryManifest"/> from the provided <see cref="NpgsqlDataReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="NpgsqlDataReader"/> containing the shipment tracking data, including shipment metadata and recipient delivery results.</param>
    /// <param name="shipmentId">The unique identifier of the shipment to associate with the manifest.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the asynchronous operation, if needed.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains an <see cref="IShipmentDeliveryManifest"/>
    /// with shipment metadata and delivery status for all recipients included in the shipment.
    /// </returns>
    /// <remarks>
    /// The method expects the first row to contain both shipment-level and recipient-level data. 
    /// It extracts metadata such as shipment status and sender's reference, then iterates through the result set to build 
    /// a complete list of deliverable entities (e.g., email and SMS deliveries). The resulting manifest aggregates all recipient 
    /// delivery statuses associated with the given shipment.
    /// </remarks>
    private static async Task<IShipmentDeliveryManifest> ReadShipmentDeliveryManifestAsync(NpgsqlDataReader reader, Guid shipmentId, CancellationToken cancellationToken)
    {
        var deliverables = new List<IDeliverableEntity>();

        var (status, reference, lastUpdate) = ExtractShipmentTrackingData(reader);

        ProcessRecipientRow(reader, deliverables);

        // Process any additional rows for recipient data
        while (await reader.ReadAsync(cancellationToken))
        {
            ProcessRecipientRow(reader, deliverables);
        }

        return new ShipmentDeliveryManifest
        {
            Status = status,
            ShipmentId = shipmentId,
            LastUpdate = lastUpdate,
            Type = _defaultShipmentType,
            SendersReference = reference,
            StatusDescription = DescribeStatus(status),
            Recipients = deliverables.ToImmutableList()
        };
    }

    /// <summary>
    /// Extracts shipment-level tracking data from the current row in the <see cref="NpgsqlDataReader"/>.
    /// </summary>
    /// <param name="reader">
    /// The <see cref="NpgsqlDataReader"/> instance positioned at a row containing shipment tracking data. 
    /// This method expects the row to include columns for status, sender reference, and last update timestamp.
    /// </param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><description><c>Status</c>: The shipment's processing status as a string.</description></item>
    ///   <item><description><c>Reference</c>: An optional reference provided by the sender, or <c>null</c> if not available.</description></item>
    ///   <item><description><c>LastUpdate</c>: The most recent update timestamp associated with the shipment. Defaults to <see cref="DateTime.MinValue"/> if not present.</description></item>
    /// </list>
    /// </returns>
    private static (string Status, string? Reference, DateTime LastUpdate) ExtractShipmentTrackingData(NpgsqlDataReader reader)
    {
        var statusOrdinal = reader.GetOrdinal(_statusColumnName);
        var referenceOrdinal = reader.GetOrdinal(_referenceColumnName);
        var lastUpdateOrdinal = reader.GetOrdinal(_lastUpdateColumnName);

        string status = reader.IsDBNull(statusOrdinal) ? _defaultStatus : reader.GetString(statusOrdinal);

        string? reference = reader.IsDBNull(referenceOrdinal) ? null : reader.GetString(referenceOrdinal);

        DateTime lastUpdate = reader.IsDBNull(lastUpdateOrdinal) ? DateTime.UtcNow : reader.GetDateTime(lastUpdateOrdinal);

        return (status, reference, lastUpdate);
    }

    /// <summary>
    /// Processes a reader row for recipient data and adds it to the deliverables collection.
    /// </summary>
    /// <param name="reader">The database reader positioned at a data row.</param>
    /// <param name="deliverables">Collection to add the extracted deliverable entity to.</param>
    private static void ProcessRecipientRow(NpgsqlDataReader reader, List<IDeliverableEntity> deliverables)
    {
        var destinationOrdinal = reader.GetOrdinal(_destinationColumnName);
        if (reader.IsDBNull(destinationOrdinal))
        {
            return;
        }

        var statusOrdinal = reader.GetOrdinal(_statusColumnName);
        var lastUpdateOrdinal = reader.GetOrdinal(_lastUpdateColumnName);

        // Extract recipient data
        string destination = reader.GetString(destinationOrdinal);

        string status = reader.IsDBNull(statusOrdinal)
            ? _defaultStatus
            : reader.GetString(statusOrdinal);

        DateTime lastUpdate = reader.IsDBNull(lastUpdateOrdinal)
            ? DateTime.MinValue
            : reader.GetDateTime(lastUpdateOrdinal);

        IDeliverableEntity entity = destination.Contains('@')
            ? new EmailDeliveryManifest
            {
                Status = status,
                LastUpdate = lastUpdate,
                Destination = destination,
                StatusDescription = DescribeStatus(status)
            }
            : new SmsDeliveryManifest
            {
                Status = status,
                LastUpdate = lastUpdate,
                Destination = destination,
                StatusDescription = DescribeStatus(status)
            };

        deliverables.Add(entity);
    }

    /// <summary>
    /// Processes a reader row for recipient data and adds it to the deliverables collection.
    /// </summary>
    /// <param name="status">The database reader positioned at a data row.</param>
    private static string DescribeStatus(string status)
    {
        return status;
    }
}
