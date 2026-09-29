using System.Data;
using System.Text.Json;

using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Files;
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

    private const string _insertEmailNotificationSql = "call notifications.insertemailnotification_v2($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)"; // $1=orderid, $2=alternateid, $3=recipientorgno, $4=recipientnin, $5=toaddress, $6=customizedbody, $7=customizedsubject, $8=result, $9=resulttime, $10=expirytime, $11=total_attachment_size_bytes
    private const string _getEmailNotificationSql = "SELECT * FROM notifications.claim_email()";
    private const string _getComposedEmailNotificationSql = "SELECT * FROM notifications.claim_composed_email()";
    private const string _getEmailRecipients = "select * from notifications.getemailrecipients_v2($1)"; // (_orderid)
    private const string _updateEmailNotificationSql = "select * from notifications.updateemailnotification_v4($1, $2, $3, $4, $5)"; // $1=result, $2=operationid, $3=alternateid, $4=deliveryreport, $5=total_attachment_size_bytes

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
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, 0L);

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
    /// <exception cref="InvalidNotificationIdentifierException">Thrown when both the notification ID and operation ID are null or empty.</exception>
    public async Task UpdateSendStatus(Guid? notificationId, EmailNotificationResultType status, string? operationId = null, string? deliveryReport = null, long? totalAttachmentSizeBytes = null)
    {
        var hasNotificationId = notificationId is Guid id && id != Guid.Empty;
        var hasOperationId = !string.IsNullOrWhiteSpace(operationId);
        if (!hasOperationId && !hasNotificationId)
        {
            throw new InvalidNotificationIdentifierException("The provided Email identifier is invalid.");
        }

        await ExecuteUpdateWithTransactionAsync(
            _updateEmailNotificationSql,
            pgcom =>
            {
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, status.ToString());
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(operationId) ? DBNull.Value : operationId);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, (notificationId == null || notificationId == Guid.Empty) ? DBNull.Value : notificationId);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Jsonb, string.IsNullOrWhiteSpace(deliveryReport) ? DBNull.Value : deliveryReport);
                pgcom.Parameters.AddWithValue(NpgsqlDbType.Bigint, (object?)totalAttachmentSizeBytes ?? DBNull.Value);
            },
            NotificationChannel.Email,
            notificationId,
            operationId,
            statusIsAcceptedOrSucceeded: status == EmailNotificationResultType.Succeeded,
            SendStatusIdentifierType.OperationId);
    }

    /// <inheritdoc/>
    public async Task<Email?> GetNewNotificationAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEmailNotificationSql);
        return await ReadNewNotificationAsync(pgcom, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Email?> GetNewNotificationAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = new(_getEmailNotificationSql, unitOfWork.Connection, unitOfWork.Transaction);
        return await ReadNewNotificationAsync(pgcom, cancellationToken);
    }

    private static async Task<Email?> ReadNewNotificationAsync(NpgsqlCommand pgcom, CancellationToken cancellationToken)
    {
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        EmailContentType emailContentType = Enum.Parse<EmailContentType>(reader.GetValue<string>("contenttype"));

        return new Email(
            await reader.GetFieldValueAsync<Guid>("alternateid", cancellationToken),
            await reader.GetFieldValueAsync<string>("subject", cancellationToken),
            await reader.GetFieldValueAsync<string>("body", cancellationToken),
            await reader.GetFieldValueAsync<string>("fromaddress", cancellationToken),
            await reader.GetFieldValueAsync<string>("toaddress", cancellationToken),
            emailContentType);
    }

    /// <inheritdoc/>
    public async Task<ComposedEmail?> GetNewComposedNotificationAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getComposedEmailNotificationSql);
        return await ReadNewComposedNotificationAsync(pgcom, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ComposedEmail?> GetNewComposedNotificationAsync(UnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand pgcom = new(_getComposedEmailNotificationSql, unitOfWork.Connection, unitOfWork.Transaction);
        return await ReadNewComposedNotificationAsync(pgcom, cancellationToken);
    }

    private static async Task<ComposedEmail?> ReadNewComposedNotificationAsync(NpgsqlCommand pgcom, CancellationToken cancellationToken)
    {
        await using NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        EmailContentType contentType = Enum.Parse<EmailContentType>(await reader.GetFieldValueAsync<string>("contenttype", cancellationToken));

        var attachmentsJson = await reader.GetFieldValueAsync<string>("attachments", cancellationToken);
        var attachments = JsonSerializer.Deserialize<List<SasFileReference>>(attachmentsJson, JsonSerializerOptionsProvider.Options)
            ?? [];

        return new ComposedEmail(
            await reader.GetFieldValueAsync<Guid>("alternateid", cancellationToken),
            await reader.GetFieldValueAsync<string>("subject", cancellationToken),
            await reader.GetFieldValueAsync<string>("body", cancellationToken),
            await reader.GetFieldValueAsync<string>("fromaddress", cancellationToken),
            await reader.GetFieldValueAsync<string>("toaddress", cancellationToken),
            contentType,
            attachments);
    }
}
