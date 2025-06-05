using System.Collections.Immutable;
using System.Data;
using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Mappers;
using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Repository service for retrieving the delivery manifest for notification orders.
/// </summary>
public partial class NotificationDeliveryManifestRepository : INotificationDeliveryManifestRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _typeColumnName = "type";
    private const string _statusColumnName = "status";
    private const string _referenceColumnName = "reference";
    private const string _lastUpdateColumnName = "last_update";
    private const string _destinationColumnName = "destination";

    private const string _getShipmentTrackingInfoFunction = "SELECT * FROM notifications.get_shipment_tracking_v2($1, $2)"; // (_alternateid, _creatorname)

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDeliveryManifestRepository"/> class.
    /// </summary>
    public NotificationDeliveryManifestRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<INotificationDeliveryManifest?> GetDeliveryManifestAsync(Guid alternateId, string creatorName, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(_getShipmentTrackingInfoFunction);
        command.Parameters.AddWithValue(NpgsqlDbType.Uuid, alternateId);
        command.Parameters.AddWithValue(NpgsqlDbType.Text, creatorName);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync(cancellationToken);

        return await CreateNotificationDeliveryManifestAsync(reader, alternateId, cancellationToken);
    }

    /// <summary>
    /// Retrieves the order type from the current database record.
    /// </summary>
    /// <param name="reader">Database reader positioned at a notification order data row.</param>
    /// <returns>A string representing the order type (e.g., "Notification", "Reminder").</returns>
    private static string GetType(NpgsqlDataReader reader)
    {
        var typeOrdinal = reader.GetOrdinal(_typeColumnName);
        return reader.GetString(typeOrdinal);
    }

    /// <summary>
    /// Retrieves the sender's reference for the notification order.
    /// </summary>
    /// <param name="reader">Database reader positioned at a notification order data row.</param>
    /// <param name="cancellationToken">Token for canceling the asynchronous operation.</param>
    /// <returns>
    /// The sender's reference as a string, or <see langword="null"/> if no reference is present.
    /// </returns>
    private static async Task<string?> GetSenderReferenceAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var referenceOrdinal = reader.GetOrdinal(_referenceColumnName);
        return await reader.IsDBNullAsync(referenceOrdinal, cancellationToken) ? null : reader.GetString(referenceOrdinal);
    }

    /// <summary>
    /// Retrieves the status and last update timestamp for the notification order.
    /// </summary>
    /// <param name="reader">Database reader positioned at a notification order data row.</param>
    /// <param name="cancellationToken">Token for canceling the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the notification order's status and timestamp of the last update.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the row doesn't contain valid notification order data. This happens if:
    /// <list type="bullet">
    ///   <item><description>The status is null</description></item>
    ///   <item><description>The timestamp is null</description></item>
    /// </list>
    /// </exception>
    private static async Task<(string Status, DateTime LastUpdate)> GetStatusAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var statusOrdinal = reader.GetOrdinal(_statusColumnName);
        var isStatusNull = await reader.IsDBNullAsync(statusOrdinal, cancellationToken);

        var lastUpdateOrdinal = reader.GetOrdinal(_lastUpdateColumnName);
        var isTimestampNull = await reader.IsDBNullAsync(lastUpdateOrdinal, cancellationToken);

        if (isStatusNull || isTimestampNull)
        {
            throw new InvalidOperationException("Invalid notification order data structure.");
        }

        var status = reader.GetString(statusOrdinal);
        var lastUpdate = reader.GetDateTime(lastUpdateOrdinal);

        return (status, lastUpdate);
    }

    /// <summary>
    /// Creates a delivery manifest entity from the current database row and adds it to the collection.
    /// </summary>
    /// <param name="reader">
    /// An <see cref="NpgsqlDataReader"/> positioned at a row containing recipient delivery data.
    /// </param>
    /// <param name="deliveryManifestEntities">
    /// A collection to which the newly created <see cref="IDeliveryManifest"/> will be added.
    /// </param>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task PopulateDeliveryManifestEntitiesAsync(NpgsqlDataReader reader, List<IDeliveryManifest> deliveryManifestEntities, CancellationToken cancellationToken)
    {
        var statusOrdinal = reader.GetOrdinal(_statusColumnName);
        var lastUpdateOrdinal = reader.GetOrdinal(_lastUpdateColumnName);
        var destinationOrdinal = reader.GetOrdinal(_destinationColumnName);

        var isStatusNull = await reader.IsDBNullAsync(statusOrdinal, cancellationToken);
        var isTimestampNull = await reader.IsDBNullAsync(lastUpdateOrdinal, cancellationToken);
        var isDestinationNull = await reader.IsDBNullAsync(destinationOrdinal, cancellationToken);

        if (isStatusNull || isTimestampNull || isDestinationNull)
        {
            return;
        }

        var status = reader.GetString(statusOrdinal);

        var lastUpdate = reader.GetDateTime(lastUpdateOrdinal);
        var destination = reader.GetString(destinationOrdinal);

        IDeliveryManifest deliverableEntity = Utilities.Helpers.MobileNumbersRegex().IsMatch(destination)
            ? new SmsDeliveryManifest
            {
                LastUpdate = lastUpdate,
                Destination = destination,
                Status = ProcessingLifecycleMapper.GetSmsLifecycleStage(status),
            }
            : new EmailDeliveryManifest
            {
                LastUpdate = lastUpdate,
                Destination = destination,
                Status = ProcessingLifecycleMapper.GetEmailLifecycleStage(status),
            };

        deliveryManifestEntities.Add(deliverableEntity);
    }

    /// <summary>
    /// Builds a <see cref="NotificationDeliveryManifest"/> from database query results.
    /// </summary>
    /// <param name="reader">Data reader positioned at the first row of shipment tracking data.</param>
    /// <param name="alternateId">Unique identifier for the notification order.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation. When completed successfully, the task result contains 
    /// a fully populated <see cref="INotificationDeliveryManifest"/> with consolidated tracking information 
    /// for both the shipment and all its individual recipient deliveries.
    /// </returns>
    private static async Task<INotificationDeliveryManifest> CreateNotificationDeliveryManifestAsync(NpgsqlDataReader reader, Guid alternateId, CancellationToken cancellationToken)
    {
        var deliverableEntities = new List<IDeliveryManifest>();

        var type = GetType(reader);

        var reference = await GetSenderReferenceAsync(reader, cancellationToken);

        var (status, lastUpdate) = await GetStatusAsync(reader, cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            await PopulateDeliveryManifestEntitiesAsync(reader, deliverableEntities, cancellationToken);
        }

        return new NotificationDeliveryManifest
        {
            Type = type,
            LastUpdate = lastUpdate,
            ShipmentId = alternateId,
            SendersReference = reference,
            Status = ProcessingLifecycleMapper.GetOrderLifecycleStage(status),
            Recipients = deliverableEntities.ToImmutableList()
        };
    }
}
