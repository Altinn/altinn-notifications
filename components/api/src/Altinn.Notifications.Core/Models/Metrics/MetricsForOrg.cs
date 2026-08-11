namespace Altinn.Notifications.Core.Models.Metrics
{
    /// <summary>
    /// Class describing the notification metrics for an organization
    /// </summary>
    public class MetricsForOrg
    {
        /// <summary>
        /// The organization the metrics apply for
        /// </summary>
        public string Org { get; set; } = string.Empty;

        /// <summary>
        /// Total number of orders created
        /// </summary>
        public int OrdersCreated { get; set; }

        /// <summary>
        /// Total number of generated email notifications
        /// </summary>
        public int EmailNotificationsCreated { get; set; }

        /// <summary>
        /// Number of successfully sent email notifications
        /// </summary>
        public int SuccessfulEmailNotifications { get; set; }

        /// <summary>
        /// Total number of generated sms notifications
        /// </summary>
        public int SmsNotificationsCreated { get; set; }

        /// <summary>
        /// Number of successfully sent sms notifications
        /// </summary>
        public int SuccessfulSmsNotifications { get; set; }

        /// <summary>
        /// The number of emails with attachments associated with this organization.
        /// </summary>
        public int EmailsWithAttachmentsCount { get; set; }

        /// <summary>
        /// The total size of all encoded attachments associated with this organization, in bytes.
        /// </summary>
        public long TotalEncodedAttachmentsSize { get; set; }
    }
}
