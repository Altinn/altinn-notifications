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
/// Implementation of email notification repository logic
/// </summary>
public class EmailNotificationRepository : IEmailNotificationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _insertEmailNotificationSql = "call notifications.insertemailnotification($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"; // (__orderid, _alternateid, _recipientorgno, _recipientnin, _toaddress, _customizedbody, _customizedsubject, _result, _resulttime, _expirytime)
    private const string _getEmailNotificationsSql = "select * from notifications.getemails_statusnew_updatestatus()";
    private const string _getEmailRecipients = "select * from notifications.getemailrecipients_v2($1)"; // (_orderid)
    private const string _updateEmailStatus =
        @"UPDATE notifications.emailnotifications 
        SET result = $1::emailnotificationresulttype, 
            resulttime = now(), 
            operationid = $2
        WHERE alternateid = $3 OR operationid = $2;";    // (_result, _operationid, _alternateid)

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public EmailNotificationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task AddNotification(EmailNotification notification, DateTime expiry)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEmailNotificationSql);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.OrganizationNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.NationalIdentityNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.ToAddress);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.CustomizedBody ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.CustomizedSubject ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, notification.SendResult.ResultTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, expiry);

        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task<List<Email>> GetNewNotifications()
    {
        List<Email> searchResult = new();
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEmailNotificationsSql);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                EmailContentType emailContentType = Enum.Parse<EmailContentType>(reader.GetValue<string>("contenttype"));

                var email = new Email(
                    reader.GetValue<Guid>("alternateid"),
                    reader.GetValue<string>("subject"),
                    reader.GetValue<string>("body"),
                    reader.GetValue<string>("fromaddress"),
                    reader.GetValue<string>("toaddress"),
                    emailContentType);

                searchResult.Add(email);
            }
        }

        return searchResult;
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_updateEmailStatus);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, operationId ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notificationId ?? (object)DBNull.Value);
        await pgcom.ExecuteNonQueryAsync();
    }

    /// <inheritdoc/>
    public async Task<List<EmailRecipient>> GetRecipients(Guid orderId)
    {
        List<EmailRecipient> searchResult = [];

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEmailRecipients);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                searchResult.Add(new EmailRecipient()
                {
                    ToAddress = reader.GetValue<string>("toaddress"),
                    OrganizationNumber = reader.GetValue<string?>("recipientorgno"),
                    NationalIdentityNumber = reader.GetValue<string?>("recipientnin"),
                });
            }
        }

        return searchResult;
    }
}
