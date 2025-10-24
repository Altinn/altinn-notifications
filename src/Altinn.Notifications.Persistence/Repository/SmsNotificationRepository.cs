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

    private const string _updateSmsNotificationSql = "select * from notifications.updatesmsnotification($1, $2, $3)"; // (_result, _gatewayreference, _alternateid)

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
        var hasGatewayReference = !string.IsNullOrWhiteSpace(gatewayReference);
        var hasNotificationId = notificationId is Guid id && id != Guid.Empty;
        if (!hasGatewayReference && !hasNotificationId)
        {
            throw new ArgumentException("The provided SMS identifier is invalid.");
        }

        await ExecuteUpdateWithTransactionAsync(
            _updateSmsNotificationSql,
            pgcom =>
            {
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(gatewayReference) ? DBNull.Value : gatewayReference);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, (notificationId == null || notificationId == Guid.Empty) ? DBNull.Value : notificationId);
            },
            hasGatewayReference,
            gatewayReference,
            notificationId,
            NotificationChannel.Sms,
            SendStatusIdentifierType.GatewayReference);
    }
}
