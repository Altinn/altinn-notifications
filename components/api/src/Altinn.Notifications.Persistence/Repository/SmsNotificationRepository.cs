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
    private readonly ILogger<SmsNotificationRepository> _logger;

    private const string _getSmsNotificationRecipientsSql = "select * from notifications.getsmsrecipients_v2($1)"; // (_orderid)
    private const string _claimAnytimeSmsBatchSql = "select * from notifications.claim_anytime_sms_batch_v2(_batchsize := @batchsize)";
    private const string _claimDaytimeSmsBatchSql = "select * from notifications.claim_daytime_sms_batch_v2(_batchsize := @batchsize)";
    private const string _insertNewSmsNotificationSql = "call notifications.insertsmsnotification_v2($1, $2, $3, $4, $5, $6, $7, $8, $9)"; // (_orderid, _alternateid, _recipientorgno, _recipientnin, _mobilenumber, _customizedbody, _result, _resulttime, _expirytime)
    private const string _persistSubstitutedSenderSql = "update notifications.smsnotifications set substitutedsender = $2 where alternateid = $1"; // (_alternateid, _substitutedsender)
    private const string _updateSmsNotificationSql = "select * from notifications.updatesmsnotification_v3($1, $2, $3, $4)"; // (_result, _gatewayreference, _alternateid, _deliveryreport)

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
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AddNotification(SmsNotification notification, DateTime expiry)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertNewSmsNotificationSql);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.OrganizationNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.NationalIdentityNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.MobileNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.CustomizedBody ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
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
                reader.GetValue<string>("body"),
                reader.GetValue<string>("creatorname")));
        }

        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidNotificationIdentifierException">Thrown when both the notification ID and gateway reference are null or empty.</exception>
    public async Task UpdateSendStatus(Guid? notificationId, SmsNotificationResultType result, string? gatewayReference = null, string? deliveryReport = null)
    {
        var hasNotificationId = notificationId is Guid id && id != Guid.Empty;
        var hasGatewayReference = !string.IsNullOrWhiteSpace(gatewayReference);
        if (!hasGatewayReference && !hasNotificationId)
        {
            throw new InvalidNotificationIdentifierException("The provided SMS identifier is invalid.");
        }

        await ExecuteUpdateWithTransactionAsync(
            _updateSmsNotificationSql,
            pgcom =>
            {
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(gatewayReference) ? DBNull.Value : gatewayReference);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, (notificationId == null || notificationId == Guid.Empty) ? DBNull.Value : notificationId);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, string.IsNullOrWhiteSpace(deliveryReport) ? DBNull.Value : deliveryReport);
            },
            NotificationChannel.Sms,
            notificationId,
            gatewayReference,
            statusIsAcceptedOrSucceeded: result == SmsNotificationResultType.Accepted,
            SendStatusIdentifierType.GatewayReference);
    }
    /// <inheritdoc/>
    public async Task PersistSubstitutedSender(Guid notificationId, string sender)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sender);

            if (notificationId == Guid.Empty)
            {
                throw new InvalidNotificationIdentifierException("The provided SMS identifier is invalid.");
            }

            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_persistSubstitutedSenderSql);

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notificationId);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, sender);

            var rowsAffected = await pgcom.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                _logger.LogWarning("No rows were updated when persisting substituted sender for SMS notification with ID {NotificationId}. This may indicate that the notification does not exist.", notificationId);
            }
        }
        catch (Exception e)
        {
            // we don't want to throw an exception here, as it will cause the entire batch to fail. Instead, we log the error and continue processing the rest of the batch.
            _logger.LogError(e, "Failed to persist substituted sender for SMS notification with ID {NotificationId}.", notificationId);
            throw new InvalidOperationException(
                "Failed to persist substituted sender.",
                e);
        }
    }
}
