using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Dashboard;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;
using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of dashboard repository logic
/// </summary>
public class DashboardRepository : IDashboardRepository
{
    private readonly NpgsqlDataSource _dataSource;

    private const string _getNotificationsByNin = "SELECT * from notifications.get_notifications_by_nin_v2($1,$2,$3)"; // (_recipientnin, _from_date,_to_date)

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public DashboardRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>
    /// Retrieves all notifications (email and SMS) for a recipient identified by their national identity number within a given date range.
    /// If no date range is provided, defaults to the last 7 days.
    /// </summary>
    /// <param name="recipientNin">The national identity number of the recipient.</param>
    /// <param name="dateTimeFrom">Start of the date range (inclusive). Defaults to 7 days ago if null.</param>
    /// <param name="dateTimeTo">End of the date range (exclusive). Defaults to now if null.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of <see cref="DashboardNotification"/> matching the search criteria.</returns>
    public async Task<List<DashboardNotification>> GetDashboardNotificationsByNinAsync(string recipientNin, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        DateTime from = (dateTimeFrom ?? DateTime.UtcNow.AddDays(-7)).ToUniversalTime();
        DateTime to = (dateTimeTo ?? DateTime.UtcNow).ToUniversalTime();

        // Preserves the first-seen order from the SQL result (ordered by requestedsendtime DESC).
        var orderList = new List<Guid>();
        var groups = new Dictionary<Guid, (string CreatorName, string? ResourceId, string? SendersReference, DateTime RequestedSendTime, NotificationChannel? NotificationChannel, string NotificationType, List<DashboardDeliveryAttempt> DeliveryAttempts)>();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getNotificationsByNin);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, recipientNin);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, from);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, to);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var shipmentId = reader.GetValue<Guid>("shipmentid");
                string channel = reader.GetValue<string>("channel");
                string? address = reader.GetValue<string>("address");

                var recipient = new DashboardDeliveryAttempt(
                    nationalIdentityNumber: reader.GetValue<string>("recipientnin"),
                    channel: channel,
                    emailAddress: channel == "email" ? address : null,
                    mobileNumber: channel == "sms" ? address : null,
                    result: reader.GetValue<string>("result"),
                    resultTime: reader.GetValue<DateTime?>("resulttime"));

                if (groups.TryGetValue(shipmentId, out var entry))
                {
                    entry.DeliveryAttempts.Add(recipient);
                }
                else
                {
                    var channelString = reader.GetValue<string>("notificationchannel");
                    var newEntry = (
                        CreatorName: reader.GetValue<string>("creatorname"),
                        ResourceId: reader.GetValue<string>("resourceid"),
                        SendersReference: reader.GetValue<string>("sendersreference"),
                        RequestedSendTime: reader.GetValue<DateTime>("requestedsendtime"),
                        NotificationChannel: Enum.TryParse<NotificationChannel>(channelString, out var notificationChannel) ? notificationChannel : (NotificationChannel?)null,
                        NotificationType: reader.GetValue<string>("notificationtype"),
                        DeliveryAttempts: new List<DashboardDeliveryAttempt> { recipient });

                    groups[shipmentId] = newEntry;
                    orderList.Add(shipmentId);
                }
            }
        }

        return [.. orderList.Select(id =>
        {
            var e = groups[id];
            return new DashboardNotification(id, e.CreatorName, e.ResourceId, e.SendersReference, e.RequestedSendTime, e.NotificationChannel, e.NotificationType, e.DeliveryAttempts);
        })];
    }
}
