using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Notification;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for SMS notification service
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Creates a new SMS notification based on the provided orderId and recipient
    /// </summary>
    public Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient, int smsCount, bool ignoreReservation = false, string? body = null);

    /// <summary>
    /// Starts the process of sending all ready SMS notifications
    /// </summary>
    public Task SendNotifications();

    /// <summary>
    /// Update send status for an SMS notification
    /// </summary>
    public Task UpdateSendStatus(SmsSendOperationResult sendOperationResult);
}
