using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.ApplicationInsights;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of sms repository logic
/// </summary>
public class SmsNotificationRepository : ISmsNotificationRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TelemetryClient? _telemetryClient;

    private const string _insertSmsNotificationSql = "call notifications.insertsmsnotification($1, $2, $3, $4, $5, $6, $7)"; // (__orderid, _alternateid, _recipientid, _mobilenumber, _result, _resulttime, _expirytime)
    private const string _getSmsNotificationsSql = "select * from notifications.getsms_statusnew_updatestatus()";
    private const string _getSmsRecipients = "select * from notifications.getsmsrecipients($1)"; // (_orderid)

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    /// <param name="telemetryClient">Telemetry client</param>
    public SmsNotificationRepository(NpgsqlDataSource dataSource, TelemetryClient? telemetryClient = null)
    {
        _dataSource = dataSource;
        _telemetryClient = telemetryClient;
    }

    /// <inheritdoc/>
    public async Task AddNotification(SmsNotification notification, DateTime expiry)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertSmsNotificationSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.RecipientId ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.RecipientNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, notification.SendResult.ResultTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, expiry);

        await pgcom.ExecuteNonQueryAsync();
        tracker.Track();
    }

    /// <inheritdoc/>
    public async Task<List<SmsRecipient>> GetRecipients(Guid orderId)
    {
        List<SmsRecipient> searchResult = new();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsRecipients);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                searchResult.Add(new SmsRecipient()
                {
                    RecipientId = reader.GetValue<string>("recipientid"),
                    MobileNumber = reader.GetValue<string>("mobilenumber")
                });
            }
        }

        tracker.Track();
        return searchResult;
    }

    /// <inheritdoc/>
    public async Task<List<Sms>> GetNewNotifications()
    {
        List<Sms> searchResult = new();
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsNotificationsSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var sms = new Sms(
                    reader.GetValue<Guid>("alternateid"),
                    reader.GetValue<string>("sendernumber"),
                    reader.GetValue<string>("mobilenumber"),
                    reader.GetValue<string>("body"));

                searchResult.Add(sms);
            }
        }

        tracker.Track();
        return searchResult;
    }
}
