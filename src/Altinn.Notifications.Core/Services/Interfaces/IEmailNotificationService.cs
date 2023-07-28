using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for email notification service
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// Creates a new email notification based on the provided orderId and recipient
    /// </summary>
    public Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient);

    /// <summary>
    /// Starts the process of sending all ready email notifications
    /// </summary>
    public Task SendNotifications();
}