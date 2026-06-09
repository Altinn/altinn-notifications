using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface for operations related to cancelling notification orders
    /// </summary>
    public interface ICancelOrderService
    {
        /// <summary>
        /// Cancels an order if it has not been processed yet
        /// </summary>
        /// <param name="id">The order id</param>
        /// <param name="creator">The creator of the orders</param>
        /// <returns>The cancelled order or a </returns>
        public Task<Result<NotificationOrderWithStatus, CancellationError>> CancelOrder(Guid id, string creator);
    }
}
