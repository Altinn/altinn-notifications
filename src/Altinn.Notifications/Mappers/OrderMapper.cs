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
        string? resouceIdentifier = null;
        List<INotificationTemplate> templates = [];
        NotificationChannel notificationChannel = NotificationChannel.Sms;

        if (request.Recipient.RecipientSms != null)
        {
            notificationChannel = NotificationChannel.Sms;
            templates.Add(request.Recipient.RecipientSms.MapToSmsTemplate());
        }
        else if (request.Recipient.RecipientEmail != null)
        {
            notificationChannel = NotificationChannel.Email;
            templates.Add(request.Recipient.RecipientEmail.MapToEmailTemplate());
        }
        else if (request.Recipient.RecipientPerson != null)
        {
            resouceIdentifier = request.Recipient.RecipientPerson.ResourceId;
            ignoreReservation = request.Recipient.RecipientPerson.IgnoreReservation;
            notificationChannel = request.Recipient.RecipientPerson.ChannelSchema;

            switch (request.Recipient.RecipientPerson.ChannelSchema)
            {
                case NotificationChannel.Sms:
                case NotificationChannel.SmsPreferred:
                    var smsTemplate = request.Recipient.RecipientPerson.MapToSmsTemplate();
                    if (smsTemplate != null)
                    {
                        templates.Add(smsTemplate);
                    }

                    break;

                case NotificationChannel.Email:
                case NotificationChannel.EmailPreferred:
                    var emailTemplate = request.Recipient.RecipientPerson.MapToEmailTemplate();
                    if (emailTemplate != null)
                    {
                        templates.Add(emailTemplate);
                    }

                    break;
            }
        }
        else if (request.Recipient.RecipientOrganization != null)
        {
            resouceIdentifier = request.Recipient.RecipientOrganization.ResourceId;
            notificationChannel = request.Recipient.RecipientOrganization.ChannelSchema;

            switch (request.Recipient.RecipientOrganization.ChannelSchema)
            {
                case NotificationChannel.Sms:
                case NotificationChannel.SmsPreferred:
                    var smsTemplate = request.Recipient.RecipientOrganization.MapToSmsTemplate();
                    if (smsTemplate != null)
                    {
                        templates.Add(smsTemplate);
                    }

                    break;

                case NotificationChannel.Email:
                case NotificationChannel.EmailPreferred:
                    var emailTemplate = request.Recipient.RecipientOrganization.MapToEmailTemplate();
                    if (emailTemplate != null)
                    {
                        templates.Add(emailTemplate);
                    }

                    break;
            }
        }

        var recipient = request.Recipient.MapToRecipient();

        return new NotificationOrder(
            Guid.Empty,
            request.SendersReference,
            templates,
            request.RequestedSendTime,
            notificationChannel,
            new Creator(creator),
            DateTime.Now,
            [recipient],
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
            }
            else if (reminder.Recipient.RecipientEmail != null)
            {
                notificationChannel = NotificationChannel.Email;
                templates.Add(reminder.Recipient.RecipientEmail.MapToEmailTemplate());
            }
            else if (reminder.Recipient.RecipientPerson != null)
            {
                resouceIdentifier = reminder.Recipient.RecipientPerson.ResourceId;
                ignoreReservation = reminder.Recipient.RecipientPerson.IgnoreReservation;
                notificationChannel = reminder.Recipient.RecipientPerson.ChannelSchema;

                switch (reminder.Recipient.RecipientPerson.ChannelSchema)
                {
                    case NotificationChannel.Sms:
                    case NotificationChannel.SmsPreferred:
                        var smsTemplate = reminder.Recipient.RecipientPerson.MapToSmsTemplate();
                        if (smsTemplate != null)
                        {
                            templates.Add(smsTemplate);
                        }

                        break;

                    case NotificationChannel.Email:
                    case NotificationChannel.EmailPreferred:
                        var emailTemplate = reminder.Recipient.RecipientPerson.MapToEmailTemplate();
                        if (emailTemplate != null)
                        {
                            templates.Add(emailTemplate);
                        }

                        break;
                }
            }
            else if (reminder.Recipient.RecipientOrganization != null)
            {
                resouceIdentifier = reminder.Recipient.RecipientOrganization.ResourceId;
                notificationChannel = reminder.Recipient.RecipientOrganization.ChannelSchema;

                switch (reminder.Recipient.RecipientOrganization.ChannelSchema)
                {
                    case NotificationChannel.Sms:
                    case NotificationChannel.SmsPreferred:
                        var smsTemplate = reminder.Recipient.RecipientOrganization.MapToSmsTemplate();
                        if (smsTemplate != null)
                        {
                            templates.Add(smsTemplate);
                        }

                        break;

                    case NotificationChannel.Email:
                    case NotificationChannel.EmailPreferred:
                        var emailTemplate = reminder.Recipient.RecipientOrganization.MapToEmailTemplate();
                        if (emailTemplate != null)
                        {
                            templates.Add(emailTemplate);
                        }

                        break;
                }
            }

            var recipient = reminder.Recipient.MapToRecipient();

            notificationOrders.Add(
                new NotificationOrder(
                    Guid.Empty,
                    reminder.SendersReference,
                    templates,
                    request.RequestedSendTime.AddDays(reminder.DelayDays),
                    notificationChannel,
                    new Creator(creator),
                    DateTime.Now,
                    [recipient],
                    ignoreReservation,
                    resouceIdentifier,
                    reminder.ConditionEndpoint));
        }

        return notificationOrders;
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrderSequenceRequestExt"/> to a <see cref="NotificationOrderSequenceRequest"/>.
    /// </summary>
    /// <param name="notificationOrderSequenceRequestExt">The request that contains a notification order and non or more reminders.</param>
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
            creator: new Creator(creatorName),
            idempotencyId: notificationOrderSequenceRequestExt.IdempotencyId,
            recipient: recipient,
            reminders: reminders,
            conditionEndpoint: notificationOrderSequenceRequestExt.ConditionEndpoint,
            dialogportenAssociation: dialogportenAssociation,
            requestedSendTime: notificationOrderSequenceRequestExt.RequestedSendTime.ToUniversalTime(),
            sendersReference: notificationOrderSequenceRequestExt.SendersReference);
    }

    /// <summary>
    /// Maps a <see cref="NotificationReminderExt"/> to a <see cref="NotificationReminder"/>.
    /// </summary>
    public static NotificationReminder MapToNotificationReminder(this NotificationReminderExt notificationReminderExt)
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

            DelayDays = notificationReminderExt.DelayDays,
            SendersReference = notificationReminderExt.SendersReference,
            ConditionEndpoint = notificationReminderExt.ConditionEndpoint
        };
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
    private static EmailTemplate? MapToEmailTemplate(this RecipientPerson recipientPerson)
    {
        if (recipientPerson.EmailSettings == null)
        {
            return null;
        }

        return new EmailTemplate(
            recipientPerson.EmailSettings.SenderEmailAddress,
            recipientPerson.EmailSettings.Subject,
            recipientPerson.EmailSettings.Body,
            recipientPerson.EmailSettings.ContentType);
    }

    /// <summary>
    /// Maps a <see cref="RecipientOrganization"/> to a <see cref="EmailTemplate"/>.
    /// </summary>
    /// <param name="recipientOrganization">The external organization recipient model.</param>
    /// <returns>The mapped internal model.</returns>
    private static EmailTemplate? MapToEmailTemplate(this RecipientOrganization recipientOrganization)
    {
        if (recipientOrganization.EmailSettings == null)
        {
            return null;
        }

        return new EmailTemplate(
            recipientOrganization.EmailSettings.SenderEmailAddress,
            recipientOrganization.EmailSettings.Subject,
            recipientOrganization.EmailSettings.Body,
            recipientOrganization.EmailSettings.ContentType);
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
        if (recipientSms.Settings == null)
        {
            return null;
        }

        return new SmsTemplate(recipientSms.Settings.Sender, recipientSms.Settings.Body);
    }

    /// <summary>
    /// Maps a <see cref="RecipientPerson"/> to a <see cref="SmsTemplate"/>.
    /// </summary>
    private static SmsTemplate? MapToSmsTemplate(this RecipientPerson recipientPerson)
    {
        if (recipientPerson.SmsSettings == null)
        {
            return null;
        }

        return new SmsTemplate(recipientPerson.SmsSettings.Sender, recipientPerson.SmsSettings.Body);
    }

    /// <summary>
    /// Maps a <see cref="RecipientOrganization"/> to a <see cref="SmsTemplate"/>.
    /// </summary>
    private static SmsTemplate? MapToSmsTemplate(this RecipientOrganization recipientOrganization)
    {
        if (recipientOrganization.SmsSettings == null)
        {
            return null;
        }

        return new SmsTemplate(recipientOrganization.SmsSettings.Sender, recipientOrganization.SmsSettings.Body);
    }

    /// <summary>
    /// Maps a <see cref="SmsSendingOptionsExt"/> to a <see cref="SmsSendingOptions"/>.
    /// </summary>
    public static SmsSendingOptions MapToSmsSendingOptions(this SmsSendingOptionsExt smsSendingOptionsExt)
    {
        return new SmsSendingOptions
        {
            Body = smsSendingOptionsExt.Body,
            Sender = smsSendingOptionsExt.Sender,
            SendingTimePolicy = (SendingTimePolicy)smsSendingOptionsExt.SendingTimePolicy
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientSpecification"/> to a <see cref="Recipient"/>.
    /// </summary>
    /// <param name="recipientSpecification">The external recipient specification model.</param>
    /// <returns>The mapped internal model.</returns>
    private static Recipient? MapToRecipient(this RecipientSpecification recipientSpecification)
    {
        if (recipientSpecification.RecipientSms != null)
        {
            return new Recipient([new SmsAddressPoint(recipientSpecification.RecipientSms.PhoneNumber)]);
        }
        else if (recipientSpecification.RecipientEmail != null)
        {
            return new Recipient([new EmailAddressPoint(recipientSpecification.RecipientEmail.EmailAddress)]);
        }
        else if (recipientSpecification.RecipientPerson != null)
        {
            return new Recipient([], nationalIdentityNumber: recipientSpecification.RecipientPerson.NationalIdentityNumber);
        }
        else if (recipientSpecification.RecipientOrganization != null)
        {
            return new Recipient([], organizationNumber: recipientSpecification.RecipientOrganization.OrgNumber);
        }

        return null;
    }

    /// <summary>
    /// Maps a <see cref="RecipientEmailExt"/> to a <see cref="RecipientEmail"/>.
    /// </summary>
    public static RecipientEmail? MapToRecipientEmail(this RecipientEmailExt recipientEmailExt)
    {
        if (recipientEmailExt.Settings == null)
        {
            return null;
        }

        return new RecipientEmail
        {
            EmailAddress = recipientEmailExt.EmailAddress,
            Settings = recipientEmailExt.Settings.MapToEmailSendingOptions()!
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientPersonExt"/> to a <see cref="RecipientPerson"/>.
    /// </summary>
    public static RecipientPerson? MapToRecipientPerson(this RecipientPersonExt recipientPersonExt)
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
    public static RecipientOrganization? MapToRecipientOrganization(this RecipientOrganizationExt recipientOrganizationExt)
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
            Settings = recipientSmsExt.Settings.MapToSmsSendingOptions()!
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
