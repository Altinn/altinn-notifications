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
    /// If e-mail address is provided, genrate email notification. 
    /// If e-mail address is not provided, generate email with failed status for now. 
    /// Future implementation: Missing e-mail =>  Send to kafka queue to complete population of recipient.
    /// </summary>
    public void ProcessEmailNotification(string orderId, EmailTemplate emailTemplate, Recipient recipient);
}