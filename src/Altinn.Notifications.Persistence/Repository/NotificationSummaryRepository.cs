﻿using Altinn.Notifications.Core.Enums;
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

    private const string _getEmailNotificationsByOrderIdSql = "select * from notifications.getemailsummary($1, $2)"; // (_alternateorderid, creatorname)
    private const string _getSmsNotificationsByOrderIdSql = "select * from notifications.getsmssummary($1, $2)"; // (_alternateorderid, creatorname)

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
        bool matchFound = false;

        List<EmailNotificationWithResult> notificationList = new();
        string sendersReference = string.Empty;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getEmailNotificationsByOrderIdSql);
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
                    notificationList.Add(
                    new EmailNotificationWithResult(
                         reader.GetValue<Guid>("alternateid"),
                         new EmailRecipient()
                         {
                             RecipientId = reader.GetValue<string>("recipientid"),
                             ToAddress = reader.GetValue<string>("toaddress")
                         },
                         new NotificationResult<EmailNotificationResultType>(
                            reader.GetValue<EmailNotificationResultType>("result"),
                            reader.GetValue<DateTime>("resulttime"))));
                }
            }
        }

        tracker.Track();

        if (!matchFound)
        {
            return null;
        }

        EmailNotificationSummary emailSummary = new(orderId)
        {
            SendersReference = sendersReference,
            Notifications = notificationList
        };

        return emailSummary;
    }

    /// <inheritdoc/>
    public async Task<SmsNotificationSummary?> GetSmsSummary(Guid orderId, string creator)
    {
        bool matchFound = false;

        List<SmsNotificationWithResult> notificationList = new();
        string sendersReference = string.Empty;

        await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsNotificationsByOrderIdSql);
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
                    notificationList.Add(
                    new SmsNotificationWithResult(
                         reader.GetValue<Guid>("alternateid"),
                         new SmsRecipient()
                         {
                             RecipientId = reader.GetValue<string>("recipientid"),
                             MobileNumber = reader.GetValue<string>("mobilenumber")
                         },
                         new NotificationResult<SmsNotificationResultType>(
                            reader.GetValue<SmsNotificationResultType>("result"),
                            reader.GetValue<DateTime>("resulttime"))));
                }
            }
        }

        tracker.Track();

        if (!matchFound)
        {
            return null;
        }

        SmsNotificationSummary smsSummary = new(orderId)
        {
            SendersReference = sendersReference,
            Notifications = notificationList
        };

        return smsSummary;
    }
}
