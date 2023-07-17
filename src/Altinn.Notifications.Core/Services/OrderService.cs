using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Clas
/// </summary>
public class OrderService
{
    private readonly IGuidService _guid;

    private OrderService(IGuidService guid)
    {
        _guid = guid;
    }

    /// <summary>
    /// Gets past due order
    /// </summary>
    public void GetPastDueOrders()
    {
        List<NotificationOrder> pastDueOrders = new(); //Repository.GetPastDueOrdersAndSetProcessState();

        foreach (NotificationOrder order in pastDueOrders)
        {
            NotificationChannel ch = order.NotificationChannel;
            EmailTemplate? emailTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Email) as EmailTemplate;

            foreach (Recipient recipient in order.Recipients)
            {
                switch (ch)
                {
                    case NotificationChannel.Email:
                        if (emailTemplate != null)
                        {
                            ProcessEmailNotification(order.Id, emailTemplate, recipient);
                        }

                        break;
                }
            }

            // Repository.ProcessingCompleted(orderId)
        }
    }

    private void GetPendingOrders()
    {
        List<NotificationOrder> pastDueOrders = new(); // Repository.GetPendingOrdersAndSetNewProcessState();

        foreach (NotificationOrder order in pastDueOrders)
        {
            NotificationChannel ch = order.NotificationChannel;

            switch (ch)
            {
                case NotificationChannel.Email:
                    List<EmailNotification> generatedEmailNotifiations = null; // Repository.GetAllEmailNotificationsForOrder();

                    // hvis det er en recipient det ikke er generert notificaion for, opprett notification og følg vanlig flyt
                    break;
            }

            // Repository.ProcessingCompleted(orderId)
        }
    }

    /// <summary>
    /// Process all email notifications. 
    /// If e-mail address is provided, genrate email notification. 
    /// If e-mail address is not provided, generate email with failed status for now. 
    /// Future implementation: Missing e-mail =>  Send to kafka queue to complete population of recipient.
    /// </summary>
    private void ProcessEmailNotification(string orderId, EmailTemplate emailTemplate, Recipient recipient)
    {
        EmailAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Email) as EmailAddressPoint;

        if (!string.IsNullOrEmpty(addressPoint?.EmailAddress))
        {
            GenerateEmailNotificationForRecipient(orderId, emailTemplate, recipient.RecipientId, addressPoint.EmailAddress);
        }
        else
        {
            // GenerateFailedEmailNotificationForRecipient(order, recipientId, "No email address identified for recipient");
        }
    }

    private void GenerateEmailNotificationForRecipient(string orderId, EmailTemplate emailTemplate, string recipientId, string toAddress)
    {
        var emailNotification = new EmailNotification()
        {
            Id = _guid.NewGuidAsString(),
            OrderId = orderId,
            ToAddress = toAddress,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId
        };

        // save in DB. With default status ? 
    }
}