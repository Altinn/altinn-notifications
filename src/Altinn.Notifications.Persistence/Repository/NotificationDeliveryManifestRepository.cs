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

    private const string _sqlGetNotificationDeliveryInfo = "SELECT * FROM notifications.get_notification_delivery_info($1, $2)"; // (_alternateid, _creatorname)

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
        await using var command = _dataSource.CreateCommand(_sqlGetNotificationDeliveryInfo);
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
    /// Maps a database SMS notification result string to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The SMS status string from the database (from SMSNOTIFICATIONRESULTTYPE enum type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the SMS notification status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known SMS notification status.</exception>
    private static ProcessingLifecycle GetSmsProcessingStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "new" => ProcessingLifecycle.SMS_New,
            "failed" => ProcessingLifecycle.SMS_Failed,
            "sending" => ProcessingLifecycle.SMS_Sending,
            "accepted" => ProcessingLifecycle.SMS_Accepted,
            "delivered" => ProcessingLifecycle.SMS_Delivered,
            "failed_deleted" => ProcessingLifecycle.SMS_Failed_Deleted,
            "failed_expired" => ProcessingLifecycle.SMS_Failed_Expired,
            "failed_rejected" => ProcessingLifecycle.SMS_Failed_Rejected,
            "failed_undelivered" => ProcessingLifecycle.SMS_Failed_Undelivered,
            "failed_barredreceiver" => ProcessingLifecycle.SMS_Failed_BarredReceiver,
            "failed_invalidreceiver" => ProcessingLifecycle.SMS_Failed_InvalidRecipient,
            "failed_invalidrecipient" => ProcessingLifecycle.SMS_Failed_InvalidRecipient,
            "failed_recipientreserved" => ProcessingLifecycle.SMS_Failed_RecipientReserved,
            "failed_recipientnotidentified" => ProcessingLifecycle.SMS_Failed_RecipientNotIdentified,
            _ => throw new ArgumentException($"Unknown SMS notification status: {status}", nameof(status))
        };
    }

    /// <summary>
    /// Maps a database email notification result string to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The email status string from the database (from emailnotificationresulttype type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the email notification status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known email notification status.</exception>
    private static ProcessingLifecycle GetEmailProcessingStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "new" => ProcessingLifecycle.Email_New,
            "failed" => ProcessingLifecycle.Email_Failed,
            "sending" => ProcessingLifecycle.Email_Sending,
            "succeeded" => ProcessingLifecycle.Email_Succeeded,
            "delivered" => ProcessingLifecycle.Email_Delivered,
            "failed_bounced" => ProcessingLifecycle.Email_Failed_Bounced,
            "failed_quarantined" => ProcessingLifecycle.Email_Failed_Quarantined,
            "failed_filteredspam" => ProcessingLifecycle.Email_Failed_FilteredSpam,
            "failed_transienterror" => ProcessingLifecycle.Email_Failed_TransientError,
            "failed_invalidemailformat" => ProcessingLifecycle.Email_Failed_InvalidFormat,
            "failed_recipientreserved" => ProcessingLifecycle.Email_Failed_RecipientReserved,
            "failed_supressedrecipient" => ProcessingLifecycle.Email_Failed_SupressedRecipient,
            "failed_recipientnotidentified" => ProcessingLifecycle.Email_Failed_RecipientNotIdentified,
            _ => throw new ArgumentException($"Unknown email notification status: {status}", nameof(status))
        };
    }

    /// <summary>
    /// Maps a database order processing state string to its corresponding <see cref="ProcessingLifecycle"/> enum value.
    /// </summary>
    /// <param name="status">The order status string from the database (from orderprocessingstate type).</param>
    /// <returns>The corresponding <see cref="ProcessingLifecycle"/> enum value for the order status.</returns>
    /// <exception cref="ArgumentException">Thrown when the status string doesn't match any known order status.</exception>
    private static ProcessingLifecycle GetOrderProcessingStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "cancelled" => ProcessingLifecycle.Order_Cancelled,
            "completed" => ProcessingLifecycle.Order_Completed,
            "registered" => ProcessingLifecycle.Order_Registered,
            "processing" => ProcessingLifecycle.Order_Processing,
            "sendconditionnotmet" => ProcessingLifecycle.Order_SendConditionNotMet,
            _ => throw new ArgumentException($"Unknown order status: {status}", nameof(status))
        };
    }

    /// <summary>
    /// Extracts the sender's reference for the notification order.
    /// </summary>
    /// <param name="reader">Database reader positioned at a notification order data row.</param>
    /// <param name="cancellationToken">Token for canceling the asynchronous operation.</param>
    /// <returns>
    /// The sender's reference as a string, or <see langword="null"/> if no reference is present.
    /// </returns>
    private static async Task<string?> ExtractSenderReferenceAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var referenceOrdinal = reader.GetOrdinal(_referenceColumnName);
        return await reader.IsDBNullAsync(referenceOrdinal, cancellationToken) ? null : reader.GetString(referenceOrdinal);
    }

    /// <summary>
    /// Extracts the status and last update timestamp for the notification order.
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
    private static async Task<(string Status, DateTime LastUpdate)> ExtractStatusAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
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
    private static async Task CreateDeliveryManifestEntityAsync(NpgsqlDataReader reader, List<IDeliveryManifest> deliveryManifestEntities, CancellationToken cancellationToken)
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
                Status = GetSmsProcessingStatus(status),
            }
            : new EmailDeliveryManifest
            {
                LastUpdate = lastUpdate,
                Destination = destination,
                Status = GetEmailProcessingStatus(status),
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

        var reference = await ExtractSenderReferenceAsync(reader, cancellationToken);

        var (status, lastUpdate) = await ExtractStatusAsync(reader, cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            await CreateDeliveryManifestEntityAsync(reader, deliverableEntities, cancellationToken);
        }

        return new NotificationDeliveryManifest
        {
            Type = "Notification",
            LastUpdate = lastUpdate,
            ShipmentId = alternateId,
            SendersReference = reference,
            Status = GetOrderProcessingStatus(status),
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
    [GeneratedRegex(@"^(?:\+|00)?[1-9]\d{7,14}$")]
    private static partial Regex MobileNumbersRegex();
}
