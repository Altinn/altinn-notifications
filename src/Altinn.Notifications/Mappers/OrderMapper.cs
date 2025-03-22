using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between external API models and internal domain models.
/// </summary>
public static class OrderMapper
{
    /// <summary>
    /// Maps a <see cref="NotificationOrderRequestExt"/> to a <see cref="NotificationOrderRequest"/>
    /// </summary>
    public static NotificationOrderRequest MapToOrderRequest(this NotificationOrderRequestExt extRequest, string creator)
    {
        List<Recipient> recipients =
          extRequest.Recipients
          .Select(r =>
          {
              List<IAddressPoint> addresses = [];

              if (!string.IsNullOrEmpty(r.EmailAddress))
              {
                  addresses.Add(new EmailAddressPoint(r.EmailAddress));
              }

              if (!string.IsNullOrEmpty(r.MobileNumber))
              {
                  addresses.Add(new SmsAddressPoint(r.MobileNumber));
              }

              return new Recipient(addresses, r.OrganizationNumber, r.NationalIdentityNumber);
          })
          .ToList();

        List<INotificationTemplate> templateList = [];

        if (extRequest.EmailTemplate != null)
        {
            var emailTemplate = new EmailTemplate(
                null,
                extRequest.EmailTemplate.Subject,
                extRequest.EmailTemplate.Body,
                (EmailContentType)extRequest.EmailTemplate.ContentType);

            templateList.Add(emailTemplate);
        }

        if (extRequest.SmsTemplate != null)
        {
            INotificationTemplate smsTemplate = new SmsTemplate(extRequest.SmsTemplate.SenderNumber, extRequest.SmsTemplate.Body);

            templateList.Add(smsTemplate);
        }

        return new NotificationOrderRequest(
            extRequest.SendersReference,
            creator,
            templateList,
            extRequest.RequestedSendTime.ToUniversalTime(),
            (NotificationChannel)extRequest.NotificationChannel!,
            recipients,
            extRequest.IgnoreReservation,
            extRequest.ResourceId,
            extRequest.ConditionEndpoint);
    }

    /// <summary>
    /// Maps a <see cref="EmailNotificationOrderRequestExt"/> to a <see cref="NotificationOrderRequest"/>
    /// </summary>
    public static NotificationOrderRequest MapToOrderRequest(this EmailNotificationOrderRequestExt extRequest, string creator)
    {
        var emailTemplate = new EmailTemplate(
            null,
            extRequest.Subject,
            extRequest.Body,
            (EmailContentType?)extRequest.ContentType ?? EmailContentType.Plain);

        List<Recipient> recipients =
            extRequest.Recipients
            .Select(r =>
            {
                List<IAddressPoint> addresses = [];

                if (!string.IsNullOrEmpty(r.EmailAddress))
                {
                    addresses.Add(new EmailAddressPoint(r.EmailAddress));
                }

                return new Recipient(addresses, r.OrganizationNumber, r.NationalIdentityNumber);
            })
            .ToList();

        return new NotificationOrderRequest(
            extRequest.SendersReference,
            creator,
            [emailTemplate],
            extRequest.RequestedSendTime.ToUniversalTime(),
            NotificationChannel.Email,
            recipients,
            extRequest.IgnoreReservation,
            extRequest.ResourceId,
            extRequest.ConditionEndpoint);
    }

