using Altinn.Notifications.Core;
using Altinn.Notifications.Core.Models;

using Microsoft.Extensions.Options;

using Npgsql;

using NpgsqlTypes;

namespace Altinn.Notifications.Persistence
{
    public class NotificationRepository : INotificationsRepository
    {
        private readonly string _connectionString;

        public NotificationRepository(IOptions<PostgreSQLSettings> postgresSettings)
        {
            _connectionString = string.Format(
                postgresSettings.Value.ConnectionString,
                postgresSettings.Value.NotificationsDbPwd);
        }

        public async Task<Notification> AddNotification(Notification notification)
        {
            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);

            await conn.OpenAsync();

            string insertNotificationSql = "select * from notifications.insert_notification(@sendtime, @instanceid, @partyreference, @sender)";
            NpgsqlCommand pgcom = new NpgsqlCommand(insertNotificationSql, conn);
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

            if (notification.Sender != null)
            {
                pgcom.Parameters.AddWithValue("sender", notification.Sender);
            }
            else
            {
                pgcom.Parameters.AddWithValue("sender", DBNull.Value);
            }

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                reader.Read();
                return ReadNotification(reader);
            }
        }

        /// <inheritdoc/>
        public async Task<Notification?> GetNotification(int id)
        {
            Notification? notification = null;

            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            NpgsqlCommand pgcom = new NpgsqlCommand("select * from notifications.get_notification(@_id)", conn);
            pgcom.Parameters.AddWithValue("_id", NpgsqlDbType.Integer, id);

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                if (reader.Read())
                {
                    notification = ReadNotification(reader);

                    do
                    {
                        int targetId = reader.GetValue<int>("targetid");
                        if (notification.Targets.All(t => t.Id != targetId))
                        {
                            Target target = new Target();
                            // Setting id values separately because the column names are different
                            target.Id = targetId;
                            target.NotificationId = notification.Id;
                            ReadTargetValues(target, reader);
                            notification.Targets.Add(target);
                        }

                        int messageId = reader.GetValue<int>("messageid");
                        if (notification.Messages.All(m => m.Id != messageId))
                        {
                            Message message = new Message();
                            // Setting id values separately because the column names are different
                            message.Id = messageId;
                            message.NotificationId = notification.Id;
                            ReadMessageValues(message, reader);
                            notification.Messages.Add(message);
                        }
                    } while (reader.Read());
                }
            }

            return notification;
        }

        public async Task<Target> AddTarget(Target target)
        {
            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);

            await conn.OpenAsync();

            string insertTargetSql = "select * from notifications.insert_target(@notificationid, @channeltype, @address, @sent)";
            NpgsqlCommand pgcom = new NpgsqlCommand(insertTargetSql, conn);
            pgcom.Parameters.AddWithValue("notificationid", target.NotificationId);
            pgcom.Parameters.AddWithValue("channeltype", target.ChannelType);

            if (target.Address != null)
            {
                pgcom.Parameters.AddWithValue("address", target.Address);
            }
            else
            {
                pgcom.Parameters.AddWithValue("address", DBNull.Value);
            }

            if (target.Sent != null)
            {
                pgcom.Parameters.AddWithValue("sent", target.Sent);
            }
            else
            {
                pgcom.Parameters.AddWithValue("sent", DBNull.Value);
            }

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                reader.Read();
                return ReadTarget(reader);
            }
        }

        public async Task<Message> AddMessage(Message message)
        {
            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);

            await conn.OpenAsync();

            string insertMessageSql = "select * from notifications.insert_message(@notificationid, @emailsubject, @emailbody, @smstext, @language)";
            NpgsqlCommand pgcom = new NpgsqlCommand(insertMessageSql, conn);
            pgcom.Parameters.AddWithValue("notificationid", message.NotificationId);

            if (message.EmailSubject != null)
            {
                pgcom.Parameters.AddWithValue("emailsubject", message.EmailSubject);
            }
            else
            {
                pgcom.Parameters.AddWithValue("emailsubject", DBNull.Value);
            }

            if (message.EmailBody != null)
            {
                pgcom.Parameters.AddWithValue("emailbody", message.EmailBody);
            }
            else
            {
                pgcom.Parameters.AddWithValue("emailbody", DBNull.Value);
            }

            if (message.SmsText != null)
            {
                pgcom.Parameters.AddWithValue("smstext", message.SmsText);
            }
            else
            {
                pgcom.Parameters.AddWithValue("smstext", DBNull.Value);
            }

            pgcom.Parameters.AddWithValue("language", message.Language);

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                reader.Read();
                return ReadMessage(reader);
            }
        }

        public async Task<List<Target>> GetUnsentTargets()
        {
            List<Target> unsentTargets = new List<Target>();

            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            NpgsqlCommand pgcom = new NpgsqlCommand("select * from notifications.get_unsenttargets()", conn);

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                while (reader.Read())
                {
                    unsentTargets.Add(ReadTarget(reader));
                }
            }

            return unsentTargets;
        }

        public async Task<Target?> GetTarget(int targetId)
        {
            Target? target = null;

            using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            NpgsqlCommand pgcom = new NpgsqlCommand("select * from notifications.get_target(@_id)", conn);
            pgcom.Parameters.AddWithValue("_id", NpgsqlDbType.Integer, targetId);

            using (NpgsqlDataReader reader = pgcom.ExecuteReader())
            {
                if (reader.Read())
                {
                    target = ReadTarget(reader);
                }
            }

            return target;
        }

        public async Task UpdateSentTarget(int id)
        {
            await using NpgsqlConnection conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using NpgsqlCommand pgcom = new NpgsqlCommand("call notifications.update_senttarget(@_id)", conn);
            pgcom.Parameters.AddWithValue("_id", id);

            await pgcom.ExecuteNonQueryAsync();
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

        private static Target ReadTarget(NpgsqlDataReader reader)
        {
            Target target = new Target();
            target.Id = reader.GetValue<int>("id");
            target.NotificationId = reader.GetValue<int>("notificationid");
            ReadTargetValues(target, reader);
            return target;
        }

        private static void ReadTargetValues(Target target, NpgsqlDataReader reader)
        {
            target.ChannelType = reader.GetValue<string>("channeltype");
            target.Address = reader.GetValue<string>("address");
            target.Sent = reader.GetValue<DateTime>("sent").ToUniversalTime();
        }

        private static Message ReadMessage(NpgsqlDataReader reader)
        {
            Message message = new Message();
            message.Id = reader.GetValue<int>("id");
            message.NotificationId = reader.GetValue<int>("notificationid");
            ReadMessageValues(message, reader);
            return message;
        }

        private static void ReadMessageValues(Message message, NpgsqlDataReader reader)
        {
            message.EmailSubject = reader.GetValue<string>("emailsubject");
            message.EmailBody = reader.GetValue<string>("emailbody");
            message.SmsText = reader.GetValue<string>("smstext");
            message.Language = reader.GetValue<string>("language");
        }
    }
}
