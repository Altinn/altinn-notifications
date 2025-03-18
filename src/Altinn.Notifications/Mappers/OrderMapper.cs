﻿using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Mapper for <see cref="EmailNotificationOrderRequestExt"/>
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
    /// Maps a <see cref="NotificationOrderWithRemindersRequestExt"/> to a <see cref="NotificationOrderWithRemindersRequest"/>
    /// </summary>
    public static NotificationOrderWithRemindersRequest MapToOrderWithRemindersRequest(this NotificationOrderWithRemindersRequestExt extRequest)
    {
        return new NotificationOrderWithRemindersRequest
        {
            IdempotencyId = extRequest.IdempotencyId,
            SendersReference = extRequest.SendersReference,
            ConditionEndpoint = extRequest.ConditionEndpoint,
            RequestedSendTime = extRequest.RequestedSendTime.ToUniversalTime(),

            DialogportenAssociation = MapDialogportenAssociation(extRequest.DialogportenAssociation),

            Recipient = new AssociatedRecipients
            {
                RecipientSms = MapSmsRecipientPayload(extRequest.Recipient.RecipientSms),
                RecipientEmail = MapEmailRecipientPayload(extRequest.Recipient.RecipientEmail),
                RecipientPerson = MapPersonRecipientPayload(extRequest.Recipient.RecipientPerson),
                RecipientOrganization = MapOrganizationRecipientPayload(extRequest.Recipient.RecipientOrganization)
            },

            Reminders = extRequest.Reminders?.Select(MapNotificationReminders).ToList()
        };
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

    private static DialogportenAssociation? MapDialogportenAssociation(DialogportenAssociationExt? source)
    {
        return source is null ? null : new DialogportenAssociation
        {
            DialogId = source.DialogId,
            TransmissionId = source.TransmissionId
        };
    }

    private static SmsRecipientPayload? MapSmsRecipientPayload(RecipientSmsRequestExt? source)
    {
        return source?.Settings is null ? null : new SmsRecipientPayload
        {
            PhoneNumber = source.PhoneNumber,
            Settings = new SmsRecipientPayloadSettings
            {
                Body = source.Settings.Body,
                SenderNumber = source.Settings.SenderNumber,
                SendingTimePolicy = (SendingTimePolicy)source.Settings.SendingTimePolicy
            }
        };
    }

    private static EmailRecipientPayload? MapEmailRecipientPayload(RecipientEmailRequestExt? source)
    {
        return source?.Settings is null ? null : new EmailRecipientPayload
        {
            EmailAddress = source.EmailAddress,
            Settings = new EmailRecipientPayloadSettings
            {
                Body = source.Settings.Body,
                Subject = source.Settings.Subject,
                SenderName = source.Settings.SenderName,
                SenderEmailAddress = source.Settings.SenderEmailAddress,
                ContentType = (EmailContentType)source.Settings.ContentType,
                SendingTimePolicy = (SendingTimePolicy)source.Settings.SendingTimePolicy
            }
        };
    }

    private static PersonRecipientPayload? MapPersonRecipientPayload(RecipientPersonRequestExt? source)
    {
        if (source is null)
        {
            return null;
        }

        var smsSettings = MapSmsRecipientPayloadSettings(source.SmsSettings);
        var emailSettings = MapEmailRecipientPayloadSettings(source.EmailSettings);

        return (smsSettings is null && emailSettings is null) ? null : new PersonRecipientPayload
        {
            SmsSettings = smsSettings,
            EmailSettings = emailSettings,
            ResourceId = source.ResourceId,
            IgnoreReservation = source.IgnoreReservation,
            NationalIdentityNumber = source.NationalIdentityNumber,
            ChannelScheme = (NotificationChannel)source.ChannelScheme
        };
    }

    private static OrganizationRecipientPayload? MapOrganizationRecipientPayload(RecipientOrganizationRequestExt? source)
    {
        if (source is null)
        {
            return null;
        }

        var smsSettings = MapSmsRecipientPayloadSettings(source.SmsSettings);
        var emailSettings = MapEmailRecipientPayloadSettings(source.EmailSettings);

        return (smsSettings is null && emailSettings is null) ? null : new OrganizationRecipientPayload
        {
            SmsSettings = smsSettings,
            EmailSettings = emailSettings,
            OrgNumber = source.OrgNumber,
            ResourceId = source.ResourceId,
            ChannelScheme = (NotificationChannel)source.ChannelScheme
        };
    }

    private static SmsRecipientPayloadSettings? MapSmsRecipientPayloadSettings(RecipientSmsSettingsRequestExt? source)
    {
        return source is null ? null : new SmsRecipientPayloadSettings
        {
            Body = source.Body,
            SenderNumber = source.SenderNumber,
            SendingTimePolicy = (SendingTimePolicy)source.SendingTimePolicy
        };
    }

    private static EmailRecipientPayloadSettings? MapEmailRecipientPayloadSettings(RecipientEmailSettingsRequestExt? source)
    {
        return source is null ? null : new EmailRecipientPayloadSettings
        {
            Body = source.Body,
            Subject = source.Subject,
            SenderName = source.SenderName,
            SenderEmailAddress = source.SenderEmailAddress,
            ContentType = (EmailContentType)source.ContentType,
            SendingTimePolicy = (SendingTimePolicy)source.SendingTimePolicy
        };
    }

    private static NotificationReminder MapNotificationReminders(NotificationOrderReminderRequestExt source)
    {
        return new()
        {
            Recipient = new AssociatedRecipients
            {
                RecipientSms = MapSmsRecipientPayload(source.Recipient.RecipientSms),
                RecipientEmail = MapEmailRecipientPayload(source.Recipient.RecipientEmail),
                RecipientPerson = MapPersonRecipientPayload(source.Recipient.RecipientPerson),
                RecipientOrganization = MapOrganizationRecipientPayload(source.Recipient.RecipientOrganization)
            },
            DelayDays = source.DelayDays,
            SendersReference = source.SendersReference,
            ConditionEndpoint = source.ConditionEndpoint
        };
    }
}
