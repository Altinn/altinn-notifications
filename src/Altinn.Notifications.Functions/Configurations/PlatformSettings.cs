namespace Altinn.Notifications.Functions.Configurations
{
    public class PlatformSettings
    {
        public string ApiNotificationsEndpoint { get; set; }

        public string StorageQueueConnectionString { get;set; }

        public string OutboundQueueName { get; set; } = "notifications-outbound";
    }
}


