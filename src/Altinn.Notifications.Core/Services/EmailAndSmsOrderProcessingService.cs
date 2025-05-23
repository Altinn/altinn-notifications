﻿using Altinn.Notifications.Core.Enums;
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
            string recipientIdentifier = recipient.OrganizationNumber ?? recipient.NationalIdentityNumber!;

            var smsAddressInfo = recipient.AddressInfo.Where(a => a.AddressType == AddressType.Sms).ToList();
            if (smsAddressInfo.Count > 0)
            {
                smsRecipients[recipientIdentifier] = new Recipient
                {
                    AddressInfo = smsAddressInfo,
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber
                };
            }

            var emailAddressInfo = recipient.AddressInfo.Where(a => a.AddressType == AddressType.Email).ToList();
            if (emailAddressInfo.Count > 0)
            {
                emailRecipients[recipientIdentifier] = new Recipient
                {
                    AddressInfo = emailAddressInfo,
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber
                };
            }
        }

        return ([.. smsRecipients.Values], [.. emailRecipients.Values]);
    }
}
