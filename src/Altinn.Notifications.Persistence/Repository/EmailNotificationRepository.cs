using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Persistence.Extensions;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public class EmailNotificationRepository : IEmailNotificationsRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _insertEmailNotificationSql = "call notifications.insertemailnotification($1, $2, $3, $4, $5, $6, $7)"; // (__orderid, _alternateid, _recipientid, _toaddress, _result, _resulttime, _expirytime)
    private readonly string _getEmailNotificationsSql = "select * from notifications.getemails_statusnew_updatestatus()";

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public EmailNotificationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public async Task AddEmailNotification(EmailNotification emailNotification, DateTime expiry)
    {
        try
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEmailNotificationSql);

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, new Guid(emailNotification.OrderId));
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, new Guid(emailNotification.Id));
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailNotification.RecipientId ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailNotification.ToAddress);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, emailNotification.SendResult.Result.ToString());
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, emailNotification.SendResult.ResultTime);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, expiry);

            await pgcom.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            throw;
        }
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
                Enum.TryParse(reader.GetValue<string>("contenttype"), out EmailContentType emailContentType);

                Email email = new Email(
                    reader.GetValue<int>("id").ToString(),
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
}