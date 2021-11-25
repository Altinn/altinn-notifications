namespace Altinn.Notifications.Persistence
{
    public class PostgreSQLSettings
    {
        /// <summary>
        /// Connection string for the postgres db
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Password for app user for the postgres db
        /// </summary>
        public string EventsDbPwd { get; set; }
    }
}
