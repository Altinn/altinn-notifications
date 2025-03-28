using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between external API models and internal domain models.
/// </summary>
public static class NotificationOrderChainMapper
{
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
                var requestedSendTime = notificationOrderChainRequestExt.RequestedSendTime.AddDays(reminder.DelayDays).ToUniversalTime();

                return reminder.MapToNotificationReminder(requestedSendTime);
            })
            .ToList();

        DialogportenIdentifiers? dialogportenAssociation = notificationOrderChainRequestExt.DialogportenAssociation?.MapToDialogportenReference();

        return new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
            .SetOrderChainId(Guid.NewGuid())
            .SetCreator(new Creator(creatorName))
            .SetIdempotencyId(notificationOrderChainRequestExt.IdempotencyId)
            .SetRecipient(recipient)
            .SetConditionEndpoint(notificationOrderChainRequestExt.ConditionEndpoint)
            .SetDialogportenAssociation(dialogportenAssociation)
            .SetReminders(reminders)
            .SetRequestedSendTime(notificationOrderChainRequestExt.RequestedSendTime.ToUniversalTime())
            .SetSendersReference(notificationOrderChainRequestExt.SendersReference)
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
            Subject = emailSendingOptionsExt.Subject,
            SenderName = emailSendingOptionsExt.SenderName,
            ContentType = (EmailContentType)emailSendingOptionsExt.ContentType,
            SenderEmailAddress = emailSendingOptionsExt.SenderEmailAddress?.Trim(),
            SendingTimePolicy = (SendingTimePolicy)emailSendingOptionsExt.SendingTimePolicy
        };
    }

    /// <summary>
    /// Maps a <see cref="NotificationReminderExt"/> to a <see cref="NotificationReminder"/>.
    /// </summary>
    /// <param name="notificationReminderExt">The external notification reminder object to map from.</param>
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
            Sender = smsSendingOptionsExt.Sender,
            SendingTimePolicy = (SendingTimePolicy)smsSendingOptionsExt.SendingTimePolicy
        };
    }
}
