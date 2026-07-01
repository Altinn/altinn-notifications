using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IEmailAndSmsOrderProcessingService"/>
/// </summary>
public class EmailAndSmsOrderProcessingService : IEmailAndSmsOrderProcessingService
{
    private readonly IEmailOrderProcessingService _emailProcessingService;
    private readonly ISmsOrderProcessingService _smsProcessingService;
    private readonly IContactPointService _contactPointService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAndSmsOrderProcessingService"/> class.
    /// </summary>
    public EmailAndSmsOrderProcessingService(
        IEmailOrderProcessingService emailProcessingService,
        ISmsOrderProcessingService smsProcessingService,
        IContactPointService contactPointService)
    {
        _emailProcessingService = emailProcessingService;
        _smsProcessingService = smsProcessingService;
        _contactPointService = contactPointService;
    }

    /// <inheritdoc/>
    public async Task<OrderProcessingResult> ProcessOrderAsync(NotificationOrder order)
    {
        return await ProcessOrderInternal(order, false);
    }

    /// <inheritdoc/>
    public async Task<OrderProcessingResult> ProcessOrderRetryAsync(NotificationOrder order)
    {
        return await ProcessOrderInternal(order, true);
    }

    /// <summary>
    /// Processes notification orders, handling both initial processing and retry attempts.
    /// Performs the contact point lookup before building in-memory notifications for both channels.
    /// </summary>
    private async Task<OrderProcessingResult> ProcessOrderInternal(NotificationOrder order, bool isRetry)
    {
        List<Recipient> recipients = order.Recipients;
        List<Recipient> recipientsWithoutContactPoint = [.. recipients.Where(r => !r.AddressInfo.Exists(ap => ap.AddressType == AddressType.Email || ap.AddressType == AddressType.Sms))];

        if (recipientsWithoutContactPoint.Count > 0)
        {
            await _contactPointService.AddEmailAndSmsContactPointsAsync(recipientsWithoutContactPoint, order.ResourceId, OrderLifecycleStage.Processing, order.UseStaleContactInformation, order.ResourceAction);
        }

        var (smsRecipients, emailRecipients) = OrganizeRecipientsByChannel(recipients);

        var smsResultTask = _smsProcessingService.ProcessOrderWithoutAddressLookup(order, smsRecipients);
        var emailResultTask = _emailProcessingService.ProcessOrderWithoutAddressLookup(order, emailRecipients);

        await Task.WhenAll(smsResultTask, emailResultTask);

        return new OrderProcessingResult(
            EmailOrderProcessingResult: await emailResultTask,
            SmsOrderProcessingResult: await smsResultTask);
    }

    /// <summary>
    /// De-duplicates and organizes recipients into SMS and email lists, filtering contact points for each channel.
    /// </summary>
    /// <remarks>
    /// All unique recipients appear in both lists. A recipient's <see cref="Recipient.AddressInfo"/> will be
    /// empty if they have no contact points for that channel. The last entry wins in case of duplicates.
    /// </remarks>
    private static (List<Recipient> SmsRecipients, List<Recipient> EmailRecipients) OrganizeRecipientsByChannel(IEnumerable<Recipient> recipients)
    {
        var smsRecipients = new Dictionary<string, Recipient>();
        var emailRecipients = new Dictionary<string, Recipient>();

        foreach (var recipient in recipients)
        {
            var recipientIdentifier = string.Empty;

            if (!string.IsNullOrWhiteSpace(recipient.ExternalIdentity))
            {
                recipientIdentifier = recipient.ExternalIdentity;
            }
            else if (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber))
            {
                recipientIdentifier = recipient.OrganizationNumber;
            }
            else if (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber))
            {
                recipientIdentifier = recipient.NationalIdentityNumber;
            }

            if (string.IsNullOrEmpty(recipientIdentifier))
            {
                throw new ArgumentException("Recipient must have either OrganizationNumber, NationalIdentityNumber, or ExternalIdentity.");
            }

            smsRecipients[recipientIdentifier] = new Recipient
            {
                IsReserved = recipient.IsReserved,
                ExternalIdentity = recipient.ExternalIdentity,
                OrganizationNumber = recipient.OrganizationNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == AddressType.Sms)]
            };

            emailRecipients[recipientIdentifier] = new Recipient
            {
                IsReserved = recipient.IsReserved,
                ExternalIdentity = recipient.ExternalIdentity,
                OrganizationNumber = recipient.OrganizationNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == AddressType.Email)]
            };
        }

        return ([.. smsRecipients.Values], [.. emailRecipients.Values]);
    }
}
