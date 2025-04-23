using System.Collections.Immutable;
using System.Data;
using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Persistence;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Repository service for retrieving the delivery manifest for notification orders.
/// </summary>
public partial class NotificationDeliveryManifestRepository : INotificationDeliveryManifestRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _statusColumnName = "status";
    private const string _referenceColumnName = "reference";
    private const string _lastUpdateColumnName = "last_update";
    private const string _destinationColumnName = "destination";

    private const string _sqlGetShipmentTrackingInfo = "SELECT * FROM notifications.get_shipment_tracking($1, $2)"; // (_alternateid, _creatorname)

    /// <summary>
    /// Maps database SMS notification result types to ProcessingLifecycle values.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ProcessingLifecycle> SmsStatusMap = new Dictionary<string, ProcessingLifecycle>(StringComparer.OrdinalIgnoreCase)
    {
        { "new", ProcessingLifecycle.SMS_New },
        { "failed", ProcessingLifecycle.SMS_Failed },
        { "sending", ProcessingLifecycle.SMS_Sending },
        { "accepted", ProcessingLifecycle.SMS_Accepted },
        { "delivered", ProcessingLifecycle.SMS_Delivered },
        { "failed_deleted", ProcessingLifecycle.SMS_Failed_Deleted },
        { "failed_expired", ProcessingLifecycle.SMS_Failed_Expired },
        { "failed_rejected", ProcessingLifecycle.SMS_Failed_Rejected },
        { "failed_undelivered", ProcessingLifecycle.SMS_Failed_Undelivered },
        { "failed_barredreceiver", ProcessingLifecycle.SMS_Failed_BarredReceiver },
        { "failed_invalidreceiver", ProcessingLifecycle.SMS_Failed_InvalidRecipient },
        { "failed_invalidrecipient", ProcessingLifecycle.SMS_Failed_InvalidRecipient },
        { "failed_recipientreserved", ProcessingLifecycle.SMS_Failed_RecipientReserved },
        { "failed_recipientnotidentified", ProcessingLifecycle.SMS_Failed_RecipientNotIdentified }
    };

    /// <summary>
    /// Maps database email notification result types to ProcessingLifecycle values.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ProcessingLifecycle> EmailStatusMap = new Dictionary<string, ProcessingLifecycle>(StringComparer.OrdinalIgnoreCase)
    {
        { "new", ProcessingLifecycle.Email_New },
        { "failed", ProcessingLifecycle.Email_Failed },
        { "sending", ProcessingLifecycle.Email_Sending },
        { "succeeded", ProcessingLifecycle.Email_Succeeded },
        { "delivered", ProcessingLifecycle.Email_Delivered },
        { "failed_bounced", ProcessingLifecycle.Email_Failed_Bounced },
        { "failed_quarantined", ProcessingLifecycle.Email_Failed_Quarantined },
        { "failed_filteredspam", ProcessingLifecycle.Email_Failed_FilteredSpam },
        { "failed_transienterror", ProcessingLifecycle.Email_Failed_TransientError },
        { "failed_invalidemailformat", ProcessingLifecycle.Email_Failed_InvalidFormat },
        { "failed_recipientreserved", ProcessingLifecycle.Email_Failed_RecipientReserved },
        { "failed_supressedrecipient", ProcessingLifecycle.Email_Failed_SuppressedRecipient },
        { "failed_recipientnotidentified", ProcessingLifecycle.Email_Failed_RecipientNotIdentified }
    };

    /// <summary>
    /// Maps database order processing state types to ProcessingLifecycle values.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ProcessingLifecycle> OrderStatusMap = new Dictionary<string, ProcessingLifecycle>(StringComparer.OrdinalIgnoreCase)
    {
        { "cancelled", ProcessingLifecycle.Order_Cancelled },
        { "completed", ProcessingLifecycle.Order_Completed },
        { "registered", ProcessingLifecycle.Order_Registered },
        { "processing", ProcessingLifecycle.Order_Processing },
        { "sendconditionnotmet", ProcessingLifecycle.Order_SendConditionNotMet }
    };

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
        await using var command = _dataSource.CreateCommand(_sqlGetShipmentTrackingInfo);
        command.Parameters.AddWithValue("_alternateid", NpgsqlDbType.Uuid, alternateId);
        command.Parameters.AddWithValue("_creatorname", NpgsqlDbType.Text, creatorName);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
        if (!reader.HasRows)
        {
            return null;
        }

        await reader.ReadAsync(cancellationToken);

        return await CreateNotificationDeliveryManifestAsync(reader, alternateId, cancellationToken);
    }

    /// <summary>
    /// Converts a database SMS notification status to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The SMS status string from the database (from SMSNOTIFICATIONRESULTTYPE type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the SMS notification status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known SMS notification status.</exception>
    /// <remarks>
    /// This method performs case-insensitive mapping between database SMS status values and the 
    /// ProcessingLifecycle enum. It's used when processing delivery information from the database
    /// to ensure consistent status representation throughout the notification system.
    /// </remarks>
    private static ProcessingLifecycle GetSmsLifecycleStage(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (SmsStatusMap.TryGetValue(status.ToLowerInvariant(), out ProcessingLifecycle result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown SMS notification status: '{status}'", nameof(status));
    }

    /// <summary>
    /// Converts a database email notification status to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The email status string from the database (from EMAILNOTIFICATIONRESULTTYPE type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the email notification status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known email notification status.</exception>
    /// <remarks>
    /// This method performs case-insensitive mapping between database email status values and the 
    /// ProcessingLifecycle enum. It's used when processing delivery information from the database
    /// to ensure consistent status representation throughout the notification system.
    /// </remarks>
    private static ProcessingLifecycle GetEmailLifecycleStage(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (EmailStatusMap.TryGetValue(status.ToLowerInvariant(), out ProcessingLifecycle result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown email notification status: '{status}'", nameof(status));
    }

    /// <summary>
    /// Converts a database order processing state to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The order status string from the database (from ORDERPROCESSINGSTATE type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the order status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known order processing state.</exception>
    /// <remarks>
    /// This method performs case-insensitive mapping between database order status values and the 
    /// ProcessingLifecycle enum. It's used when processing delivery information from the database
    /// to ensure consistent status representation throughout the notification system.
    /// </remarks>
    private static ProcessingLifecycle GetOrderLifecycleStage(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (OrderStatusMap.TryGetValue(status.ToLowerInvariant(), out ProcessingLifecycle result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown order processing state: '{status}'", nameof(status));
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

        var lastUpdate = reader.GetDateTime(lastUpdateOrdinal);
        var destination = reader.GetString(destinationOrdinal);

        IDeliveryManifest deliverableEntity = MobileNumbersRegex().IsMatch(destination)
            ? new SmsDeliveryManifest
            {
                LastUpdate = lastUpdate,
                Destination = destination,
                Status = GetSmsLifecycleStage(status),
            }
            : new EmailDeliveryManifest
            {
                LastUpdate = lastUpdate,
                Destination = destination,
                Status = GetEmailLifecycleStage(status),
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

        var reference = await GetSenderReferenceAsync(reader, cancellationToken);

        var (status, lastUpdate) = await GetStatusAsync(reader, cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            await PopulateDeliveryManifestEntitiesAsync(reader, deliverableEntities, cancellationToken);
        }

        return new NotificationDeliveryManifest
        {
            Type = "Notification",
            LastUpdate = lastUpdate,
            ShipmentId = alternateId,
            SendersReference = reference,
            Status = GetOrderLifecycleStage(status),
            Recipients = deliverableEntities.ToImmutableList()
        };
    }

    /// <summary>
    /// A regular expression pattern to detect mobile numbers.
    /// </summary>
    /// <returns>
    /// A <see cref="Regex"/> that matches mobile number formats, accepting:
    /// <list type="bullet">
    ///   <item><description>Optional leading '+' or '00' international prefix</description></item>
    ///   <item><description>One or more digits</description></item>
    /// </list>
    /// </returns>
    [GeneratedRegex(@"^(?:\+|00)?[1-9]\d{1,14}$")]
    private static partial Regex MobileNumbersRegex();
}
