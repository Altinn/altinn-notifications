using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Interface for email notification service
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>
    /// Process all email notifications.
    /// </summary>
    public Task CreateEmailNotification(string orderId, DateTime requestedSendTime, EmailTemplate emailTemplate, Recipient recipient);

    /// <summary>
    /// Stats the process of sending all ready email notifications
    /// </summary>
    /// <returns></returns>
    public Task SendNotifications();

}