using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Persistence.Extensions;

using Microsoft.ApplicationInsights;

using Npgsql;

namespace Altinn.Notifications.Persistence.Repository
{
    /// <summary>
    /// Implementation of sms notification repository logic
    /// </summary>
    public class SmsNotificationRepository : ISmsNotificationRepository
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly TelemetryClient? _telemetryClient;

        private const string _getSmsNotificationsSql = "select * from notifications.getsms_statusnew_updatestatus()";

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
        public async Task<List<Sms>> GetNewNotifications()
        {
            List<Sms> searchResult = new();
            await using NpgsqlCommand pgcom = _dataSource.CreateCommand(_getSmsNotificationsSql);
            using TelemetryTracker tracker = new(_telemetryClient, pgcom);

            await using (NpgsqlDataReader reader = await pgcom.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    EmailContentType emailContentType = (EmailContentType)Enum.Parse(typeof(EmailContentType), reader.GetValue<string>("contenttype"));

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
}
