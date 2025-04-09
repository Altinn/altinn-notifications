using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IBothChannelsProcessingService"/>
/// </summary>
public class BothChannelsProcessingService : IBothChannelsProcessingService
{
    private readonly IEmailOrderProcessingService _emailProcessingService;
    private readonly ISmsOrderProcessingService _smsProcessingService;
    private readonly IContactPointService _contactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferredChannelProcessingService"/> class.
    /// </summary>
    public BothChannelsProcessingService(
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
        List<Recipient> recipientsWithoutContactPoint = [.. recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))];

        await _contactPointService.AddEmailAndSmsContactPointsAsync(recipientsWithoutContactPoint, order.ResourceId);

        List<Recipient> smsRecipients;
        List<Recipient> emailRecipients;
        (emailRecipients, smsRecipients) = GenerateRecipientLists(recipients);

        if (isRetry)
        {
            await _smsProcessingService.ProcessOrderRetryWithoutAddressLookup(order, smsRecipients);
            await _emailProcessingService.ProcessOrderRetryWithoutAddressLookup(order, emailRecipients);
        }
        else
        {
            await _smsProcessingService.ProcessOrderWithoutAddressLookup(order, smsRecipients);
            await _emailProcessingService.ProcessOrderWithoutAddressLookup(order, emailRecipients);
        }
    }

    private static (List<Recipient> EmailRecipients, List<Recipient> SmsRecipients) GenerateRecipientLists(List<Recipient> recipients)
    {
        var smsRecipients = new Dictionary<string, Recipient>();
        var emailRecipients = new Dictionary<string, Recipient>();

        foreach (var recipient in recipients)
        {
            string recipientIdentifier = recipient.NationalIdentityNumber ?? recipient.OrganizationNumber ?? Guid.NewGuid().ToString();

            int smsContactPointCount = recipient.AddressInfo.Count(a => a.AddressType == AddressType.Sms);
            if (smsContactPointCount > 0)
            {
                smsRecipients[recipientIdentifier] = new Recipient
                {
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == AddressType.Sms)],
                };
            }

            int emailContactPointCount = recipient.AddressInfo.Count(a => a.AddressType == AddressType.Email);
            if (emailContactPointCount > 0)
            {
                emailRecipients[recipientIdentifier] = new Recipient
                {
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == AddressType.Email)],
                };
            }
        }

        return ([.. emailRecipients.Values], [.. smsRecipients.Values]);
    }
}
