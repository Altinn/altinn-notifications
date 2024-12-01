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
/// Implementation of the <see cref="IEmailOrderProcessingService"/>.
/// </summary>
public class EmailOrderProcessingService : IEmailOrderProcessingService
{
    private readonly IContactPointService _contactPointService;
    private readonly IEmailNotificationRepository _emailNotificationRepository;
    private readonly IEmailNotificationService _emailService;
    private readonly IKeywordsService _keywordsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOrderProcessingService"/> class.
    /// </summary>
    /// <param name="emailNotificationRepository">The email notification repository.</param>
    /// <param name="emailService">The email notification service.</param>
    /// <param name="contactPointService">The contact point service.</param>
    /// <param name="keywordsService">The keywords service.</param>
    public EmailOrderProcessingService(
        IEmailNotificationRepository emailNotificationRepository,
        IEmailNotificationService emailService,
        IContactPointService contactPointService,
        IKeywordsService keywordsService)
    {
        _emailNotificationRepository = emailNotificationRepository;
        _emailService = emailService;
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
        var emailRecipients = await _emailNotificationRepository.GetRecipients(order.Id);

        foreach (var recipient in recipients)
        {
            var addressPoint = recipient.AddressInfo.OfType<EmailAddressPoint>().FirstOrDefault();

            var emailRecipient = emailRecipients.Find(er =>
                er.ToAddress == addressPoint?.EmailAddress &&
                er.OrganizationNumber == recipient.OrganizationNumber &&
                er.NationalIdentityNumber == recipient.NationalIdentityNumber);

            if (emailRecipient == null)
            {
                continue;
            }

            await _emailService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                [addressPoint],
                emailRecipient!,
                order.IgnoreReservation ?? false);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var emailRecipients = await GetEmailRecipientsAsync(order, recipients);

        foreach (var recipient in recipients)
        {
            var emailAddresses = recipient.AddressInfo
                .OfType<EmailAddressPoint>()
                .Where(a => !string.IsNullOrWhiteSpace(a.EmailAddress))
                .ToList();

            var emailRecipient = FindEmailRecipient(emailRecipients, recipient);
            emailRecipient ??= new() { ToAddress = emailAddresses[0].EmailAddress };

            await _emailService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                emailAddresses,
                emailRecipient,
                order.IgnoreReservation ?? false);
        }
    }

    /// <summary>
    /// Retrieves email recipients with replaced keywords.
    /// </summary>
    /// <param name="order">The notification order.</param>
    /// <param name="recipients">The list of recipients.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of email recipients.</returns>
    private async Task<IEnumerable<EmailRecipient>> GetEmailRecipientsAsync(NotificationOrder order, IEnumerable<Recipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(order.Templates);

        var emailTemplate = order.Templates.OfType<EmailTemplate>().FirstOrDefault();
        var emailRecipients = recipients.Select(recipient => new EmailRecipient
        {
            IsReserved = recipient.IsReserved,
            OrganizationNumber = recipient.OrganizationNumber,
            NationalIdentityNumber = recipient.NationalIdentityNumber,
            CustomizedBody = (_keywordsService.ContainsRecipientNumberPlaceholder(emailTemplate?.Body) || _keywordsService.ContainsRecipientNamePlaceholder(emailTemplate?.Body)) ? emailTemplate?.Body : null,
            CustomizedSubject = (_keywordsService.ContainsRecipientNumberPlaceholder(emailTemplate?.Subject) || _keywordsService.ContainsRecipientNamePlaceholder(emailTemplate?.Subject)) ? emailTemplate?.Subject : null,
        }).ToList();

        return await _keywordsService.ReplaceKeywordsAsync(emailRecipients);
    }

    /// <summary>
    /// Finds the email recipient matching the given recipient.
    /// </summary>
    /// <param name="emailRecipients">The list of email recipients.</param>
    /// <param name="recipient">The recipient to match.</param>
    /// <returns>The matching email recipient, or null if no match is found.</returns>
    private static EmailRecipient? FindEmailRecipient(IEnumerable<EmailRecipient> emailRecipients, Recipient recipient)
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
        var recipientsWithoutEmail = order.Recipients
            .Where(r => !r.AddressInfo.Exists(a => a.AddressType == AddressType.Email))
            .ToList();

        if (recipientsWithoutEmail.Count != 0)
        {
            await _contactPointService.AddEmailContactPoints(recipientsWithoutEmail, order.ResourceId);
        }

        return order.Recipients;
    }
}
