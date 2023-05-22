﻿#nullable disable
namespace Altinn.Notifications.Persistence.Configuration
{
    /// <summary>
    /// Settings for Postgre database
    /// </summary>
    public class PostgreSqlSettings
    {
        /// <summary>
        /// Connection string for the postgre db
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Password for app user for the postgre db
        /// </summary>
        public string NotificationssDbPwd { get; set; }
    }
}