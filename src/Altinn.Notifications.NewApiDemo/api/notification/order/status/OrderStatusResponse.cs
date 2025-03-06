namespace Altinn.Notifications.NewApiDemo.api.order.status
{
    public class OrderStatusResponse
    {
        public class NotificationOrderResponse
        {
            [Description("The uuid of the order")]
            [JsonPropertyName("notificationOrderId")]
            [JsonPropertyOrder(0)]
            public required Guid NotificationOrderId { get; set; }
        
            [Description("The notification contained in the order")]
            [JsonPropertyName("notification")]
            [JsonPropertyOrder(1)]
            public required NotificationStatus NotificationStatus { get; set; }
    }
}
