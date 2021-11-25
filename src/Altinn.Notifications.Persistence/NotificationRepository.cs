using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;
using NpgsqlTypes;

namespace Altinn.Notifications.Persistence
{
    public class NotificationRepository : INotificationsRepository
    {
        private readonly string insertSubscriptionSql = "select * from notifications.insert_notification(@sendtime, @instanceid, @partyreference, @sender)";

        private readonly string _connectionString;
        
        private readonly ILogger _logger;

        public NotificationRepository(IOptions<PostgreSQLSettings> postgresSettings, ILogger<NotificationRepository> logger)
        {
            _connectionString = string.Format(postgresSettings.Value.ConnectionString, postgresSettings.Value.EventsDbPwd);

            _logger = logger;
        }

        /// <summary>
        /// Remporarily created constructor to simplyfy testing.
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="logger">Logger</param>
        public NotificationRepository(string connectionString, ILogger<NotificationRepository> logger)
        {
            _connectionString = connectionString;

            _logger = logger;
        }

        public async Task<Notification> SaveNotification(Notification notification)
        {
            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            
            await conn.OpenAsync();

            NpgsqlCommand pgcom = new NpgsqlCommand(insertSubscriptionSql, conn);
            pgcom.Parameters.AddWithValue("sendtime", notification.SendTime);

            if (notification.InstanceId != null)
            {
                pgcom.Parameters.AddWithValue("instanceid", notification.InstanceId);
            }
            else
            {
                pgcom.Parameters.AddWithValue("instanceid", DBNull.Value);
            }

            if (notification.PartyReference != null)
            {
                pgcom.Parameters.AddWithValue("partyreference", notification.PartyReference);
            }
            else
            {
                pgcom.Parameters.AddWithValue("partyreference", DBNull.Value);
            }

            pgcom.Parameters.AddWithValue("sender", notification.Sender);

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                reader.Read();
                return ReadNotification(reader);
            }
        }

        private static Notification ReadNotification(NpgsqlDataReader reader)
        {
            Notification notification = new Notification();
            notification.Id = reader.GetValue<int>("id");
            notification.SendTime = reader.GetValue<DateTime>("sendtime").ToUniversalTime();
            notification.InstanceId = reader.GetValue<string>("instanceid");
            notification.PartyReference = reader.GetValue<string>("partyreference");
            notification.Sender = reader.GetValue<string>("sender");
            return notification;
        }
    }
}
