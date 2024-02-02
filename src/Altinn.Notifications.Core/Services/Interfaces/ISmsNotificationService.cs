using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for sms notification service
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Creates a new sms notification based on the provided orderId and recipient
    /// </summary>
    public Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient);

    /// <summary>
    /// Starts the process of sending all ready sms notifications
    /// </summary>
    public Task SendNotifications();
}
