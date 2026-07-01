using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IPreferredChannelProcessingService"/>
/// </summary>
public class PreferredChannelProcessingService : IPreferredChannelProcessingService
{
    private readonly IEmailOrderProcessingService _emailProcessingService;
    private readonly ISmsOrderProcessingService _smsProcessingService;
    private readonly IContactPointService _contactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferredChannelProcessingService"/> class.
    /// </summary>
    public PreferredChannelProcessingService(
        IEmailOrderProcessingService emailProcessingService,
        ISmsOrderProcessingService smsProcessingService,
        IContactPointService contactPointService)
    {
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
        _contactPointService = contactPointService;
    }

    /// <inheritdoc/>
    public async Task<OrderProcessingResult> ProcessOrder(NotificationOrder order)
    {
        if (order.NotificationChannel is not (NotificationChannel.EmailPreferred or NotificationChannel.SmsPreferred))
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order.NotificationChannel,
                $"Preferred channel processing only supports {NotificationChannel.EmailPreferred} and {NotificationChannel.SmsPreferred}.");
        }

        List<Recipient> recipients = order.Recipients;
        List<Recipient> recipientsWithoutContactPoint =
            [.. recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))];

        if (recipientsWithoutContactPoint.Count > 0)
        {
            await _contactPointService.AddPreferredContactPoints(order.NotificationChannel, recipientsWithoutContactPoint, order.ResourceId, OrderLifecycleStage.Processing, order.UseStaleContactInformation, order.ResourceAction);
        }

        AddressType preferredAddressType = order.NotificationChannel == NotificationChannel.EmailPreferred
            ? AddressType.Email
            : AddressType.Sms;

        AddressType fallbackAddressType = order.NotificationChannel == NotificationChannel.EmailPreferred
            ? AddressType.Sms
            : AddressType.Email;

        var (preferredRecipients, fallbackRecipients) = GenerateRecipientLists(recipients, preferredAddressType, fallbackAddressType);

        Task<EmailOrderProcessingResult> emailResultTask = order.NotificationChannel == NotificationChannel.EmailPreferred
            ? _emailProcessingService.ProcessOrderWithoutAddressLookup(order, preferredRecipients)
            : _emailProcessingService.ProcessOrderWithoutAddressLookup(order, fallbackRecipients);

        Task<SmsOrderProcessingResult> smsResultTask = order.NotificationChannel == NotificationChannel.SmsPreferred
            ? _smsProcessingService.ProcessOrderWithoutAddressLookup(order, preferredRecipients)
            : _smsProcessingService.ProcessOrderWithoutAddressLookup(order, fallbackRecipients);

        await Task.WhenAll(emailResultTask, smsResultTask);

        return new OrderProcessingResult(
            EmailOrderProcessingResult: await emailResultTask,
            SmsOrderProcessingResult: await smsResultTask);
    }

    /// <inheritdoc/>
    public Task<OrderProcessingResult> ProcessOrderRetry(NotificationOrder order)
    {
        return ProcessOrder(order);
    }

    private static (List<Recipient> PreferredChannelRecipients, List<Recipient> FallbackChannelRecipients) GenerateRecipientLists(List<Recipient> recipients, AddressType preferredAddressType, AddressType fallbackAddressType)
    {
        var fallbackChannelRecipients = new Dictionary<string, Recipient>();
        var preferredChannelRecipients = new Dictionary<string, Recipient>();

        foreach (var recipient in recipients)
        {
            string recipientIdentifier = recipient.ExternalIdentity ?? recipient.NationalIdentityNumber ?? recipient.OrganizationNumber ?? Guid.NewGuid().ToString();

            int fallbackAddressCount = recipient.AddressInfo.Count(a => a.AddressType == fallbackAddressType);
            if (fallbackAddressCount > 0)
            {
                fallbackChannelRecipients[recipientIdentifier] = new Recipient
                {
                    IsReserved = recipient.IsReserved,
                    ExternalIdentity = recipient.ExternalIdentity,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == fallbackAddressType)],
                };
            }

            int preferredAddressCount = recipient.AddressInfo.Count(a => a.AddressType == preferredAddressType);
            if (preferredAddressCount > 0)
            {
                preferredChannelRecipients[recipientIdentifier] = new Recipient
                {
                    IsReserved = recipient.IsReserved,
                    ExternalIdentity = recipient.ExternalIdentity,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == preferredAddressType)],
                };
            }

            if (fallbackAddressCount == 0 && preferredAddressCount == 0)
            {
                preferredChannelRecipients[recipientIdentifier] = recipient;
            }
        }

        return ([.. preferredChannelRecipients.Values], [.. fallbackChannelRecipients.Values]);
    }
}
