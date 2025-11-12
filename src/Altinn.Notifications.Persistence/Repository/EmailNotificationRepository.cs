using System.Data;

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
    private const string _getEmailNotificationsBatchSql = "SELECT * FROM notifications.claim_email_batch(@batchsize)";
    private const string _getEmailRecipients = "select * from notifications.getemailrecipients_v2($1)"; // (_orderid)
    private const string _updateEmailNotificationSql = "select * from notifications.updateemailnotification_v2($1, $2, $3)"; // (_result, _operationid, _alternateid)

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
    public async Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null)
    {
        var hasNotificationId = notificationId is Guid id && id != Guid.Empty;
        var hasOperationId = !string.IsNullOrWhiteSpace(operationId);
        if (!hasOperationId && !hasNotificationId)
        {
            throw new ArgumentException("The provided Email identifier is invalid.");
        }

        await ExecuteUpdateWithTransactionAsync(
            _updateEmailNotificationSql,
            pgcom =>
            {
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(operationId) ? DBNull.Value : operationId);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, (notificationId == null || notificationId == Guid.Empty) ? DBNull.Value : notificationId);
            },
            hasOperationId,
            operationId,
            notificationId,
            NotificationChannel.Email,
            hasNotificationId ? SendStatusIdentifierType.NotificationId : SendStatusIdentifierType.OperationId);
    }

    /// <inheritdoc/>
    public async Task<List<Email>> GetNewNotificationsAsync(int publishBatchSize, CancellationToken cancellationToken)
    {
        List<Email> searchResult = [];
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEmailNotificationsBatchSql);
        pgcom.Parameters.AddWithValue("batchsize", NpgsqlDbType.Integer, publishBatchSize);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                EmailContentType emailContentType = Enum.Parse<EmailContentType>(reader.GetValue<string>("contenttype"));

                var email = new Email(
                    await reader.GetFieldValueAsync<Guid>("alternateid", cancellationToken),
                    await reader.GetFieldValueAsync<string>("subject", cancellationToken),
                    await reader.GetFieldValueAsync<string>("body", cancellationToken),
                    await reader.GetFieldValueAsync<string>("fromaddress", cancellationToken),
                    await reader.GetFieldValueAsync<string>("toaddress", cancellationToken),
                    emailContentType);

                searchResult.Add(email);
            }
        }

        return searchResult;
    }
}
