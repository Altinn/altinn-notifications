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
    public async Task AddNotification(EmailNotification notification, DateTime expiry)
    {
        try
        {
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertEmailNotificationSql);

            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, new Guid(notification.OrderId));
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, new Guid(notification.Id));
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.RecipientId ?? (object)DBNull.Value);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.ToAddress);
            pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, notification.SendResult.ResultTime);
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
                EmailContentType emailContentType = (EmailContentType)Enum.Parse(typeof(EmailContentType), reader.GetValue<string>("contenttype"));
                
                var email = new Email(
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