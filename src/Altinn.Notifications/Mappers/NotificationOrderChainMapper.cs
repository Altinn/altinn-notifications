using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between external API models and internal domain models.
/// </summary>
public static partial class NotificationOrderChainMapper
{
    private const string SingleWhiteSpace = " ";

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
            RecipientOrganization = notificationOrderChainRequestExt.Recipient.RecipientOrganization?.MapToRecipientOrganization(),
            RecipientEmailAndSms = notificationOrderChainRequestExt.Recipient.RecipientEmailAndSms?.MapToRecipientEmailAndSms()
        };

        // Map the reminders and set their RequestedSendTime based on the main notification's requested time plus the delay.
        var reminders = notificationOrderChainRequestExt.Reminders?
            .Select(reminder =>
            {
                var reminderDelayDays = 0;
                DateTime requestedSendTime = notificationOrderChainRequestExt.RequestedSendTime;

                if (reminder.DelayDays.HasValue)
                {
                    requestedSendTime = notificationOrderChainRequestExt.RequestedSendTime.AddDays(reminder.DelayDays.Value).ToUniversalTime();
                }
                else if (reminder.RequestedSendTime.HasValue)
                {
                    requestedSendTime = reminder.RequestedSendTime.Value.ToUniversalTime();
                    reminderDelayDays = reminder.RequestedSendTime.Value.Subtract(notificationOrderChainRequestExt.RequestedSendTime).Days;
                }

                return reminder.MapToNotificationReminder(requestedSendTime, reminderDelayDays);
            })
            .ToList();

        DialogportenIdentifiers? dialogportenAssociation = notificationOrderChainRequestExt.DialogportenAssociation?.MapToDialogportenReference();

        return new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetRecipient(recipient)
            .SetReminders(reminders)
            .SetOrderId(Guid.NewGuid())
            .SetOrderChainId(Guid.NewGuid())
            .SetType(OrderType.Notification)
            .SetCreator(new Creator(creatorName))
            .SetDialogportenAssociation(dialogportenAssociation)
            .SetIdempotencyId(notificationOrderChainRequestExt.IdempotencyId)
            .SetSendersReference(notificationOrderChainRequestExt.SendersReference)
            .SetConditionEndpoint(notificationOrderChainRequestExt.ConditionEndpoint)
            .SetRequestedSendTime(notificationOrderChainRequestExt.RequestedSendTime.ToUniversalTime())
            .Build();
    }

    /// <summary>
    /// Maps a <see cref="DialogportenIdentifiersExt"/> to a <see cref="DialogportenIdentifiers"/>.
    /// </summary>
    private static DialogportenIdentifiers MapToDialogportenReference(this DialogportenIdentifiersExt dialogportenReferenceExt)
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
            ContentType = (EmailContentType)emailSendingOptionsExt.ContentType,
            SenderEmailAddress = emailSendingOptionsExt.SenderEmailAddress?.Trim(),
            SendingTimePolicy = (SendingTimePolicy)emailSendingOptionsExt.SendingTimePolicy,
            Subject = NormalizeLineEndingsRegex().Replace(emailSendingOptionsExt.Subject, SingleWhiteSpace)
        };
    }

    /// <summary>
    /// Maps a <see cref="NotificationReminderExt"/> to a <see cref="NotificationReminder"/>.
    /// </summary>
    /// <param name="notificationReminderExt">The external notification reminder object to map from.</param>
    /// <param name="requestedSendTime">The requested send time for the reminder.</param>
    /// <param name="reminderDelayDays">The number of days to delay the reminder relative to the main notification.</param>
    /// <returns>A <see cref="NotificationReminder"/> object mapped from the provided notification reminder.</returns>
    private static NotificationReminder MapToNotificationReminder(this NotificationReminderExt notificationReminderExt, DateTime requestedSendTime, int reminderDelayDays)
    {
        return new()
        {
            Recipient = new NotificationRecipient
            {
                RecipientSms = notificationReminderExt.Recipient.RecipientSms?.MapToRecipientSms(),
                RecipientEmail = notificationReminderExt.Recipient.RecipientEmail?.MapToRecipientEmail(),
                RecipientPerson = notificationReminderExt.Recipient.RecipientPerson?.MapToRecipientPerson(),
                RecipientOrganization = notificationReminderExt.Recipient.RecipientOrganization?.MapToRecipientOrganization(),
                RecipientEmailAndSms = notificationReminderExt.Recipient.RecipientEmailAndSms?.MapToRecipientEmailAndSms()
            },

            OrderId = Guid.NewGuid(),
            Type = OrderType.Reminder,
            RequestedSendTime = requestedSendTime,
            SendersReference = notificationReminderExt.SendersReference,
            ConditionEndpoint = notificationReminderExt.ConditionEndpoint,
            DelayDays = notificationReminderExt.DelayDays ?? reminderDelayDays
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientEmailExt"/> to a <see cref="RecipientEmail"/>.
    /// </summary>
    private static RecipientEmail MapToRecipientEmail(this RecipientEmailExt recipientEmailExt)
    {
        return new RecipientEmail
        {
            EmailAddress = recipientEmailExt.EmailAddress,
            Settings = recipientEmailExt.Settings.MapToEmailSendingOptions()
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientOrganizationExt"/> to a <see cref="RecipientOrganization"/>.
    /// </summary>
    private static RecipientOrganization MapToRecipientOrganization(this RecipientOrganizationExt recipientOrganizationExt)
    {
        return new RecipientOrganization
        {
            OrgNumber = recipientOrganizationExt.OrgNumber,
            ResourceId = recipientOrganizationExt.ResourceId,
            ChannelSchema = (NotificationChannel)recipientOrganizationExt.ChannelSchema,
            SmsSettings = recipientOrganizationExt.SmsSettings?.MapToSmsSendingOptions(),
            EmailSettings = recipientOrganizationExt.EmailSettings?.MapToEmailSendingOptions()
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientPersonExt"/> to a <see cref="RecipientPerson"/>.
    /// </summary>
    private static RecipientPerson MapToRecipientPerson(this RecipientPersonExt recipientPersonExt)
    {
        var smsSettings = recipientPersonExt.SmsSettings?.MapToSmsSendingOptions();
        var emailSettings = recipientPersonExt.EmailSettings?.MapToEmailSendingOptions();

        return new RecipientPerson
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
    /// Maps a <see cref="RecipientSmsExt"/> to a <see cref="RecipientSms"/>.
    /// </summary>
    private static RecipientSms MapToRecipientSms(this RecipientSmsExt recipientSmsExt)
    {
        return new RecipientSms
        {
            PhoneNumber = recipientSmsExt.PhoneNumber,
            Settings = recipientSmsExt.Settings.MapToSmsSendingOptions()
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
            Sender = smsSendingOptionsExt.Sender?.Trim(),
            SendingTimePolicy = (SendingTimePolicy)smsSendingOptionsExt.SendingTimePolicy
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientEmailAndSmsExt"/> to a <see cref="RecipientEmailAndSms"/>.
    /// </summary>
    private static RecipientEmailAndSms MapToRecipientEmailAndSms(this RecipientEmailAndSmsExt recipientEmailAndSmsExt)
    {
        return new RecipientEmailAndSms
        {
            EmailAddress = recipientEmailAndSmsExt.EmailAddress,
            PhoneNumber = recipientEmailAndSmsExt.PhoneNumber,
            EmailSettings = recipientEmailAndSmsExt.EmailSettings.MapToEmailSendingOptions(),
            SmsSettings = recipientEmailAndSmsExt.SmsSettings.MapToSmsSendingOptions()
        };
    }

    /// <summary>
    /// Regular expression to detect newline characters.
    /// </summary>
    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex NormalizeLineEndingsRegex();
}
