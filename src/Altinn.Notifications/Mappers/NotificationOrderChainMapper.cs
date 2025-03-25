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
                // Calculate the send time for each reminder.
                var requestedSendTime = notificationOrderChainRequestExt.RequestedSendTime.AddDays(reminder.DelayDays).ToUniversalTime();

                // Map the reminder with the calculated send time.
                return reminder.MapToNotificationReminder(requestedSendTime);
            })
            .ToList();

        DialogportenIdentifiers? dialogportenAssociation = notificationOrderChainRequestExt.DialogportenAssociation?.MapToDialogportenReference();

        return new NotificationOrderChainRequest.NotificationOrderChainRequestBuilder()
            .SetOrderId(Guid.NewGuid())
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
            ContentType = (EmailContentType)emailSendingOptionsExt.ContentType,
            SendingTimePolicy = (SendingTimePolicy)emailSendingOptionsExt.SendingTimePolicy,
            SenderEmailAddress = string.IsNullOrWhiteSpace(emailSendingOptionsExt.SenderName) ? string.Empty : emailSendingOptionsExt.SenderEmailAddress?.Trim() ?? string.Empty,
        };
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
    /// Maps a <see cref="RecipientEmailExt"/> to a <see cref="RecipientEmail"/>.
    /// </summary>
    private static RecipientEmail? MapToRecipientEmail(this RecipientEmailExt recipientEmailExt)
    {
        if (string.IsNullOrWhiteSpace(recipientEmailExt.EmailAddress) || recipientEmailExt.Settings == null)
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
    /// Maps a <see cref="RecipientSmsExt"/> to a <see cref="RecipientSms"/>.
    /// </summary>
    private static RecipientSms? MapToRecipientSms(this RecipientSmsExt recipientSmsExt)
    {
        if (string.IsNullOrWhiteSpace(recipientSmsExt.PhoneNumber) || recipientSmsExt.Settings == null)
        {
            return null;
        }

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
            SendingTimePolicy = (SendingTimePolicy)smsSendingOptionsExt.SendingTimePolicy,
            Sender = string.IsNullOrWhiteSpace(smsSendingOptionsExt.Sender) ? string.Empty : smsSendingOptionsExt.Sender
        };
    }
}
