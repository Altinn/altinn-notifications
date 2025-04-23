using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
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
        List<Recipient> recipientsWithoutContactPoint =
            recipients
                .Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))
                .ToList();

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

    private static (List<Recipient> PreferredChannelRecipients, List<Recipient> FallbackChannelRecipients) GenerateRecipientLists(List<Recipient> recipients, AddressType preferredAddressType, AddressType fallbackAddressType)
    {
        // Initialize dictionaries to hold recipients for preferred and fallback channels
        var fallbackChannelRecipients = new Dictionary<string, Recipient>();
        var preferredChannelRecipients = new Dictionary<string, Recipient>();

        foreach (var recipient in recipients)
        {
            // Generate a unique identifier for the recipient
            string recipientIdentifier = recipient.NationalIdentityNumber ?? recipient.OrganizationNumber ?? Guid.NewGuid().ToString();

            // Process recipients with fallback addresses.
            int fallbackAddressCount = recipient.AddressInfo.Count(a => a.AddressType == fallbackAddressType);
            if (fallbackAddressCount > 0)
            {
                fallbackChannelRecipients[recipientIdentifier] = new Recipient
                {
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == fallbackAddressType)],
                };
            }

            // Process recipients with preferred addresses
            int preferredAddressCount = recipient.AddressInfo.Count(a => a.AddressType == preferredAddressType);
            if (preferredAddressCount > 0)
            {
                preferredChannelRecipients[recipientIdentifier] = new Recipient
                {
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == preferredAddressType)],
                };
            }

            // Handle recipients with neither a preferred nor a fallback address.
            if (fallbackAddressCount == 0 && preferredAddressCount == 0)
            {
                preferredChannelRecipients[recipientIdentifier] = recipient;
            }
        }

        return ([.. preferredChannelRecipients.Values], [.. fallbackChannelRecipients.Values]);
    }
}
