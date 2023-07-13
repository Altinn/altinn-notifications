using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Repository.Interfaces;

namespace Altinn.Notifications.Persistence.Repository;

/// <summary>
/// Implementation of order repository logic
/// </summary>
public class OrderRepository : IOrderRepository
{
    /// <inheritdoc/>
    public Task<NotificationOrder> Create(NotificationOrder order)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<NotificationOrder> GetById(string id)
    {
        throw new NotImplementedException();
    }
}