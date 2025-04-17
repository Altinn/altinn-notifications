using System.Collections.Immutable;
using System.Data;
using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implements the <see cref="IShipmentDeliveryManifestRepository"/> interface, providing database
/// access operations for retrieving shipment information and delivery statuses.
/// </summary>
public partial class ShipmentDeliveryManifestRepository : IShipmentDeliveryManifestRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _statusColumnName = "status";
    private const string _referenceColumnName = "reference";
    private const string _lastUpdateColumnName = "last_update";
    private const string _destinationColumnName = "destination";

    private const string _defaultShipmentType = "Notification";

    private const string _sqlGetShipmentTrackingInfo = "SELECT * FROM notifications.get_shipment_tracking($1, $2)"; // (_alternateid, _creatorname)

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentDeliveryManifestRepository"/> class.
    /// </summary>
    public ShipmentDeliveryManifestRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IShipmentDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateid, string creatorName, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(_sqlGetShipmentTrackingInfo);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, alternateid);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, creatorName);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync(cancellationToken);

        return await CreateShipmentDeliveryManifestAsync(reader, alternateid, cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="ShipmentDeliveryManifest"/> from database query results.
    /// </summary>
    /// <param name="reader">Data reader positioned at the first row of shipment tracking data.</param>
    /// <param name="shipmentId">Unique identifier for the shipment.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. When completed successfully, the task result contains 
    /// a fully populated <see cref="IShipmentDeliveryManifest"/> with consolidated tracking information 
    /// for both the shipment and all its individual recipient deliveries.
    /// </returns>
    private static async Task<IShipmentDeliveryManifest> CreateShipmentDeliveryManifestAsync(NpgsqlDataReader reader, Guid shipmentId, CancellationToken cancellationToken)
    {
        var deliverableEntities = new List<IDeliveryManifest>();

        var (status, lastUpdate, reference) = await ExtractShipmentDataAsync(reader, cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            await CreateAndAddDeliveryEntityAsync(reader, deliverableEntities, cancellationToken);
        }

        return new ShipmentDeliveryManifest
        {
            Status = status,
            LastUpdate = lastUpdate,
            ShipmentId = shipmentId,
            Type = _defaultShipmentType,
            SendersReference = reference,
            StatusDescription = DescribeStatus(status),
            Recipients = deliverableEntities.ToImmutableList()
        };
    }

    /// <summary>
    /// Creates a deliverable entity and adds it to the collection if a valid destination is present.
    /// </summary>
    /// <param name="reader">
    /// An <see cref="NpgsqlDataReader"/> positioned at a row with deliverable entity
    /// tracking data, which must include destination, status, and timestamp values.
    /// </param>
    /// <param name="deliverableEntities">
    /// A collection to which the newly created <see cref="IDeliveryManifest"/> will be added.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the operation.
    /// </param>
    /// <remarks>
    /// If the destination field is null, the row is skipped and no entity is added.
    /// </remarks>
    private static async Task CreateAndAddDeliveryEntityAsync(NpgsqlDataReader reader, List<IDeliveryManifest> deliverableEntities, CancellationToken cancellationToken)
    {
        var statusOrdinal = reader.GetOrdinal(_statusColumnName);
        var referenceOrdinal = reader.GetOrdinal(_referenceColumnName);
        var lastUpdateOrdinal = reader.GetOrdinal(_lastUpdateColumnName);
        var destinationOrdinal = reader.GetOrdinal(_destinationColumnName);

        var isStatusNull = await reader.IsDBNullAsync(statusOrdinal, cancellationToken);
        var isTimestampNull = await reader.IsDBNullAsync(lastUpdateOrdinal, cancellationToken);
        var isDestinationNull = await reader.IsDBNullAsync(destinationOrdinal, cancellationToken);
        var isReferencePresent = !await reader.IsDBNullAsync(referenceOrdinal, cancellationToken);

        if (isStatusNull || isTimestampNull || isDestinationNull || isReferencePresent)
        {
            return;
        }

        var status = reader.GetString(statusOrdinal);
        var statusDescription = DescribeStatus(status);

        var lastUpdate = reader.GetDateTime(lastUpdateOrdinal);
        var destination = reader.GetString(destinationOrdinal);

        IDeliveryManifest deliverableEntity = MobileNumbersRegex().IsMatch(destination)
            ? new SmsDeliveryManifest
            {
                Status = status,
                LastUpdate = lastUpdate,
                Destination = destination,
                StatusDescription = statusDescription
            }
            : new EmailDeliveryManifest
            {
                Status = status,
                LastUpdate = lastUpdate,
                Destination = destination,
                StatusDescription = statusDescription
            };

        deliverableEntities.Add(deliverableEntity);
    }

    /// <summary>
    /// Extracts the status, timestamp of the last update, and sender's reference for the shipment.
    /// </summary>
    /// <param name="reader">Database reader positioned at a shipment data row.</param>
    /// <param name="cancellationToken">Token for canceling the operation.</param>
    /// <returns>
    /// A tuple with the shipment's status, timestamp of the last update, and sender's reference.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the row doesn't contain valid shipment data.
    /// </exception>
    private static async Task<(string Status, DateTime LastUpdate, string? Reference)> ExtractShipmentDataAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var statusOrdinal = reader.GetOrdinal(_statusColumnName);
        var referenceOrdinal = reader.GetOrdinal(_referenceColumnName);
        var lastUpdateOrdinal = reader.GetOrdinal(_lastUpdateColumnName);
        var destinationOrdinal = reader.GetOrdinal(_destinationColumnName);

        var isStatusNull = await reader.IsDBNullAsync(statusOrdinal, cancellationToken);
        var isTimestampNull = await reader.IsDBNullAsync(lastUpdateOrdinal, cancellationToken);
        var isDestinationPresent = !await reader.IsDBNullAsync(destinationOrdinal, cancellationToken);

        if (isStatusNull || isTimestampNull || isDestinationPresent)
        {
            throw new InvalidOperationException("Invalid notification order shipment data.");
        }

        var status = reader.GetString(statusOrdinal);
        var lastUpdate = reader.GetDateTime(lastUpdateOrdinal);
        var reference = await reader.IsDBNullAsync(referenceOrdinal, cancellationToken) ? null : reader.GetString(referenceOrdinal);

        return (status, lastUpdate, reference);
    }

    /// <summary>
    /// Processes a reader row for recipient data and adds it to the deliverables collection.
    /// </summary>
    /// <param name="status">The database reader positioned at a data row.</param>
    private static string DescribeStatus(string status)
    {
        return $"A human-readable explanation of the current status: {status}, including additional context or error details when applicable.";
    }

    /// <summary>
    /// Provides a regular expression pattern for validating mobile phone numbers.
    /// </summary>
    /// <returns>
    /// A <see cref="Regex"/> that matches mobile phone number formats, accepting:
    /// <list type="bullet">
    ///   <item><description>Optional leading '+' or '00' international prefix</description></item>
    ///   <item><description>One or more digits</description></item>
    /// </list>
    /// </returns>
    [GeneratedRegex(@"^(?:\+|00)?\d+$")]
    private static partial Regex MobileNumbersRegex();
}
