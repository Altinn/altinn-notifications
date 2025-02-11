using Altinn.Notifications.Core.Enums;
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
/// Implements the repository logic for SMS notifications.
/// </summary>
public class SmsNotificationRepository : ISmsNotificationRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TelemetryClient? _telemetryClient;

    private const string _insertSmsNotificationSql = "call notifications.insertsmsnotification($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)"; // (__orderid, _alternateid, _recipientorgno, _recipientnin, _mobilenumber, _customizedbody, _result, _smscount, _resulttime, _expirytime)
    private const string _getSmsNotificationsSql = "select * from notifications.getsms_statusnew_updatestatus()";
    private const string _getSmsRecipients = "select * from notifications.getsmsrecipients_v2($1)"; // (_orderid)

    private const string _updateSmsNotificationStatus =
        @"UPDATE notifications.smsnotifications 
            SET result = $1::smsnotificationresulttype, 
                resulttime = now(), 
                gatewayreference = $2
            WHERE alternateid = $3"; // (_result, _gatewayreference, _alternateid)

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The Npgsql data source.</param>
    /// <param name="telemetryClient">The telemetry client.</param>
    public SmsNotificationRepository(NpgsqlDataSource dataSource, TelemetryClient? telemetryClient = null)
    {
        _dataSource = dataSource;
        _telemetryClient = telemetryClient;
    }

    /// <inheritdoc/>
    public async Task AddNotification(SmsNotification notification, DateTime expiry, int smsCount)
    {
        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_insertSmsNotificationSql);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);

        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.OrderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, notification.Id);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, (object)DBNull.Value); // recipientorgno
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.NationalIdentityNumber ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.MobileNumber);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.Recipient.CustomizedBody ?? (object)DBNull.Value);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, notification.SendResult.Result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Integer, smsCount);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, notification.SendResult.ResultTime);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, expiry);

        await pgcom.ExecuteNonQueryAsync();
        tracker.Track();
    }

    /// <inheritdoc/>
    public async Task<List<SmsRecipient>> GetRecipients(Guid orderId)
    {
        List<SmsRecipient> searchResult = [];

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsRecipients);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                searchResult.Add(new SmsRecipient()
                {
                    OrganizationNumber = reader.GetValue<string?>("recipientorgno"),
                    NationalIdentityNumber = reader.GetValue<string?>("recipientnin"),
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
        List<Sms> searchResult = [];
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

    /// <inheritdoc/>
    public async Task UpdateSendStatus(Guid id, SmsNotificationResultType result, string? gatewayReference = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The provided SMS order identifier is invalid.");
        }

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_updateSmsNotificationStatus);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, result.ToString());
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, string.IsNullOrWhiteSpace(gatewayReference) ? DBNull.Value : gatewayReference);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, id);

        await pgcom.ExecuteNonQueryAsync();
        tracker.Track();
    }
}
