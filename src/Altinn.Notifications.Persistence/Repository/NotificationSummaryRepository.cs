using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.ApplicationInsights;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of notification summary repository logic
/// </summary>
public class NotificationSummaryRepository : INotificationSummaryRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly TelemetryClient? _telemetryClient;

    private const string _getEmailNotificationsByOrderIdSql = "select * from notifications.getemailsummary_v2($1, $2)"; // (_alternateorderid, creatorname)
    private const string _getSmsNotificationsByOrderIdSql = "select * from notifications.getsmssummary_v2($1, $2)"; // (_alternateorderid, creatorname)

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationRepository"/> class.
    /// </summary>
    /// <param name="dataSource">The npgsql data source.</param>
    /// <param name="telemetryClient">Telemetry client</param>
    public NotificationSummaryRepository(NpgsqlDataSource dataSource, TelemetryClient? telemetryClient = null)
    {
        _dataSource = dataSource;
        _telemetryClient = telemetryClient;
    }

    /// <inheritdoc/>
    public async Task<EmailNotificationSummary?> GetEmailSummary(Guid orderId, string creator)
    {
        var (matchFound, sendersReference, notificationList) = await GetSummary(
            orderId,
            creator,
            _getEmailNotificationsByOrderIdSql,
            reader => new EmailNotificationWithResult(
                reader.GetValue<Guid>("alternateid"),
                new EmailRecipient()
                {
                    OrganizationNumber = reader.GetValue<string?>("recipientorgno"),
                    NationalIdentityNumber = reader.GetValue<string?>("recipientnin"),
                    ToAddress = reader.GetValue<string>("toaddress")
                },
                new NotificationResult<EmailNotificationResultType>(
                    reader.GetValue<EmailNotificationResultType>("result"),
                    reader.GetValue<DateTime>("resulttime"))));

        if (!matchFound)
        {
            return null;
        }

        return new EmailNotificationSummary(orderId)
        {
            SendersReference = sendersReference,
            Notifications = notificationList.ToList()
        };
    }

    /// <inheritdoc/>
    public async Task<SmsNotificationSummary?> GetSmsSummary(Guid orderId, string creator)
    {
        var (matchFound, sendersReference, notificationList) = await GetSummary(
            orderId,
            creator,
            _getSmsNotificationsByOrderIdSql,
            reader => new SmsNotificationWithResult(
                reader.GetValue<Guid>("alternateid"),
                new SmsRecipient()
                {
                    OrganizationNumber = reader.GetValue<string?>("recipientorgno"),
                    NationalIdentityNumber = reader.GetValue<string>("recipientnin"),
                    MobileNumber = reader.GetValue<string>("mobilenumber")
                },
                new NotificationResult<SmsNotificationResultType>(
                    reader.GetValue<SmsNotificationResultType>("result"),
                    reader.GetValue<DateTime>("resulttime"))));

        if (!matchFound)
        {
            return null;
        }

        return new SmsNotificationSummary(orderId)
        {
            SendersReference = sendersReference,
            Notifications = notificationList.ToList()
        };
    }

    private async Task<(bool MatchFound, string SendersReference, List<T> NotificationList)> GetSummary<T>(Guid orderId, string creator, string sqlCommand, Func<NpgsqlDataReader, T> createNotification)
    {
        bool matchFound = false;
        List<T> notificationList = new();
        string sendersReference = string.Empty;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(sqlCommand);
        using TelemetryTracker tracker = new(_telemetryClient, pgcom);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Uuid, orderId);
        pgcom.Parameters.AddWithValue(NpgsqlDbType.Text, creator);

        await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (!matchFound)
                {
                    matchFound = true;
                    sendersReference = reader.GetValue<string>("sendersreference");
                }

                Guid notificationId = reader.GetValue<Guid>("alternateid");

                if (notificationId != Guid.Empty)
                {
                    notificationList.Add(createNotification(reader));
                }
            }
        }

        tracker.Track();

        return (matchFound, sendersReference, notificationList);
    }
}
