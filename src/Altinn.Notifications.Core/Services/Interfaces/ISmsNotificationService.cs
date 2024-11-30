using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for SMS notification service
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Creates a new SMS notification based on the provided orderId and recipient
    /// </summary>
    public Task CreateNotification(Guid orderId, DateTime requestedSendTime, List<SmsAddressPoint> smsAddresses, SmsRecipient smsRecipient, int smsCount, bool ignoreReservation = false);

    /// <summary>
    /// Starts the process of sending all ready SMS notifications
    /// </summary>
    public Task SendNotifications();

    /// <summary>
    /// Update send status for an SMS notification
    /// </summary>
    public Task UpdateSendStatus(SmsSendOperationResult sendOperationResult);
}
