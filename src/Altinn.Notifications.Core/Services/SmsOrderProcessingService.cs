﻿using System.Web;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="ISmsOrderProcessingService"/>
/// </summary>
public class SmsOrderProcessingService : ISmsOrderProcessingService
{
    private readonly ISmsNotificationRepository _smsNotificationRepository;
    private readonly ISmsNotificationService _smsService;
    private readonly IContactPointService _contactPointService;
    private readonly IKeywordsService _keywordsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOrderProcessingService"/> class.
    /// </summary>
    public SmsOrderProcessingService(
        ISmsNotificationRepository smsNotificationRepository,
        ISmsNotificationService smsService,
        IContactPointService contactPointService,
        IKeywordsService keywordsService)
    {
        _smsNotificationRepository = smsNotificationRepository;
        _smsService = smsService;
        _contactPointService = contactPointService;
        _keywordsService = keywordsService;
    }

    /// <inheritdoc/>
    public async Task ProcessOrder(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        await ProcessOrderWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetry(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        await ProcessOrderRetryWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        int smsCount = GetSmsCountForOrder(order);
        List<SmsRecipient> smsRecipients = await _smsNotificationRepository.GetRecipients(order.Id);

        foreach (var recipient in recipients)
        {
            var smsAddress = recipient.AddressInfo.OfType<SmsAddressPoint>().FirstOrDefault();

            var smsRecipient = smsRecipients.Find(er =>
                er.MobileNumber == smsAddress?.MobileNumber &&
                er.OrganizationNumber == recipient.OrganizationNumber &&
                er.NationalIdentityNumber == recipient.NationalIdentityNumber);

            if (smsRecipient == null || smsAddress == null)
            {
                continue;
            }

            await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                [smsAddress],
                smsRecipient,
                smsCount);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        int smsCount = GetSmsCountForOrder(order);

        var emailRecipients = await GetSmsRecipientsAsync(order, recipients);

        foreach (var recipient in recipients)
        {
            var emailRecipient = FindEmailRecipient(emailRecipients, recipient);

            var emailAddresses = recipient.AddressInfo
                .OfType<SmsAddressPoint>()
                .Where(a => !string.IsNullOrWhiteSpace(a.MobileNumber))
                .ToList();

            await _smsService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                emailAddresses,
                emailRecipient,
                smsCount, 
                order.IgnoreReservation ?? false);
        }
    }

    /// <summary>
    /// Retrieves email recipients with replaced keywords.
    /// </summary>
    /// <param name="order">The notification order.</param>
    /// <param name="recipients">The list of recipients.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of email recipients.</returns>
    private async Task<IEnumerable<SmsRecipient>> GetSmsRecipientsAsync(NotificationOrder order, IEnumerable<Recipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(order.Templates);

        var smsTemplate = order.Templates.OfType<SmsTemplate>().FirstOrDefault();
        var smsRecipients = recipients.Select(recipient => new SmsRecipient
        {
            IsReserved = recipient.IsReserved,
            OrganizationNumber = recipient.OrganizationNumber,
            NationalIdentityNumber = recipient.NationalIdentityNumber,
            CustomizedBody = (_keywordsService.ContainsRecipientNumberPlaceholder(smsTemplate?.Body) || _keywordsService.ContainsRecipientNamePlaceholder(smsTemplate?.Body)) ? smsTemplate?.Body : null,
        }).ToList();

        return await _keywordsService.ReplaceKeywordsAsync(smsRecipients);
    }

    /// <summary>
    /// Finds the email recipient matching the given recipient.
    /// </summary>
    /// <param name="emailRecipients">The list of email recipients.</param>
    /// <param name="recipient">The recipient to match.</param>
    /// <returns>The matching email recipient, or null if no match is found.</returns>
    private static SmsRecipient? FindEmailRecipient(IEnumerable<SmsRecipient> emailRecipients, Recipient recipient)
    {
        return emailRecipients.FirstOrDefault(er =>
            (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber) && er.OrganizationNumber == recipient.OrganizationNumber) ||
            (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber) && er.NationalIdentityNumber == recipient.NationalIdentityNumber));
    }

    /// <summary>
    /// Updates the recipients with contact points.
    /// </summary>
    /// <param name="order">The notification order.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the updated list of recipients.</returns>
    private async Task<List<Recipient>> UpdateRecipientsWithContactPointsAsync(NotificationOrder order)
    {
        var recipientsWithoutMobileNumber = order.Recipients
            .Where(r => !r.AddressInfo.Exists(a => a.AddressType == AddressType.Sms))
            .ToList();

        if (recipientsWithoutMobileNumber.Count != 0)
        {
            await _contactPointService.AddSmsContactPoints(recipientsWithoutMobileNumber, order.ResourceId);
        }

        return order.Recipients;
    }

    /// <summary>
    /// Calculates the number of messages based on the rules for concatenation of SMS messages in the SMS gateway.
    /// </summary>
    internal static int CalculateNumberOfMessages(string message)
    {
        const int maxCharactersPerMessage = 160;
        const int maxMessagesPerConcatenation = 16;
        const int charactersPerConcatenatedMessage = 134;

        string urlEncodedMessage = HttpUtility.UrlEncode(message);
        int messageLength = urlEncodedMessage.Length;

        if (messageLength <= maxCharactersPerMessage)
        {
            return 1;
        }

        // Calculate the number of messages for messages exceeding 160 characters
        int numberOfMessages = (int)Math.Ceiling((double)messageLength / charactersPerConcatenatedMessage);

        // Check if the total number of messages exceeds the limit
        if (numberOfMessages > maxMessagesPerConcatenation)
        {
            numberOfMessages = maxMessagesPerConcatenation;
        }

        return numberOfMessages;
    }

    private static int GetSmsCountForOrder(NotificationOrder order)
    {
        SmsTemplate? smsTemplate = order.Templates.Find(t => t.Type == NotificationTemplateType.Sms) as SmsTemplate;
        return CalculateNumberOfMessages(smsTemplate!.Body);
    }
}
