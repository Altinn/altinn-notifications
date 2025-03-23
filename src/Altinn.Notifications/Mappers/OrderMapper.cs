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
    /// Maps the main notification order defined in a <see cref="NotificationOrderChainRequestExt"/> to a <see cref="NotificationOrder"/>.
    /// </summary>
    public static NotificationOrder MapToNotificationOrder(this NotificationOrderChainRequest request, string creator)
    {
        var (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier) = MapRecipientAndTemplates(request.Recipient);

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
            resourceIdentifier,
            request.ConditionEndpoint);
    }

    /// <summary>
    /// Maps reminders defined in a <see cref="NotificationOrderChainRequest"/> to a list of <see cref="NotificationOrder"/> objects.
    /// </summary>
    public static List<NotificationOrder> MapToNotificationOrders(this NotificationOrderChainRequest request, string creator)
    {
        if (request.Reminders == null || request.Reminders.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var creatorObject = new Creator(creator);

        return [.. request.Reminders
            .Select(reminder =>
            {
                var (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier) = MapRecipientAndTemplates(reminder.Recipient);

                return new NotificationOrder(
                    reminder.OrderId,
                    reminder.SendersReference,
                    templates,
                    reminder.RequestedSendTime,
                    notificationChannel,
                    creatorObject,
                    now,
                    recipients,
                    ignoreReservation,
                    resourceIdentifier,
                    reminder.ConditionEndpoint);
            })];
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrderChainRequestExt"/> to a <see cref="NotificationOrderChainRequest"/>.
    /// </summary>
    /// <param name="notificationOrderChainRequestExt">The request that contains a notification order and zero or more reminders.</param>
    /// <param name="creatorName">The name of the person or entity who created the notification request.</param>
    /// <returns>A <see cref="NotificationOrderChainRequest"/> object mapped from the provided notification order chain request.</returns>
    public static NotificationOrderChainRequest MapToNotificationOrderChainRequest(this NotificationOrderChainRequestExt notificationOrderChainRequestExt, string creatorName)
    {
        // Map the recipient.
        var recipient = new NotificationRecipient
        {
            RecipientSms = notificationOrderChainRequestExt.Recipient.RecipientSms?.MapToRecipientSms(),
            RecipientEmail = notificationOrderChainRequestExt.Recipient.RecipientEmail?.MapToRecipientEmail(),
            RecipientPerson = notificationOrderChainRequestExt.Recipient.RecipientPerson?.MapToRecipientPerson(),
            RecipientOrganization = notificationOrderChainRequestExt.Recipient.RecipientOrganization?.MapToRecipientOrganization()
        };

        // Map the reminders and set their RequestedSendTime based on the main notification's requested time plus the delay.
        var reminders = notificationOrderChainRequestExt.Reminders?
            .Select(reminder =>
            {
                // Calculate the send time for each reminder.
                var requestedSendTime = notificationOrderChainRequestExt.RequestedSendTime.AddDays(reminder.DelayDays).ToUniversalTime();

                // Map the reminder with the calculated send time.
                return reminder.MapToNotificationReminder(requestedSendTime);
            })
            .ToList();

        DialogportenIdentifiers? dialogportenAssociation = notificationOrderChainRequestExt.DialogportenAssociation?.MapToDialogportenReference();

        return new NotificationOrderChainRequest(
            Guid.NewGuid(),
            new Creator(creatorName),
            notificationOrderChainRequestExt.IdempotencyId,
            recipient,
            notificationOrderChainRequestExt.ConditionEndpoint,
            dialogportenAssociation,
            reminders,
            notificationOrderChainRequestExt.RequestedSendTime.ToUniversalTime(),
            notificationOrderChainRequestExt.SendersReference);

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
    /// Maps recipient and templates from the given notification recipient.
    /// </summary>
    private static (List<Recipient> Recipients, List<INotificationTemplate> Templates, NotificationChannel NotificationChannel, bool? IgnoreReservation, string? ResourceIdentifier) MapRecipientAndTemplates(NotificationRecipient recipient)
    {
        // Initialize default values
        bool? ignoreReservation = null;
        List<Recipient> recipients = [];
        string? resourceIdentifier = null;
        List<INotificationTemplate> templates = [];
        NotificationChannel notificationChannel = NotificationChannel.Sms;

        // Handle different recipient types
        if (recipient.RecipientSms?.Settings != null)
        {
            notificationChannel = NotificationChannel.Sms;
            recipients.Add(CreateSmsRecipient(recipient.RecipientSms));
            templates.Add(CreateSmsTemplate(recipient.RecipientSms.Settings));
        }
        else if (recipient.RecipientEmail?.Settings != null)
        {
            notificationChannel = NotificationChannel.Email;
            recipients.Add(CreateEmailRecipient(recipient.RecipientEmail));
            templates.Add(CreateEmailTemplate(recipient.RecipientEmail.Settings));
        }
        else if (recipient.RecipientPerson != null)
        {
            notificationChannel = recipient.RecipientPerson.ChannelSchema;
            resourceIdentifier = recipient.RecipientPerson.ResourceId;
            ignoreReservation = recipient.RecipientPerson.IgnoreReservation;
            recipients.Add(CreatePersonRecipient(recipient.RecipientPerson));

            AddTemplatesForPerson(recipient.RecipientPerson, templates);
        }
        else if (recipient.RecipientOrganization != null)
        {
            notificationChannel = recipient.RecipientOrganization.ChannelSchema;
            resourceIdentifier = recipient.RecipientOrganization.ResourceId;
            recipients.Add(CreateOrganizationRecipient(recipient.RecipientOrganization));

            AddTemplatesForOrganization(recipient.RecipientOrganization, templates);
        }

        return (recipients, templates, notificationChannel, ignoreReservation, resourceIdentifier);
    }

    /// <summary>
    /// Creates a recipient for SMS notifications.
    /// </summary>
    private static Recipient CreateSmsRecipient(RecipientSms recipientSms)
    {
        return new Recipient([new SmsAddressPoint(recipientSms.PhoneNumber)]);
    }

    /// <summary>
    /// Creates a recipient for Email notifications.
    /// </summary>
    private static Recipient CreateEmailRecipient(RecipientEmail recipientEmail)
    {
        return new Recipient([new EmailAddressPoint(recipientEmail.EmailAddress)]);
    }

    /// <summary>
    /// Creates a recipient for sending notifications to a person identified by their national identity number.
    /// </summary>
    private static Recipient CreatePersonRecipient(RecipientPerson recipientPerson)
    {
        return new Recipient([], nationalIdentityNumber: recipientPerson.NationalIdentityNumber);
    }

    /// <summary>
    /// Creates a recipient for sending notifications to an organization's contact person.
    /// </summary>
    private static Recipient CreateOrganizationRecipient(RecipientOrganization recipientOrganization)
    {
        return new Recipient([], organizationNumber: recipientOrganization.OrgNumber);
    }

    /// <summary>
    /// Creates an SMS template from SMS settings.
    /// </summary>
    private static SmsTemplate CreateSmsTemplate(SmsSendingOptions smsSettings)
    {
        return new SmsTemplate(smsSettings.Sender, smsSettings.Body);
    }

    /// <summary>
    /// Creates an Email template from Email settings.
    /// </summary>
    private static EmailTemplate CreateEmailTemplate(EmailSendingOptions emailSettings)
    {
        return new EmailTemplate(
            emailSettings.SenderEmailAddress,
            emailSettings.Subject,
            emailSettings.Body,
            emailSettings.ContentType);
    }

    /// <summary>
    /// Adds templates for person notifications based on available settings.
    /// </summary>
    private static void AddTemplatesForPerson(RecipientPerson person, List<INotificationTemplate> templates)
    {
        if (person.SmsSettings != null)
        {
            templates.Add(CreateSmsTemplate(person.SmsSettings));
        }

        if (person.EmailSettings != null)
        {
            templates.Add(CreateEmailTemplate(person.EmailSettings));
        }
    }

    /// <summary>
    /// Adds templates for organization notifications based on available settings.
    /// </summary>
    private static void AddTemplatesForOrganization(RecipientOrganization organization, List<INotificationTemplate> templates)
    {
        if (organization.SmsSettings != null)
        {
            templates.Add(CreateSmsTemplate(organization.SmsSettings));
        }

        if (organization.EmailSettings != null)
        {
            templates.Add(CreateEmailTemplate(organization.EmailSettings));
        }
    }

    /// <summary>
    /// Maps a <see cref="NotificationReminderExt"/> to a <see cref="NotificationReminder"/>.
    /// </summary>
    /// <param name="notificationReminderExt">The extended notification reminder object to map from.</param>
    /// <param name="requestedSendTime">The requested send time for the reminder.</param>
    /// <returns>A <see cref="NotificationReminder"/> object mapped from the provided notification reminder.</returns>
    private static NotificationReminder MapToNotificationReminder(this NotificationReminderExt notificationReminderExt, DateTime requestedSendTime)
    {
        return new()
        {
            Recipient = new NotificationRecipient
            {
                RecipientSms = notificationReminderExt.Recipient.RecipientSms?.MapToRecipientSms(),
                RecipientEmail = notificationReminderExt.Recipient.RecipientEmail?.MapToRecipientEmail(),
                RecipientPerson = notificationReminderExt.Recipient.RecipientPerson?.MapToRecipientPerson(),
                RecipientOrganization = notificationReminderExt.Recipient.RecipientOrganization?.MapToRecipientOrganization()
            },

            OrderId = Guid.NewGuid(),
            RequestedSendTime = requestedSendTime,
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
    /// Maps a <see cref="DialogportenIdentifiersExt"/> to a <see cref="DialogportenIdentifiers"/>.
    /// </summary>
    private static DialogportenIdentifiers? MapToDialogportenReference(this DialogportenIdentifiersExt dialogportenReferenceExt)
    {
        return new DialogportenIdentifiers
        {
            DialogId = dialogportenReferenceExt.DialogId,
            TransmissionId = dialogportenReferenceExt.TransmissionId
        };
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
