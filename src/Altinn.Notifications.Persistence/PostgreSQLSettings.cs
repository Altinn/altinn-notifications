namespace Altinn.Notifications.Persistence
{
    /// <summary>
    /// Represents settings needed to communicate with the PostgreSQL database server.
    /// </summary>
    public class PostgreSQLSettings
    {
        /// <summary>
        /// Connection string to the PostgresSQL database server.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// The application database user password. Username already a part of the connection string.
        /// </summary>
        public string NotificationsDbPwd { get; set; } = string.Empty;
    }
}
