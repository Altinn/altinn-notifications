using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implements the repository logic for SMS notifications.
/// </summary>
public class SmsNotificationRepository : NotificationRepositoryBase, ISmsNotificationRepository
{
    private const string _smsSourceIdentifier = "SMS";
    private readonly NpgsqlDataSource _dataSource;

    private const string _getSmsNotificationRecipientsSql = "select * from notifications.getsmsrecipients_v2($1)"; // (_orderid)
    private const string _claimAnytimeSmsBatchSql = "select * from notifications.claim_anytime_sms_batch(_batchsize := @batchsize)";
    private const string _claimDaytimeSmsBatchSql = "select * from notifications.claim_daytime_sms_batch(_batchsize := @batchsize)";
    private const string _insertNewSmsNotificationSql = "call notifications.insertsmsnotification($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"; // (__orderid, _alternateid, _recipientorgno, _recipientnin, _mobilenumber, _customizedbody, _result, _smscount, _resulttime, _expirytime)

    private const string _updateSmsNotificationBasedOnIdentifierSql =
        @"UPDATE notifications.smsnotifications 
            SET result = $1::smsnotificationresulttype, 
                resulttime = now(), 
                gatewayreference = $2 
            WHERE alternateid = $3"; // (_result, _gatewayreference, _alternateid)

    private const string _updateSmsNotificationBasedOnGatewayReferenceSql =
        @"UPDATE notifications.smsnotifications 
            SET result = $1::smsnotificationresulttype, 
                resulttime = now() 
            WHERE gatewayreference = $2
            RETURNING alternateid"; // (_result, _gatewayreference)

    /// <inheritdoc/>
    protected override string SourceIdentifier => _smsSourceIdentifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    /// <param name="logger">The logger associated with this implementation of the ISmsNotificationRepository</param>
    /// <param name="config">The notification configuration</param>
    public SmsNotificationRepository(NpgsqlDataSource dataSource, ILogger<SmsNotificationRepository> logger, IOptions<NotificationConfig> config) : base(dataSource, logger, config)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task AddNotification(SmsNotification notification, DateTime expiry, int count)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertNewSmsNotificationSql);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.OrganizationNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.NationalIdentityNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.MobileNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.CustomizedBody ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, count);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, notification.SendResult.ResultTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, expiry);

        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task<List<SmsRecipient>> GetRecipients(Guid orderId)
    {
        List<SmsRecipient> recipients = [];

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsNotificationRecipientsSql);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                recipients.Add(new SmsRecipient()
                {
                    MobileNumber = reader.GetValue<string>("mobilenumber"),
                    OrganizationNumber = reader.GetValue<string?>("recipientorgno"),
                    NationalIdentityNumber = reader.GetValue<string?>("recipientnin")
                });
            }
        }

        return recipients;
    }

    /// <inheritdoc/>   
    public async Task<List<Sms>> GetNewNotifications(int publishBatchSize, CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime)
    {
        if (publishBatchSize <= 0)
        {
            return [];
        }

        var claimSmsBatchForSending = sendingTimePolicy switch
        {
            SendingTimePolicy.Anytime => _claimAnytimeSmsBatchSql,
            _ => _claimDaytimeSmsBatchSql,
        };
       
        await using var command = _dataSource.CreateCommand(claimSmsBatchForSending);

        command.Parameters.AddWithValue("@batchsize", NpgsqlDbType.Integer, publishBatchSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<Sms>(publishBatchSize);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetValue<Guid>("alternateid"),
                reader.GetValue<string>("sendernumber"),
                reader.GetValue<string>("mobilenumber"),
                reader.GetValue<string>("body")));
        }

        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Throws if the provided SMS identifier is invalid.</exception>
    public async Task UpdateSendStatus(Guid? notificationId, SmsNotificationResultType result, string? gatewayReference = null)
    {
        if ((!notificationId.HasValue || notificationId.Value == Guid.Empty) && string.IsNullOrWhiteSpace(gatewayReference))
        {
            throw new ArgumentException("The provided SMS identifier is invalid.");
        }

        if (notificationId.HasValue && notificationId.Value != Guid.Empty)
        {
            await UpdateSendStatusById(notificationId.Value, result, gatewayReference);
        }
        else if (!string.IsNullOrWhiteSpace(gatewayReference))
        {
            await UpdateSendStatusByGatewayReference(gatewayReference, result);
        }
    }

    /// <summary>
    /// Updates the send status of an SMS notification based on its identifier and sets the gateway reference.
    /// </summary>
    /// <param name="smsNotificationAlternateId">The unique identifier of the SMS notification.</param>
    /// <param name="result">The result status of sending the SMS notification.</param>
    /// <param name="gatewayReference">The gateway reference (optional). If provided, it will be updated in the database.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided SMS identifier is invalid.</exception>
    private async Task UpdateSendStatusById(Guid smsNotificationAlternateId, SmsNotificationResultType result, string? gatewayReference = null)
    {
        if (smsNotificationAlternateId == Guid.Empty)
        {
            throw new ArgumentException("The provided SMS identifier is invalid.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand pgcom = new(_updateSmsNotificationBasedOnIdentifierSql, connection, transaction);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(gatewayReference) ? DBNull.Value : gatewayReference);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, smsNotificationAlternateId);

            await pgcom.ExecuteNonQueryAsync();

            var orderIsSetAsCompleted = await TryCompleteOrderBasedOnNotificationsState(smsNotificationAlternateId, connection, transaction);

            if (orderIsSetAsCompleted)
            {
                await InsertOrderStatusCompletedOrder(connection, transaction, smsNotificationAlternateId);
            }

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Updates the send status of an SMS notification based on its gateway reference.
    /// </summary>
    /// <param name="gatewayReference">The gateway reference of the SMS notification.</param>
    /// <param name="result">The result status of sending the SMS notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="SendStatusUpdateException">Thrown when the notification cannot be found or the update operation fails.</exception>
    private async Task UpdateSendStatusByGatewayReference(string gatewayReference, SmsNotificationResultType result)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference))
        {
            throw new ArgumentException("The provided gateway reference is invalid.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using var command = new NpgsqlCommand(_updateSmsNotificationBasedOnGatewayReferenceSql, connection, transaction);
            command.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
            command.Parameters.AddWithValue(NpgsqlDbType.Text, gatewayReference);

            // Get the alternate ID from the database
            var alternateId = await command.ExecuteScalarAsync();
            if (alternateId == null)
            {
                throw new SendStatusUpdateException(NotificationChannel.Sms, gatewayReference, SendStatusIdentifierType.GatewayReference);
            }

            if (!Guid.TryParse(alternateId.ToString(), out Guid alternateIdGuid))
            {
                throw new SendStatusUpdateException(NotificationChannel.Sms, gatewayReference, SendStatusIdentifierType.GatewayReference);
            }

            bool orderIsCompleted = await TryCompleteOrderBasedOnNotificationsState(alternateIdGuid, connection, transaction);
            if (orderIsCompleted)
            {
                await InsertOrderStatusCompletedOrder(connection, transaction, alternateIdGuid);
            }

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
