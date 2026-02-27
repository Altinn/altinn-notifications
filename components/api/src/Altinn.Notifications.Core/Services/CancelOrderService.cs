using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of the <see cref="ICancelOrderService"/> interface.
    /// </summary>
    public class CancelOrderService : ICancelOrderService
    {
        private readonly IOrderRepository _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancelOrderService"/> class.
        /// </summary>
        /// <param name="repository">The repository</param>
        public CancelOrderService(IOrderRepository repository)
        {
            _repository = repository;
        }

        /// <inheritdoc/>
        public async Task<Result<NotificationOrderWithStatus, CancellationError>> CancelOrder(Guid id, string creator)
        {
            var result = await _repository.CancelOrder(id, creator);

            return result.Match<Result<NotificationOrderWithStatus, CancellationError>>(
                 order =>
                 {
                     order.ProcessingStatus.StatusDescription = GetOrderService.GetStatusDescription(order.ProcessingStatus.Status);
                     return order;
                 },
                 error =>
                 {
                     return error;
                 });
        }
    }
}
