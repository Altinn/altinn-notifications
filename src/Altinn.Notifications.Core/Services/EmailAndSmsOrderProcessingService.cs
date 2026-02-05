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
    public async Task ProcessOrderAsync(NotificationOrder order)
    {
        await ProcessOrderInternal(order, false);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetryAsync(NotificationOrder order)
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

        Task smsOrdersHandlingTask;
        Task emailOrdersHandlingTask;

        if (isRetry)
        {
            smsOrdersHandlingTask = _smsProcessingService.ProcessOrderRetryWithoutAddressLookup(order, smsRecipients);
            emailOrdersHandlingTask = _emailProcessingService.ProcessOrderRetryWithoutAddressLookup(order, emailRecipients);
        }
        else
        {
            smsOrdersHandlingTask = _smsProcessingService.ProcessOrderWithoutAddressLookup(order, smsRecipients);
            emailOrdersHandlingTask = _emailProcessingService.ProcessOrderWithoutAddressLookup(order, emailRecipients);
        }

        await Task.WhenAll(smsOrdersHandlingTask, emailOrdersHandlingTask);
    }

    /// <summary>
    /// De-duplicates and organizes recipients into SMS and email lists, filtering contact points for each channel.
    /// </summary>
    /// <param name="recipients">The recipients to process.</param>
    /// <returns>A tuple with (<c>SmsRecipients</c>, <c>EmailRecipients</c>) lists. Each list contains all unique recipients with contact points filtered for the channel.</returns>
    /// <remarks>
    /// All unique recipients appear in both lists. A recipient's <see cref="Recipient.AddressInfo"/> will be empty if they have no contact points for that channel. The last entry wins in case of duplicates.
    /// </remarks>
    private static (List<Recipient> SmsRecipients, List<Recipient> EmailRecipients) OrganizeRecipientsByChannel(IEnumerable<Recipient> recipients)
    {
        var smsRecipients = new Dictionary<string, Recipient>();
        var emailRecipients = new Dictionary<string, Recipient>();

        foreach (var recipient in recipients)
        {
            var recipientIdentifier = string.Empty;

            if (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber))
            {
                recipientIdentifier = recipient.OrganizationNumber;
            }
            else if (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber))
            {
                recipientIdentifier = recipient.NationalIdentityNumber;
            }

            if (string.IsNullOrEmpty(recipientIdentifier))
            {
                throw new ArgumentException("Recipient must have either OrganizationNumber or NationalIdentityNumber.");
            }

            smsRecipients[recipientIdentifier] = new Recipient
            {
                IsReserved = recipient.IsReserved,
                OrganizationNumber = recipient.OrganizationNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == AddressType.Sms)]
            };

            emailRecipients[recipientIdentifier] = new Recipient
            {
                IsReserved = recipient.IsReserved,
                OrganizationNumber = recipient.OrganizationNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                AddressInfo = [.. recipient.AddressInfo.Where(a => a.AddressType == AddressType.Email)]
            };
        }

        return ([.. smsRecipients.Values], [.. emailRecipients.Values]);
    }
}
