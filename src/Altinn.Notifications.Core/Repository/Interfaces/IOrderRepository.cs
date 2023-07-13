﻿using System;
using Altinn.Notifications.Core.Models.Orders;

namespace Altinn.Notifications.Core.Repository.Interfaces;

/// <summary>
/// Interface describing all repository actions for notification orders
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Gets a notification order by id
    /// </summary>
    /// <param name="id">The id of the notification order to retrieve</param>
    /// <returns>A notification order</returns>
    public Task<NotificationOrder> GetById(string id);

    /// <summary>
    /// Creates a new notification order in the database
    /// </summary>
    /// <param name="order">The order to save</param>
    /// <returns>The saved notification order</returns>
    public Task<NotificationOrder> Create(NotificationOrder order);
}