using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of email notification repository logic
/// </summary>
public class EmailNotificationRepository : NotificationRepositoryBase, IEmailNotificationRepository
{
    private const string _emailSourceIdentifier = "EMAIL";
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<EmailNotificationRepository> _logger;

    private const string _insertEmailNotificationSql = "call notifications.insertemailnotification($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"; // (__orderid, _alternateid, _recipientorgno, _recipientnin, _toaddress, _customizedbody, _customizedsubject, _result, _resulttime, _expirytime)
    private const string _getEmailNotificationsSql = "select * from notifications.getemails_statusnew_updatestatus()";
    private const string _getEmailRecipients = "select * from notifications.getemailrecipients_v2($1)"; // (_orderid)
    private const string _updateEmailStatus =
        @"UPDATE notifications.emailnotifications 
        SET result = $1::emailnotificationresulttype, 
            resulttime = now(), 
            operationid = $2
        WHERE alternateid = $3 OR operationid = $2
        RETURNING alternateid;"; // (_result, _operationid, _alternateid)

    private const string _updateStatusAcceptedSql = @"UPDATE notifications.emailnotifications
                                                    SET result = 'Failed'
                                                    WHERE _id IN (
                                                        SELECT _id
                                                        FROM notifications.emailnotifications
                                                        WHERE result = 'Succeeded' AND expirytime < (now() - INTERVAL '48 hours')
                                                        ORDER BY _id ASC
                                                        LIMIT @limit
                                                    )
                                                    RETURNING alternateid;";

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    /// <param name="logger">The logger associated with this implementation of the IEmailNotificationRepository</param>
    public EmailNotificationRepository(NpgsqlDataSource dataSource, ILogger<EmailNotificationRepository> logger)
    : base(logger) // Pass required parameters to the base class constructor
    {
        _dataSource = dataSource;
        _logger = logger;
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
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand pgcom = new(_updateEmailStatus, connection, transaction);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, operationId ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notificationId ?? (object)DBNull.Value);
            var alternateId = await pgcom.ExecuteScalarAsync();

            if (alternateId == null)
            {
                _logger.LogInformation("Status type for email notification {NotificationId} with operation id {OperationId} was updated to {Status}. No alternateId was returned from the updateEmailStatus query.", notificationId, operationId, status);
                await transaction.RollbackAsync();
                return;
            }

            var parseResult = Guid.TryParse(alternateId.ToString(), out Guid emailNotificationAlternateId);

            if (!parseResult)
            {
                throw new InvalidOperationException($"Guid could not be parsed");
            }

            var orderIsSetAsCompleted = await TryCompleteOrderBasedOnNotificationsState(emailNotificationAlternateId, connection, transaction);

            if (orderIsSetAsCompleted)
            {
                var orderStatus = await GetShipmentTracking(emailNotificationAlternateId, connection, transaction);
                if (orderStatus != null)
                {
                    await InsertStatusFeed(orderStatus, connection, transaction);
                }
                else
                {
                    // order status could not be retrieved, we roll back the transaction and throw an exception
                    _logger.LogError("Order status could not be retrieved for the specified alternate ID.");
                    throw new InvalidOperationException("Order status could not be retrieved for the specified alternate ID.");
                }
            }

            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
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
