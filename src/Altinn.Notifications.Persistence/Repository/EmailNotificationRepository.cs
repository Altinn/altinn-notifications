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
/// Implementation of email notification repository logic
/// </summary>
public class EmailNotificationRepository : NotificationRepositoryBase, IEmailNotificationRepository
{
    private const string _emailSourceIdentifier = "EMAIL";
    private readonly NpgsqlDataSource _dataSource;

    private const string _insertEmailNotificationSql = "call notifications.insertemailnotification($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"; // (__orderid, _alternateid, _recipientorgno, _recipientnin, _toaddress, _customizedbody, _customizedsubject, _result, _resulttime, _expirytime)
    private const string _getEmailNotificationsSql = "select * from notifications.getemails_statusnew_updatestatus()";
    private const string _getEmailRecipients = "select * from notifications.getemailrecipients_v2($1)"; // (_orderid)
    private const string _updateEmailNotificationSql = "select * from notifications.updateemailnotification($1, $2, $3)"; // (_result, _operationid, _alternateid)

    /// <inheritdoc/>
    protected override string SourceIdentifier => _emailSourceIdentifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    /// <param name="logger">The logger associated with this implementation of the IEmailNotificationRepository</param>
    /// <param name="config">The notification configuration</param>
    public EmailNotificationRepository(NpgsqlDataSource dataSource, ILogger<EmailNotificationRepository> logger, IOptions<NotificationConfig> config)
    : base(dataSource, logger, config) // Pass required parameters to the base class constructor
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
        var hasOperationId = !string.IsNullOrWhiteSpace(operationId);
        var hasNotificationId = notificationId is Guid id && id != Guid.Empty;
        if (!hasOperationId && !hasNotificationId)
        {
            throw new ArgumentException("The provided Email identifier is invalid.");
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using NpgsqlCommand pgcom = new(_updateEmailNotificationSql, connection, transaction);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(operationId) ? DBNull.Value : operationId);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, (notificationId == null || notificationId == Guid.Empty) ? DBNull.Value : notificationId);

            // Execute and read the result table
            Guid? resultAlternateId = null;
            bool wasUpdated = false;
            bool isExpired = false;

            await using (var reader = await pgcom.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    resultAlternateId = await reader.IsDBNullAsync(0) ? null : reader.GetGuid(0);
                    wasUpdated = reader.GetBoolean(1);
                    isExpired = reader.GetBoolean(2);
                }
            }

            // Handle not found or expired cases
            HandleUpdateResult(resultAlternateId, isExpired, hasOperationId, operationId, notificationId, NotificationChannel.Email);

            // Proceed with order completion logic if update was successful
            if (wasUpdated && resultAlternateId.HasValue)
            {
                var orderIsSetAsCompleted = await TryCompleteOrderBasedOnNotificationsState(resultAlternateId.Value, connection, transaction);

                if (orderIsSetAsCompleted)
                {
                    await InsertOrderStatusCompletedOrder(connection, transaction, resultAlternateId.Value);
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

    /// <summary>
    /// Validates the result of an update operation and throws appropriate exceptions if the update failed.
    /// </summary>
    private static void HandleUpdateResult(
        Guid? resultAlternateId,
        bool isExpired,
        bool hasOperationId,
        string? operationId,
        Guid? notificationId,
        NotificationChannel channel)
    {
        // Notification not found in database
        if (resultAlternateId == null)
        {
            var identifier = hasOperationId ? operationId! : notificationId!.Value.ToString();
            var identifierType = hasOperationId ? SendStatusIdentifierType.OperationId : SendStatusIdentifierType.NotificationId;
            throw new NotificationNotFoundException(channel, identifier, identifierType);
        }

        // Notification has passed its expiry time (TTL) - update was blocked
        if (isExpired)
        {
            var identifier = hasOperationId ? operationId! : notificationId!.Value.ToString();
            var identifierType = hasOperationId ? SendStatusIdentifierType.OperationId : SendStatusIdentifierType.NotificationId;
            throw new NotificationExpiredException(channel, identifier, identifierType);
        }
    }
}
