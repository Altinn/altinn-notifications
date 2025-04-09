using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IEmailAndSmsProcessingService"/>
/// </summary>
public class EmailAndSmsProcessingService : IEmailAndSmsProcessingService
{
    private readonly IEmailOrderProcessingService _emailProcessingService;
    private readonly ISmsOrderProcessingService _smsProcessingService;
    private readonly IContactPointService _contactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAndSmsProcessingService"/> class.
    /// </summary>
    public EmailAndSmsProcessingService(
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

    /// <summary>
    /// Process notification orders, handling both initial processing and retry attempts.
    /// </summary>
    /// <param name="order">The notification order containing recipients and delivery preferences to process.</param>
    /// <param name="isRetry">Boolean flag indicating whether this is a retry attempt of a previously failed order.</param>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    private async Task ProcessOrderInternal(NotificationOrder order, bool isRetry)
    {
        List<Recipient> recipients = order.Recipients;
        List<Recipient> recipientsWithoutContactPoint = [.. recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))];

        await _contactPointService.AddEmailAndSmsContactPointsAsync(recipientsWithoutContactPoint, order.ResourceId);

        var (smsRecipients, emailRecipients) = OrganizeRecipientsByChannel(recipients);

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

    /// <summary>
    /// Organizes recipients into channel-specific collections by separating their contact points by type.
    /// </summary>
    /// <param name="recipients">An enumerable collection of recipients, each potentially containing both email and SMS address points.</param>
    /// <returns>A tuple containing two lists: 
    /// <para>SmsRecipients: Recipients with only SMS address points</para>
    /// <para>EmailRecipients: Recipients with only email address points</para>
    /// </returns>
    /// <remarks>
    /// For each recipient in the input collection:
    /// <list type="bullet">
    /// <item>Creates a new Recipient instance for SMS if SMS contact points exist</item>
    /// <item>Creates a new Recipient instance for email if email contact points exist</item>
    /// <item>Preserves recipient identity using NationalIdentityNumber or OrganizationNumber as dictionary keys</item>
    /// <item>Copies core properties (IsReserved, NationalIdentityNumber, OrganizationNumber) to each new instance</item>
    /// <item>Filters AddressInfo to include only the relevant address type in each new instance</item>
    /// </list>
    /// Recipients without either email or SMS contact points are effectively filtered out of the results.
    /// </remarks>
    private static (List<Recipient> SmsRecipients, List<Recipient> EmailRecipients) OrganizeRecipientsByChannel(IEnumerable<Recipient> recipients)
    {
        var smsRecipients = new Dictionary<string, Recipient>();
        var emailRecipients = new Dictionary<string, Recipient>();

        foreach (var recipient in recipients)
        {
            string recipientIdentifier = recipient.OrganizationNumber ?? recipient.NationalIdentityNumber ?? Guid.NewGuid().ToString();

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

        return ([.. smsRecipients.Values], [.. emailRecipients.Values]);
    }
}