    /// <summary>
    /// Maps a <see cref="SmsNotificationOrderRequestExt"/> to a <see cref="NotificationOrderRequest"/>
    /// </summary>
    public static NotificationOrderRequest MapToOrderRequest(this SmsNotificationOrderRequestExt extRequest, string creator)
    {
        INotificationTemplate smsTemplate = new SmsTemplate(extRequest.SenderNumber, extRequest.Body);

        List<Recipient> recipients =
          extRequest.Recipients
          .Select(r =>
          {
              List<IAddressPoint> addresses = [];

              if (!string.IsNullOrEmpty(r.MobileNumber))
              {
                  addresses.Add(new SmsAddressPoint(r.MobileNumber));
              }

              return new Recipient(addresses, r.OrganizationNumber, r.NationalIdentityNumber);
          })
          .ToList();

        return new NotificationOrderRequest(
            extRequest.SendersReference,
            creator,
            [smsTemplate],
            extRequest.RequestedSendTime.ToUniversalTime(),
            NotificationChannel.Sms,
            recipients,
            extRequest.IgnoreReservation,
            extRequest.ResourceId,
            extRequest.ConditionEndpoint);
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrderWithStatus"/> to a <see cref="NotificationOrderWithStatusExt"/>
    /// </summary>
    public static NotificationOrderWithStatusExt MapToNotificationOrderWithStatusExt(this NotificationOrderWithStatus order)
    {
        var orderExt = new NotificationOrderWithStatusExt();
        orderExt.MapBaseNotificationOrder(order);

        orderExt.ProcessingStatus = new()
        {
            LastUpdate = order.ProcessingStatus.LastUpdate,
            Status = order.ProcessingStatus.Status.ToString(),
            StatusDescription = order.ProcessingStatus.StatusDescription
        };

        if (order.NotificationStatuses.Count != 0)
        {
            orderExt.NotificationsStatusSummary = new();
            foreach (var entry in order.NotificationStatuses)
            {
                NotificationTemplateType notificationType = entry.Key;
                NotificationStatus status = entry.Value;

                switch (notificationType)
                {
                    case NotificationTemplateType.Email:
                        orderExt.NotificationsStatusSummary.Email = new()
                        {
                            Generated = status.Generated,
                            Succeeded = status.Succeeded
                        };
                        break;
                    case NotificationTemplateType.Sms:
                        orderExt.NotificationsStatusSummary.Sms = new()
                        {
                            Generated = status.Generated,
                            Succeeded = status.Succeeded
                        };
                        break;
                }
            }

            orderExt.NotificationSummaryResourceLinks();
        }

        return orderExt;
    }

    /// <summary>
    /// Maps an <see cref="EmailNotificationOrderRequestExt"/> to an <see cref="EmailTemplateExt"/>.
    /// </summary>
    /// <param name="request">The email notification order request.</param>
    /// <returns>The mapped email template.</returns>
    public static EmailTemplateExt MapToEmailTemplateExt(this EmailNotificationOrderRequestExt request)
    {
        return new EmailTemplateExt
        {
            Body = request.Body,
            Subject = request.Subject,
            ContentType = request.ContentType ?? EmailContentTypeExt.Plain
        };
    }

    /// <summary>
    /// Maps an <see cref="SmsNotificationOrderRequestExt"/> to an <see cref="SmsTemplateExt"/>.
    /// </summary>
    /// <param name="request">The SMS notification order request.</param>
    /// <returns>The mapped SMS template.</returns>
    public static SmsTemplateExt MapToSmsTemplateExt(this SmsNotificationOrderRequestExt request)
    {
        return new SmsTemplateExt
        {
            Body = request.Body,
            SenderNumber = request.SenderNumber ?? string.Empty
        };
    }

    /// <summary>
    /// Maps a list of <see cref="NotificationOrder"/> to a <see cref="NotificationOrderListExt"/>
    /// </summary>
    public static NotificationOrderListExt MapToNotificationOrderListExt(this List<NotificationOrder> orders)
    {
        NotificationOrderListExt ordersExt = new()
        {
            Count = orders.Count
        };

        foreach (NotificationOrder order in orders)
        {
            ordersExt.Orders.Add(order.MapToNotificationOrderExt());
        }

        return ordersExt;
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrder"/> to a <see cref="NotificationOrderExt"/>
    /// </summary>
    public static NotificationOrderExt MapToNotificationOrderExt(this NotificationOrder order)
    {
        var orderExt = new NotificationOrderExt();

        orderExt.MapBaseNotificationOrder(order);
        orderExt.Recipients = order.Recipients.MapToRecipientExt();

        foreach (var template in order.Templates)
        {
            switch (template.Type)
            {
                case NotificationTemplateType.Email:
                    var emailTemplate = template! as EmailTemplate;

                    orderExt.EmailTemplate = new()
                    {
                        Body = emailTemplate!.Body,
                        FromAddress = emailTemplate.FromAddress,
                        ContentType = (EmailContentTypeExt)emailTemplate.ContentType,
                        Subject = emailTemplate.Subject
                    };

                    break;
                case NotificationTemplateType.Sms:
                    var smsTemplate = template! as SmsTemplate;
                    orderExt.SmsTemplate = new()
                    {
                        Body = smsTemplate!.Body,
                        SenderNumber = smsTemplate.SenderNumber
                    };
                    break;
            }
        }

        orderExt.SetResourceLinks();
        return orderExt;
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrderSequenceRequestExt"/> to a <see cref="NotificationOrder"/>.
    /// </summary>
    public static NotificationOrder MapToNotificationOrder(this NotificationOrderSequenceRequest request, string creator)
    {
        bool? ignoreReservation = null;
        List<Recipient> recipients = [];
        string? resouceIdentifier = null;
        List<INotificationTemplate> templates = [];
        NotificationChannel notificationChannel = NotificationChannel.Sms;

        if (request.Recipient.RecipientSms != null)
        {
            notificationChannel = NotificationChannel.Sms;
            templates.Add(request.Recipient.RecipientSms.MapToSmsTemplate());
            recipients = [new Recipient([new SmsAddressPoint(request.Recipient.RecipientSms.PhoneNumber)])];
        }
        else if (request.Recipient.RecipientEmail != null)
        {
            notificationChannel = NotificationChannel.Email;
            templates.Add(request.Recipient.RecipientEmail.MapToEmailTemplate());
            recipients = [new Recipient([new SmsAddressPoint(request.Recipient.RecipientEmail.EmailAddress)])];
        }
        else if (request.Recipient.RecipientPerson != null)
        {
            resouceIdentifier = request.Recipient.RecipientPerson.ResourceId;
            notificationChannel = request.Recipient.RecipientPerson.ChannelSchema;
            ignoreReservation = request.Recipient.RecipientPerson.IgnoreReservation;
            recipients = [new Recipient([], nationalIdentityNumber: request.Recipient.RecipientPerson.NationalIdentityNumber)];

            switch (request.Recipient.RecipientPerson.ChannelSchema)
            {
                case NotificationChannel.Sms:
                    if (request.Recipient.RecipientPerson.SmsSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientPerson.MapToSmsTemplate());
                    }

                    break;

                case NotificationChannel.Email:
                    if (request.Recipient.RecipientPerson.EmailSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientPerson.MapToEmailTemplate());
                    }

                    break;

                case NotificationChannel.SmsPreferred:
                case NotificationChannel.EmailPreferred:
                    if (request.Recipient.RecipientPerson.SmsSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientPerson.MapToSmsTemplate());
                    }

                    if (request.Recipient.RecipientPerson.EmailSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientPerson.MapToEmailTemplate());
                    }

                    break;
            }
        }
        else if (request.Recipient.RecipientOrganization != null)
        {
            resouceIdentifier = request.Recipient.RecipientOrganization.ResourceId;
            notificationChannel = request.Recipient.RecipientOrganization.ChannelSchema;
            recipients = [new Recipient([], nationalIdentityNumber: request.Recipient.RecipientOrganization.OrgNumber)];

            switch (request.Recipient.RecipientOrganization.ChannelSchema)
            {
                case NotificationChannel.Sms:
                    if (request.Recipient.RecipientOrganization.SmsSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientOrganization.MapToSmsTemplate());
                    }

                    break;

                case NotificationChannel.Email:
                    if (request.Recipient.RecipientOrganization.EmailSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientOrganization.MapToEmailTemplate());
                    }

