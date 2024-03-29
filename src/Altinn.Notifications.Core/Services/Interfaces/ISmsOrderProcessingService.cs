﻿using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the order processing service speficic to sms orders
/// </summary>
public interface ISmsOrderProcessingService
{
    /// <summary>
    /// Processes a notification order
    /// </summary>
    public Task ProcessOrder(NotificationOrder order);

    /// <summary>
    /// Retry processing of an order
    /// </summary>
    public Task ProcessOrderRetry(NotificationOrder order);
}
