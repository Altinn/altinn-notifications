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

    private const string _getNotificationsByOrgNo = "SELECT * from notifications.get_notifications_by_organization_number($1,$2,$3)"; // (_recipientorgno, _from_date,_to_date)
    private const string _getNotificationsByNin = "SELECT * from notifications.get_notifications_by_nin_v2($1,$2,$3)"; // (_recipientnin, _from_date,_to_date)
    private const string _getNotificationsByEmail = "SELECT * from notifications.get_notifications_by_email($1,$2,$3)"; // (_email, _from_date,_to_date)
    private const string _getNotificationsByPhoneNumber = "SELECT * from notifications.get_notifications_by_phone_number($1,$2,$3)"; // (_phonenumber, _from_date,_to_date)
    private const string _getNotificationsByShipmentId = "SELECT * from notifications.get_notifications_by_shipmentid($1)"; // (_shipmentid)

    private const int _defaultDateRangeDays = 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    public DashboardRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc/>
    public Task<List<DashboardNotification>> GetDashboardNotificationsByNinAsync(string recipientNin, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        // default value is the past 7 days
        DateTime from = (dateTimeFrom ?? DateTime.UtcNow.AddDays(-_defaultDateRangeDays)).ToUniversalTime();
        DateTime to = (dateTimeTo ?? DateTime.UtcNow).ToUniversalTime();
        return GetDashboardNotificationsAsync(_getNotificationsByNin, recipientNin, NpgsqlDbType.Text, from, to, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<DashboardNotification>> GetDashboardNotificationsByOrgNumberAsync(string recipientOrgNo, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        // default value is the past 7 days
        DateTime from = (dateTimeFrom ?? DateTime.UtcNow.AddDays(-_defaultDateRangeDays)).ToUniversalTime();
        DateTime to = (dateTimeTo ?? DateTime.UtcNow).ToUniversalTime();
        return GetDashboardNotificationsAsync(_getNotificationsByOrgNo, recipientOrgNo, NpgsqlDbType.Text, from, to, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<DashboardNotification>> GetDashboardNotificationsByEmailAsync(string email, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        // default value is the past 7 days
        DateTime from = (dateTimeFrom ?? DateTime.UtcNow.AddDays(-_defaultDateRangeDays)).ToUniversalTime();
        DateTime to = (dateTimeTo ?? DateTime.UtcNow).ToUniversalTime();
        return GetDashboardNotificationsAsync(_getNotificationsByEmail, email, NpgsqlDbType.Text, from, to, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<DashboardNotification>> GetDashboardNotificationsByPhoneNumberAsync(string phoneNumber, DateTime? dateTimeFrom, DateTime? dateTimeTo, CancellationToken cancellationToken)
    {
        // default value is the past 7 days
        DateTime from = (dateTimeFrom ?? DateTime.UtcNow.AddDays(-_defaultDateRangeDays)).ToUniversalTime();
        DateTime to = (dateTimeTo ?? DateTime.UtcNow).ToUniversalTime();
        return GetDashboardNotificationsAsync(_getNotificationsByPhoneNumber, phoneNumber, NpgsqlDbType.Text, from, to, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<List<DashboardNotification>> GetDashboardNotificationsByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return GetDashboardNotificationsAsync(_getNotificationsByShipmentId, shipmentId, NpgsqlDbType.Uuid, null, null, cancellationToken);
    }

    /// <summary>
    /// Executes the given SQL command to fetch dashboard notifications for a recipient, grouping delivery attempts by shipment.
    /// </summary>
    /// <param name="sqlCommand">The SQL command to execute. Must accept the recipient value as its first parameter, followed by 'from' and 'to' only when provided.</param>
    /// <param name="recipientValue">The recipient identifier to filter by (e.g. national identity number, organization number, email address, or shipment id).</param>
    /// <param name="recipientValueType">The Npgsql type to send <paramref name="recipientValue"/> as.</param>
    /// <param name="dateTimeFrom">Start of the date range (inclusive), already resolved by the caller. Omitted from the command when null.</param>
    /// <param name="dateTimeTo">End of the date range (exclusive), already resolved by the caller. Omitted from the command when null.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of <see cref="DashboardNotification"/> in the order first seen in the result set, each with its associated delivery attempts.</returns>
    private async Task<List<DashboardNotification>> GetDashboardNotificationsAsync(
        string sqlCommand,
        object recipientValue,
        NpgsqlDbType recipientValueType,
        DateTime? dateTimeFrom,
        DateTime? dateTimeTo,
        CancellationToken cancellationToken)
    {
        // Preserves the first-seen order from the SQL result (ordered by requestedsendtime DESC).
        var orderList = new List<Guid>();
        var groups = new Dictionary<Guid, (string CreatorName, string? ResourceId, string? SendersReference, DateTime RequestedSendTime, NotificationChannel? NotificationChannel, string NotificationType, List<DashboardDeliveryAttempt> DeliveryAttempts)>();

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(sqlCommand);
        pgcom.Parameters.AddWithValue(recipientValueType, recipientValue);

        if (dateTimeFrom.HasValue)
        {
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, dateTimeFrom.Value);
        }

        if (dateTimeTo.HasValue)
        {
            pgcom.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, dateTimeTo.Value);
        }

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync(cancellationToken))
        {
            bool hasRecipientNin = HasColumn(reader, "recipientnin");
            bool hasRecipientOrgNo = HasColumn(reader, "recipientorgno");

            while (await reader.ReadAsync(cancellationToken))
            {
                ProcessRow(reader, hasRecipientNin, hasRecipientOrgNo, groups, orderList);
            }
        }

        return [.. orderList.Select(id =>
        {
            var e = groups[id];
            return new DashboardNotification(id, e.CreatorName, e.ResourceId, e.SendersReference, e.RequestedSendTime, e.NotificationChannel, e.NotificationType, e.DeliveryAttempts);
        })];
    }

    /// <summary>
    /// Reads the current row from <paramref name="reader"/> and either appends a new delivery attempt
    /// to an existing shipment group, or creates a new group for it.
    /// </summary>
    private static void ProcessRow(
        NpgsqlDataReader reader,
        bool hasRecipientNin,
        bool hasRecipientOrgNo,
        Dictionary<Guid, (string CreatorName, string? ResourceId, string? SendersReference, DateTime RequestedSendTime, NotificationChannel? NotificationChannel, string NotificationType, List<DashboardDeliveryAttempt> DeliveryAttempts)> groups,
        List<Guid> orderList)
    {
        string? recipientNin = hasRecipientNin ? reader.GetValue<string?>("recipientnin") : null;
        string? recipientOrgNo = hasRecipientOrgNo ? reader.GetValue<string?>("recipientorgno") : null;

        var shipmentId = reader.GetValue<Guid>("shipmentid");
        string channel = reader.GetValue<string>("channel");
        string? address = reader.GetValue<string>("address");

        var deliveryAttempt = new DashboardDeliveryAttempt(
            nationalIdentityNumber: recipientNin,
            organizationNumber: recipientOrgNo,
            channel: channel,
            emailAddress: channel == "email" ? address : null,
            mobileNumber: channel == "sms" ? address : null,
            result: reader.GetValue<string>("result"),
            resultTime: reader.GetValue<DateTime?>("resulttime"));

        if (groups.TryGetValue(shipmentId, out var entry))
        {
            entry.DeliveryAttempts.Add(deliveryAttempt);
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
                DeliveryAttempts: new List<DashboardDeliveryAttempt> { deliveryAttempt });

            groups[shipmentId] = newEntry;
            orderList.Add(shipmentId);
        }
    }

    /// <summary>
    /// Determines whether the given data reader's result set contains a column with the given name.
    /// </summary>
    private static bool HasColumn(NpgsqlDataReader reader, string colName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), colName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
