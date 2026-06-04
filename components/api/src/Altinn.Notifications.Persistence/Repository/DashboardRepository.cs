using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
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

    private const string _getNotificationsByNin = "SELECT * from notifications.get_notifications_by_nin($1,$2,$3)"; // (_nin, _dateFrom,_dateTo)

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
    public async Task<List<DashboardNotification>> GetDashboardNotificationsByNinAsync(string recipientNin, DateTimeOffset? dateTimeFrom, DateTimeOffset? dateTimeTo, CancellationToken cancellationToken)
    {
        List<DashboardNotification> searchResult = [];
        DateTimeOffset from = dateTimeFrom ?? DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset to = dateTimeTo ?? DateTimeOffset.UtcNow;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getNotificationsByNin);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, recipientNin);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, from);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, to);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                string channel = reader.GetValue<string>("channel");
                string? address = reader.GetValue<string>("address");

                IAddressPoint? addressPoint = (channel, address) switch
                {
                    ("email", not null) => new EmailAddressPoint(address),
                    ("sms", not null) => new SmsAddressPoint(address),
                    _ => null
                };

                List<IAddressPoint> addressInfo = addressPoint is not null ? [addressPoint] : [];

                searchResult.Add(new DashboardNotification(
                    reader.GetValue<Guid>("notificationid"),
                    reader.GetValue<string>("creatorname"),
                    reader.GetValue<string>("resourceid"),
                    reader.GetValue<string>("sendersreference"),
                    reader.GetValue<DateTime>("requestedsendtime"),
                    [new Recipient(addressInfo, nationalIdentityNumber: reader.GetValue<string>("recipientnin"))],
                    channel,
                    reader.GetValue<string>("result"),
                    reader.GetValue<DateTime?>("resulttime")));
            }
        }

        return searchResult;
    }
}