                    break;

                case NotificationChannel.SmsPreferred:
                case NotificationChannel.EmailPreferred:
                    if (request.Recipient.RecipientOrganization.SmsSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientOrganization.MapToSmsTemplate());
                    }

                    if (request.Recipient.RecipientOrganization.EmailSettings != null)
                    {
                        templates.Add(request.Recipient.RecipientOrganization.MapToEmailTemplate());
                    }

                    break;
            }
        }

        return new NotificationOrder(
            request.OrderId,
            request.SendersReference,
            templates,
            request.RequestedSendTime,
            notificationChannel,
            new Creator(creator),
            DateTime.UtcNow,
            recipients,
            ignoreReservation,
            resouceIdentifier,
            request.ConditionEndpoint);
    }

    /// <summary>
    /// Maps reminders in a <see cref="NotificationOrderSequenceRequest"/> to a list of <see cref="NotificationOrder"/> objects.
    /// </summary>
    public static List<NotificationOrder> MapToNotificationOrders(this NotificationOrderSequenceRequest request, string creator)
    {
        List<NotificationOrder> notificationOrders = [];

        if (request.Reminders == null || request.Reminders.Count == 0)
        {
            return notificationOrders;
        }

        List<Recipient> recipients = [];

        foreach (var reminder in request.Reminders)
        {
            bool? ignoreReservation = null;
            string? resouceIdentifier = null;
            List<INotificationTemplate> templates = [];
            NotificationChannel notificationChannel = NotificationChannel.Sms;

            if (reminder.Recipient.RecipientSms != null)
            {
                notificationChannel = NotificationChannel.Sms;
                templates.Add(reminder.Recipient.RecipientSms.MapToSmsTemplate());
                recipients = [new Recipient([new SmsAddressPoint(reminder.Recipient.RecipientSms.PhoneNumber)])];
            }
            else if (reminder.Recipient.RecipientEmail != null)
            {
                notificationChannel = NotificationChannel.Email;
                templates.Add(reminder.Recipient.RecipientEmail.MapToEmailTemplate());
                recipients = [new Recipient([new SmsAddressPoint(reminder.Recipient.RecipientEmail.EmailAddress)])];
            }
            else if (reminder.Recipient.RecipientPerson != null)
            {
                resouceIdentifier = reminder.Recipient.RecipientPerson.ResourceId;
                notificationChannel = reminder.Recipient.RecipientPerson.ChannelSchema;
                ignoreReservation = reminder.Recipient.RecipientPerson.IgnoreReservation;
                recipients = [new Recipient([], nationalIdentityNumber: reminder.Recipient.RecipientPerson.NationalIdentityNumber)];

                switch (reminder.Recipient.RecipientPerson.ChannelSchema)
                {
                    case NotificationChannel.Sms:
                        if (reminder.Recipient.RecipientPerson.SmsSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientPerson.MapToSmsTemplate());
                        }

                        break;

                    case NotificationChannel.Email:
                        if (reminder.Recipient.RecipientPerson.EmailSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientPerson.MapToEmailTemplate());
                        }

                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        if (reminder.Recipient.RecipientPerson.SmsSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientPerson.MapToSmsTemplate());
                        }

                        if (reminder.Recipient.RecipientPerson.EmailSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientPerson.MapToEmailTemplate());
                        }

                        break;
                }

            }
            else if (reminder.Recipient.RecipientOrganization != null)
            {
                resouceIdentifier = reminder.Recipient.RecipientOrganization.ResourceId;
                notificationChannel = reminder.Recipient.RecipientOrganization.ChannelSchema;
                recipients = [new Recipient([], nationalIdentityNumber: reminder.Recipient.RecipientOrganization.OrgNumber)];

                switch (reminder.Recipient.RecipientOrganization.ChannelSchema)
                {
                    case NotificationChannel.Sms:
                        if (reminder.Recipient.RecipientOrganization.SmsSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientOrganization.MapToSmsTemplate());
                        }

                        break;

                    case NotificationChannel.Email:
                        if (reminder.Recipient.RecipientOrganization.EmailSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientOrganization.MapToEmailTemplate());
                        }

                        break;

                    case NotificationChannel.SmsPreferred:
                    case NotificationChannel.EmailPreferred:
                        if (reminder.Recipient.RecipientOrganization.SmsSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientOrganization.MapToSmsTemplate());
                        }

                        if (reminder.Recipient.RecipientOrganization.EmailSettings != null)
                        {
                            templates.Add(reminder.Recipient.RecipientOrganization.MapToEmailTemplate());
                        }

                        break;
                }
            }

            notificationOrders.Add(
                new NotificationOrder(
                    reminder.OrderId,
                    reminder.SendersReference,
                    templates,
                    reminder.RequestedSendTime,
                    notificationChannel,
                    new Creator(creator),
                    DateTime.UtcNow,
                    recipients,
                    ignoreReservation,
                    resouceIdentifier,
                    reminder.ConditionEndpoint));
        }

        return notificationOrders;
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrderSequenceRequestExt"/> to a <see cref="NotificationOrderSequenceRequest"/>.
    /// </summary>
    /// <param name="notificationOrderSequenceRequestExt">The request that contains a notification order and zero or more reminders.</param>
    /// <param name="creatorName">The creator of the notification request.</param>
    /// <returns>A notification order sequence request.</returns>
    public static NotificationOrderSequenceRequest MapToNotificationOrderSequenceRequest(this NotificationOrderSequenceRequestExt notificationOrderSequenceRequestExt, string creatorName)
    {
        // Map the recipient.
        var recipient = new RecipientSpecification
        {
            RecipientSms = notificationOrderSequenceRequestExt.Recipient.RecipientSms?.MapToRecipientSms(),
            RecipientEmail = notificationOrderSequenceRequestExt.Recipient.RecipientEmail?.MapToRecipientEmail(),
            RecipientPerson = notificationOrderSequenceRequestExt.Recipient.RecipientPerson?.MapToRecipientPerson(),
            RecipientOrganization = notificationOrderSequenceRequestExt.Recipient.RecipientOrganization?.MapToRecipientOrganization()
        };

        // Map the reminders and set their RequestedSendTime based on the main notification's requested time plus delay.
        var reminders = notificationOrderSequenceRequestExt.Reminders?
            .Select(reminder =>
            {
                // First map the reminder
                NotificationReminder mappedReminder = MapToNotificationReminder(reminder);
                mappedReminder.RequestedSendTime = notificationOrderSequenceRequestExt.RequestedSendTime.AddDays(mappedReminder.DelayDays).ToUniversalTime();

                return mappedReminder;
            }).ToList();

        DialogportenReference? dialogportenAssociation = notificationOrderSequenceRequestExt.DialogportenAssociation?.MapToDialogportenReference();

        return new NotificationOrderSequenceRequest(
            orderId: Guid.NewGuid(),
            creator: new Creator(creatorName),
            idempotencyId: notificationOrderSequenceRequestExt.IdempotencyId,
            recipient: recipient,
            conditionEndpoint: notificationOrderSequenceRequestExt.ConditionEndpoint,
            dialogportenAssociation: dialogportenAssociation,
            reminders: reminders,
            requestedSendTime: notificationOrderSequenceRequestExt.RequestedSendTime.ToUniversalTime(),
            sendersReference: notificationOrderSequenceRequestExt.SendersReference);
    }

    /// <summary>
    /// Maps a List of <see cref="Recipient"/> to a List of <see cref="RecipientExt"/>
    /// </summary>
    internal static List<RecipientExt> MapToRecipientExt(this List<Recipient> recipients)
    {
        var recipientExt = new List<RecipientExt>();

        recipientExt.AddRange(
            recipients.Select(r => new RecipientExt
            {
                EmailAddress = GetEmailFromAddressList(r.AddressInfo),
                MobileNumber = GetMobileNumberFromAddressList(r.AddressInfo),
                NationalIdentityNumber = r.NationalIdentityNumber,
                OrganizationNumber = r.OrganizationNumber,
                IsReserved = r.IsReserved
            }));

        return recipientExt;
    }

    /// <summary>
    /// Maps a <see cref="NotificationReminderExt"/> to a <see cref="NotificationReminder"/>.
    /// </summary>
    private static NotificationReminder MapToNotificationReminder(this NotificationReminderExt notificationReminderExt)
    {
        return new()
        {
            Recipient = new RecipientSpecification
            {
                RecipientSms = notificationReminderExt.Recipient.RecipientSms?.MapToRecipientSms(),
                RecipientEmail = notificationReminderExt.Recipient.RecipientEmail?.MapToRecipientEmail(),
                RecipientPerson = notificationReminderExt.Recipient.RecipientPerson?.MapToRecipientPerson(),
                RecipientOrganization = notificationReminderExt.Recipient.RecipientOrganization?.MapToRecipientOrganization()
            },

            OrderId = Guid.NewGuid(),
            DelayDays = notificationReminderExt.DelayDays,
            SendersReference = notificationReminderExt.SendersReference,
            ConditionEndpoint = notificationReminderExt.ConditionEndpoint
        };
    }

    /// <summary>
    /// Maps a <see cref="BaseNotificationOrderExt"/> to a <see cref="BaseNotificationOrderExt"/>.
    /// </summary>
    private static BaseNotificationOrderExt MapBaseNotificationOrder(this BaseNotificationOrderExt orderExt, IBaseNotificationOrder order)
    {
        orderExt.Id = order.Id.ToString();
        orderExt.SendersReference = order.SendersReference;
        orderExt.Created = order.Created;
        orderExt.Creator = order.Creator.ShortName;
        orderExt.NotificationChannel = (NotificationChannelExt)order.NotificationChannel;
        orderExt.RequestedSendTime = order.RequestedSendTime;
        orderExt.IgnoreReservation = order.IgnoreReservation;
        orderExt.ResourceId = order.ResourceId;
        orderExt.ConditionEndpoint = order.ConditionEndpoint;

        return orderExt;
    }

    /// <summary>
    /// Maps a <see cref="DialogportenReferenceExt"/> to a <see cref="DialogportenReference"/>.
    /// </summary>
    private static DialogportenReference? MapToDialogportenReference(this DialogportenReferenceExt dialogportenReferenceExt)
    {
        return new DialogportenReference
        {
            DialogId = dialogportenReferenceExt.DialogId,
            TransmissionId = dialogportenReferenceExt.TransmissionId
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientEmail"/> to a <see cref="EmailTemplate"/>.
    /// </summary>
    private static EmailTemplate MapToEmailTemplate(this RecipientEmail recipientEmail)
    {
        return new EmailTemplate(
            recipientEmail.Settings.SenderEmailAddress,
            recipientEmail.Settings.Subject,
            recipientEmail.Settings.Body,
            recipientEmail.Settings.ContentType);
    }

    /// <summary>
    /// Maps a <see cref="RecipientPerson"/> to a <see cref="EmailTemplate"/>.
    /// </summary>
    private static EmailTemplate MapToEmailTemplate(this RecipientPerson recipientPerson)
    {
        return new EmailTemplate(
            recipientPerson.EmailSettings!.SenderEmailAddress,
            recipientPerson.EmailSettings!.Subject,
            recipientPerson.EmailSettings!.Body,
            recipientPerson.EmailSettings!.ContentType);
    }

    /// <summary>
    /// Maps a <see cref="RecipientOrganization"/> to a <see cref="EmailTemplate"/>.
    /// </summary>
    private static EmailTemplate MapToEmailTemplate(this RecipientOrganization recipientOrganization)
    {
        return new EmailTemplate(
            recipientOrganization.EmailSettings!.SenderEmailAddress,
            recipientOrganization.EmailSettings!.Subject,
            recipientOrganization.EmailSettings!.Body,
            recipientOrganization.EmailSettings!.ContentType);
    }

    /// <summary>
    /// Maps a <see cref="EmailSendingOptionsExt"/> to a <see cref="EmailSendingOptions"/>.
    /// </summary>
    private static EmailSendingOptions MapToEmailSendingOptions(this EmailSendingOptionsExt emailSendingOptionsExt)
    {
        return new EmailSendingOptions
        {
            Body = emailSendingOptionsExt.Body,
            Subject = emailSendingOptionsExt.Subject,
            SenderName = emailSendingOptionsExt.SenderName,
            SenderEmailAddress = emailSendingOptionsExt.SenderEmailAddress,
            ContentType = (EmailContentType)emailSendingOptionsExt.ContentType,
            SendingTimePolicy = (SendingTimePolicy)emailSendingOptionsExt.SendingTimePolicy
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientSms"/> to a <see cref="SmsTemplate"/>.
    /// </summary>
    private static SmsTemplate MapToSmsTemplate(this RecipientSms recipientSms)
    {
        return new SmsTemplate(recipientSms.Settings.Sender, recipientSms.Settings.Body);
    }

    /// <summary>
    /// Maps a <see cref="RecipientPerson"/> to a <see cref="SmsTemplate"/>.
    /// </summary>
    private static SmsTemplate MapToSmsTemplate(this RecipientPerson recipientPerson)
    {
        return new SmsTemplate(recipientPerson.SmsSettings!.Sender, recipientPerson.SmsSettings!.Body);
    }

    /// <summary>
    /// Maps a <see cref="RecipientOrganization"/> to a <see cref="SmsTemplate"/>.
    /// </summary>
    private static SmsTemplate MapToSmsTemplate(this RecipientOrganization recipientOrganization)
    {
        return new SmsTemplate(recipientOrganization.SmsSettings!.Sender, recipientOrganization.SmsSettings!.Body);
    }

    /// <summary>
    /// Maps a <see cref="SmsSendingOptionsExt"/> to a <see cref="SmsSendingOptions"/>.
    /// </summary>
    private static SmsSendingOptions MapToSmsSendingOptions(this SmsSendingOptionsExt smsSendingOptionsExt)
    {
        return new SmsSendingOptions
        {
            Body = smsSendingOptionsExt.Body,
            Sender = smsSendingOptionsExt.Sender,
            SendingTimePolicy = (SendingTimePolicy)smsSendingOptionsExt.SendingTimePolicy
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientEmailExt"/> to a <see cref="RecipientEmail"/>.
    /// </summary>
    private static RecipientEmail? MapToRecipientEmail(this RecipientEmailExt recipientEmailExt)
    {
        if (recipientEmailExt.Settings == null)
        {
            return null;
        }

        return new RecipientEmail
        {
            EmailAddress = recipientEmailExt.EmailAddress,
            Settings = recipientEmailExt.Settings.MapToEmailSendingOptions()
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientPersonExt"/> to a <see cref="RecipientPerson"/>.
    /// </summary>
    private static RecipientPerson? MapToRecipientPerson(this RecipientPersonExt recipientPersonExt)
    {
        var smsSettings = recipientPersonExt.SmsSettings?.MapToSmsSendingOptions();
        var emailSettings = recipientPersonExt.EmailSettings?.MapToEmailSendingOptions();

        return (smsSettings is null && emailSettings is null) ? null : new RecipientPerson
        {
            SmsSettings = smsSettings,
            EmailSettings = emailSettings,
            ResourceId = recipientPersonExt.ResourceId,
            IgnoreReservation = recipientPersonExt.IgnoreReservation,
            NationalIdentityNumber = recipientPersonExt.NationalIdentityNumber,
            ChannelSchema = (NotificationChannel)recipientPersonExt.ChannelSchema
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientOrganizationExt"/> to a <see cref="RecipientOrganization"/>.
    /// </summary>
    private static RecipientOrganization? MapToRecipientOrganization(this RecipientOrganizationExt recipientOrganizationExt)
    {
        var smsSettings = recipientOrganizationExt.SmsSettings?.MapToSmsSendingOptions();
        var emailSettings = recipientOrganizationExt.EmailSettings?.MapToEmailSendingOptions();

        return (smsSettings is null && emailSettings is null) ? null : new RecipientOrganization
        {
            SmsSettings = smsSettings,
            EmailSettings = emailSettings,
            OrgNumber = recipientOrganizationExt.OrgNumber,
            ResourceId = recipientOrganizationExt.ResourceId,
            ChannelSchema = (NotificationChannel)recipientOrganizationExt.ChannelSchema
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientSmsExt"/> to a <see cref="RecipientSms"/>.
    /// </summary>
    private static RecipientSms? MapToRecipientSms(this RecipientSmsExt recipientSmsExt)
    {
        if (recipientSmsExt.Settings == null)
        {
            return null;
        }

        return new RecipientSms
        {
            PhoneNumber = recipientSmsExt.PhoneNumber,
            Settings = recipientSmsExt.Settings.MapToSmsSendingOptions()
        };
    }

    private static string? GetEmailFromAddressList(List<IAddressPoint> addressPoints)
    {
        var emailAddressPoint = addressPoints
            .Find(a => a.AddressType.Equals(AddressType.Email))
            as EmailAddressPoint;

        return emailAddressPoint?.EmailAddress;
    }

    private static string? GetMobileNumberFromAddressList(List<IAddressPoint> addressPoints)
    {
        var smsAddressPoint = addressPoints
            .Find(a => a.AddressType.Equals(AddressType.Sms))
            as SmsAddressPoint;

        return smsAddressPoint?.MobileNumber;
    }
}
