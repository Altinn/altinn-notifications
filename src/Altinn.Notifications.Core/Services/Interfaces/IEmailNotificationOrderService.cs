using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for the email notification order service
/// </summary>
public interface IEmailNotificationOrderService
{
    /// <summary>
    /// Registers a new order
    /// </summary>
    /// <param name="order">The email notification order request</param>
    /// <returns></returns>
    public Task<NotificationOrder> RegisterNewEmailNotificationOrder(NotificationOrder order);
}