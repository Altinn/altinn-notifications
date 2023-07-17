using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Models;

namespace Altinn.Notifications.Mappers;

/// <summary>
/// Mapper for <see cref="EmailNotificationOrderRequestExt"/>
/// </summary>
public static class OrderMapper
{
    /// <summary>
    /// Maps a <see cref="EmailNotificationOrderRequestExt"/> to a <see cref="NotificationOrderRequest"/>
    /// </summary>
    public static NotificationOrderRequest MapToOrderRequest(this EmailNotificationOrderRequestExt extRequest, string creator)
    {
        var emailTemplate = new EmailTemplate(extRequest.FromAddress, extRequest.Subject, extRequest.Body, extRequest.ContentType);

        var recipients = new List<Recipient>();

        recipients.AddRange(
            extRequest.Recipients.Select(r => new Recipient(r.Id ?? string.Empty, new List<IAddressPoint>() { new EmailAddressPoint(r.EmailAddress!) })));

        return new NotificationOrderRequest(
            extRequest.SendersReference,
            creator,
            new List<INotificationTemplate>() { emailTemplate },
            extRequest.SendTime,
            NotificationChannel.Email,
            recipients);
    }

    /// <summary>
    /// Maps a List of <see cref="Recipient"/> to a List of <see cref="RecipientExt"/>
    /// </summary>
    public static List<RecipientExt> MapToRecipientExt(this List<Recipient> recipients)
    {
        var recipientExt = new List<RecipientExt>();

        recipientExt.AddRange(
            recipients.Select(r => new RecipientExt
            {
                Id = r.RecipientId,
                EmailAddress = GetEmailFromAddressList(r.AddressInfo)
            }));

        return recipientExt;
    }

    private static string? GetEmailFromAddressList(List<IAddressPoint> addressPoints)
    {
        var emailAddressPoint = addressPoints
            .Find(a => a.AddressType.Equals(AddressType.Email))
            as EmailAddressPoint;

        return emailAddressPoint?.EmailAddress;
    }

    /// <summary>
    /// Maps a <see cref="NotificationOrder"/> to a <see cref="NotificationOrderExt"/>
    /// </summary>
    public static NotificationOrderExt MapToNotificationOrderExt(this NotificationOrder order)
    {
        var orderExt = new NotificationOrderExt
        {
            Id = order.Id,
            SendersReference = order.SendersReference,
            Created = order.Created,
            Creator = order.Creator.ShortName,
            NotificationChannel = order.NotificationChannel,
            Recipients = order.Recipients.MapToRecipientExt(),
            SendTime = order.SendTime
        };

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
                        ContentType = emailTemplate.ContentType,
                        Subject = emailTemplate.Subject
                    };

                    break;
                default:
                    break;
            }
        }

        return orderExt;
    }
}