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
    /// Initializes a new instance of the <see cref="OrderProcessingService"/> class.
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
    public async Task ProcessOrder(NotificationOrder order)
    {
        await ProcessOrderInternal(order, false);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        await ProcessOrderInternal(order, true);
    }

    private async Task ProcessOrderInternal(NotificationOrder order, bool isRetry)
    {
        List<Recipient> recipients = order.Recipients;
        List<Recipient> recipientsWithoutContactPoint = recipients.Where(r => r.AddressInfo.Count == 0).ToList();

        await _contactPointService.AddPreferredContactPoints(order.NotificationChannel, recipientsWithoutContactPoint, order.ResourceId);

        List<Recipient> preferredChannelRecipients;
        List<Recipient> fallBackChannelRecipients;

        switch (order.NotificationChannel)
        {
            case NotificationChannel.EmailPreferred:
                (preferredChannelRecipients, fallBackChannelRecipients) =
                    GenerateRecipientLists(recipients, AddressType.Email, AddressType.Sms);

                if (isRetry)
                {
                    await _emailProcessingService.ProcessOrderRetryWithoutAddressLookup(order, preferredChannelRecipients);
                    await _smsProcessingService.ProcessOrderRetryWithoutAddressLookup(order, fallBackChannelRecipients);
                }
                else
                {
                    await _emailProcessingService.ProcessOrderWithoutAddressLookup(order, preferredChannelRecipients);
                    await _smsProcessingService.ProcessOrderWithoutAddressLookup(order, fallBackChannelRecipients);
                }

                break;

            case NotificationChannel.SmsPreferred:
                (preferredChannelRecipients, fallBackChannelRecipients) =
                     GenerateRecipientLists(recipients, AddressType.Sms, AddressType.Email);

                if (isRetry)
                {
                    await _smsProcessingService.ProcessOrderRetryWithoutAddressLookup(order, preferredChannelRecipients);
                    await _emailProcessingService.ProcessOrderRetryWithoutAddressLookup(order, fallBackChannelRecipients);
                }
                else
                {
                    await _smsProcessingService.ProcessOrderWithoutAddressLookup(order, preferredChannelRecipients);
                    await _emailProcessingService.ProcessOrderWithoutAddressLookup(order, fallBackChannelRecipients);
                }

                break;
        }
    }

    private (List<Recipient> PreferredChannelRecipients, List<Recipient> FallBackChannelRecipients) GenerateRecipientLists(
      List<Recipient> recipients,
      AddressType preferredAddressType,
      AddressType fallbackAddressType)
    {
        List<Recipient> preferredChannelRecipients = recipients
            .Where(r => r.AddressInfo.Exists(ap => ap.AddressType == preferredAddressType))
            .ToList();

        List<Recipient> fallBackChannelRecipients = recipients
            .Where(r => r.AddressInfo.Exists(ap => ap.AddressType == fallbackAddressType))
            .Except(preferredChannelRecipients)
            .ToList();

        List<Recipient> missingContactRecipients = recipients
            .Except(preferredChannelRecipients)
            .Except(fallBackChannelRecipients)
            .ToList();

        preferredChannelRecipients.AddRange(missingContactRecipients);

        return (preferredChannelRecipients, fallBackChannelRecipients);
    }
}
