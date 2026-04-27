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
    /// <returns>A list of <see cref="DashboardNotification"/> matching the search criteria.</returns>
    public async Task<List<DashboardNotification>> GetDashboardNotificationsByNinAsync(string recipientNin, DateTime? dateTimeFrom, DateTime? dateTimeTo)
    {
        List<DashboardNotification> searchResult = [];
        if (dateTimeFrom == null || dateTimeTo == null)
        {
            dateTimeFrom = DateTime.UtcNow.AddDays(-7);
            dateTimeTo = DateTime.UtcNow;
        }

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getNotificationsByNin);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, recipientNin);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, dateTimeFrom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, dateTimeTo);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                searchResult.Add(new DashboardNotification()
                {
                    NotificationId = reader.GetValue<Guid>("notificationid"),
                    OrderId = reader.GetValue<long>("_orderid"),
                    SendersReference = reader.GetValue<string>("sendersreference"),
                    RequestedSendTime = reader.GetValue<DateTime>("requestedsendtime"),
                    RecipientOrgNo = reader.GetValue<string>("recipientorgno"),
                    RecipientNin = reader.GetValue<string>("recipientnin"),
                    Channel = reader.GetValue<string>("channel"),
                    Result = reader.GetValue<string>("result"),
                    ResultTime = reader.GetValue<DateTime>("resulttime"),
                    ResourceId = reader.GetValue<string>("resourceid"),
                    CreatorName = reader.GetValue<string>("creatorname")
                });
            }
        }

        return searchResult;
    }
}
