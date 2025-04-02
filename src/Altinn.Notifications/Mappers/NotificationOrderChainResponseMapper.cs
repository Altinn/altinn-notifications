using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Mappers
{
    /// <summary>
    /// Provides extension methods for mapping domain models of 
    /// <see cref="NotificationOrderChainResponse"/> objects to their corresponding 
    /// external data transfer models of <see cref="NotificationOrderChainResponseExt"/>.
    /// </summary>
    public static class NotificationOrderChainResponseMapper
    {
        /// <summary>
        /// Maps a <see cref="NotificationOrderChainResponse"/> to a <see cref="NotificationOrderChainResponseExt"/>
        /// </summary>
        public static NotificationOrderChainResponseExt MapToNotificationOrderChainResponseExt(this NotificationOrderChainResponse response)
        {
            return new NotificationOrderChainResponseExt
            {
                OrderChainId = response.OrderChainId,
                OrderChainReceipt = MapOrderChainReceipt(response)
            };
        }

        /// <summary>
        /// Maps a <see cref="NotificationOrderChainResponse"/> to a <see cref="NotificationOrderChainReceiptExt"/>
        /// </summary>
        private static NotificationOrderChainReceiptExt MapOrderChainReceipt(NotificationOrderChainResponse response)
        {
            return new NotificationOrderChainReceiptExt
            {
                ShipmentId = response.OrderChainReceipt.ShipmentId,
                SendersReference = response.OrderChainReceipt.SendersReference,
                Reminders = MapOrderChainShipments(response.OrderChainReceipt.Reminders),
            };
        }

        /// <summary>
        /// Maps a <see cref="List{NotificationOrderChainShipment}"/> to a <see cref="List{NotificationOrderChainShipmentExt}"/>
        /// </summary>
        private static List<NotificationOrderChainShipmentExt>? MapOrderChainShipments(List<NotificationOrderChainShipment>? reminders)
        {
            return reminders?.Select(e => new NotificationOrderChainShipmentExt
            {
                ShipmentId = e.ShipmentId,
                SendersReference = e.SendersReference
            }).ToList();
        }
    }
}
