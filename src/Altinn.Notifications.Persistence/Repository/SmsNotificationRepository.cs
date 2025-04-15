using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implements the repository logic for SMS notifications.
/// </summary>
public class SmsNotificationRepository : ISmsNotificationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _getNewSmsNoticationsSql = "select * from notifications.getsms_statusnew_updatestatus($1)"; // (_sendingtimepolicy) this is now calling an overload function with the sending time policy parameter
    private const string _getSmsNotificationRecipientsSql = "select * from notifications.getsmsrecipients_v2($1)"; // (_orderid)
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
            WHERE gatewayreference = $2"; // (_result, _gatewayreference)

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The Npgsql data source.</param>
    public SmsNotificationRepository(NpgsqlDataSource dataSource)
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
    public async Task<List<Sms>> GetNewNotifications(SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime)
    {
        List<Sms> readyToSendSMS = [];
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getNewSmsNoticationsSql);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, (int)sendingTimePolicy);
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var sms = new Sms(
                    reader.GetValue<Guid>("alternateid"),
                    reader.GetValue<string>("sendernumber"),
                    reader.GetValue<string>("mobilenumber"),
                    reader.GetValue<string>("body"));

                readyToSendSMS.Add(sms);
            }
        }

        return readyToSendSMS;
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
    /// <param name="id">The unique identifier of the SMS notification.</param>
    /// <param name="result">The result status of sending the SMS notification.</param>
    /// <param name="gatewayReference">The gateway reference (optional). If provided, it will be updated in the database.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided SMS identifier is invalid.</exception>
    private async Task UpdateSendStatusById(Guid id, SmsNotificationResultType result, string? gatewayReference = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The provided SMS identifier is invalid.");
        }

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_updateSmsNotificationBasedOnIdentifierSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(gatewayReference) ? DBNull.Value : gatewayReference);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, id);

        await pgcom.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the send status of an SMS notification based on its gateway reference.
    /// </summary>
    /// <param name="gatewayReference">The gateway reference of the SMS notification.</param>
    /// <param name="result">The result status of sending the SMS notification.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided gateway reference is invalid.</exception>
    private async Task UpdateSendStatusByGatewayReference(string gatewayReference, SmsNotificationResultType result)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference))
        {
            throw new ArgumentException("The provided gateway reference is invalid.");
        }

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_updateSmsNotificationBasedOnGatewayReferenceSql);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, gatewayReference);

        await pgcom.ExecuteNonQueryAsync();
    }
}
