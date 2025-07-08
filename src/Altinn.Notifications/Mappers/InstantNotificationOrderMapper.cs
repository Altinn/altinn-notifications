using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Models.Orders;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Provides mapping functionality between external API models and internal domain models for instant notifications.
/// </summary>
public static class InstantNotificationOrderMapper
{
    /// <summary>
    /// Maps a <see cref="InstantNotificationOrderRequestExt"/> to a <see cref="InstantNotificationOrderRequest"/>.
    /// </summary>
    /// <param name="instantNotificationOrderRequest">The request that contains an instant notification order.</param>
    /// <param name="creatorName">The name of the person or entity who created the notification request.</param>
    /// <returns>A <see cref="InstantNotificationOrderRequest"/> object mapped from the provided notification order chain request.</returns>
    public static InstantNotificationOrderRequest MapToNotificationOrderChainRequest(this InstantNotificationOrderRequestExt instantNotificationOrderRequest, string creatorName)
    {
        return new InstantNotificationOrderRequest
        {
            OrderId = Guid.NewGuid(),
            OrderChainId = Guid.NewGuid(),
            Creator = new Creator(creatorName),
            IdempotencyId = instantNotificationOrderRequest.IdempotencyId,
            SendersReference = instantNotificationOrderRequest.SendersReference,
            Recipient = new InstantNotificationRecipient
            {
                RecipientTimedSms = instantNotificationOrderRequest.Recipient.RecipientTimedSms.MapToRecipientTimedSms()
            }
        };
    }

    /// <summary>
    /// Maps a <see cref="RecipientTimedSmsExt"/> to a <see cref="RecipientTimedSms"/>.
    /// </summary>
    private static RecipientTimedSms MapToRecipientTimedSms(this RecipientTimedSmsExt recipientSmsExt)
    {
        return new RecipientTimedSms
        {
            PhoneNumber = recipientSmsExt.PhoneNumber,
            Details = recipientSmsExt.Details.MapToSmsDetails(),
            TimeToLiveInSeconds = recipientSmsExt.TimeToLiveInSeconds
        };
    }

    /// <summary>
    /// Maps a <see cref="SmsDetailsExt"/> to a <see cref="SmsDetails"/>.
    /// </summary>
    private static SmsDetails MapToSmsDetails(this SmsDetailsExt smsSendingOptionsExt)
    {
        return new SmsDetails
        {
            Body = smsSendingOptionsExt.Body,
            Sender = smsSendingOptionsExt.Sender?.Trim()
        };
    }
}
