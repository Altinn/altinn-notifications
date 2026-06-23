using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
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
    public async Task<EmailOrderProcessingResult> ProcessOrder(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        return await ProcessOrderWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task<EmailOrderProcessingResult> ProcessOrderRetry(NotificationOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var recipients = await UpdateRecipientsWithContactPointsAsync(order);

        return await ProcessOrderRetryWithoutAddressLookup(order, recipients);
    }

    /// <inheritdoc/>
    public async Task<EmailOrderProcessingResult> ProcessOrderRetryWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var allEmailRecipients = await GetEmailRecipientsAsync(order, recipients);
        var registeredEmailRecipients = await _emailNotificationRepository.GetRecipients(order.Id);

        var notifications = new List<EmailNotification>();

        foreach (var recipient in recipients)
        {
            var addressPoint = recipient.AddressInfo.OfType<EmailAddressPoint>().FirstOrDefault();

            var isEmailRecipientRegistered =
                registeredEmailRecipients.Exists(er => er.ToAddress == addressPoint?.EmailAddress &&
                                                 er.OrganizationNumber == recipient.OrganizationNumber &&
                                                 er.NationalIdentityNumber == recipient.NationalIdentityNumber);
            if (isEmailRecipientRegistered)
            {
                continue;
            }

            var matchedEmailRecipient = FindEmailRecipient(allEmailRecipients, recipient);
            var emailRecipient = matchedEmailRecipient ?? new EmailRecipient { IsReserved = recipient.IsReserved };
            List<EmailAddressPoint> emailAddresses = addressPoint != null ? [addressPoint] : [];

            var created = await _emailService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                emailAddresses,
                emailRecipient,
                order.IgnoreReservation ?? false);

            notifications.AddRange(created);
        }

        return new EmailOrderProcessingResult(notifications);
    }

    /// <inheritdoc/>
    public async Task<EmailOrderProcessingResult> ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var allEmailRecipients = await GetEmailRecipientsAsync(order, recipients);

        var notifications = new List<EmailNotification>();

        foreach (var recipient in recipients)
        {
            var emailAddresses = recipient.AddressInfo
                .OfType<EmailAddressPoint>()
                .Where(a => !string.IsNullOrWhiteSpace(a.EmailAddress))
                .ToList();

            var matchedEmailRecipient = FindEmailRecipient(allEmailRecipients, recipient);
            var emailRecipient = matchedEmailRecipient ?? new EmailRecipient { IsReserved = recipient.IsReserved };

            var created = await _emailService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                emailAddresses,
                emailRecipient,
                order.IgnoreReservation ?? false);

            notifications.AddRange(created);
        }

        return new EmailOrderProcessingResult(notifications);
    }

    private bool RequiresCustomization(string? templatePart)
    {
        return _keywordsService.ContainsRecipientNumberPlaceholder(templatePart) || _keywordsService.ContainsRecipientNamePlaceholder(templatePart);
    }

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
            CustomizedBody = RequiresCustomization(emailTemplate?.Body) ? emailTemplate!.Body : null,
            CustomizedSubject = RequiresCustomization(emailTemplate?.Subject) ? emailTemplate!.Subject : null,
        }).ToList();

        return await _keywordsService.ReplaceKeywordsAsync(emailRecipients);
    }

    private static EmailRecipient? FindEmailRecipient(IEnumerable<EmailRecipient> emailRecipients, Recipient recipient)
    {
        return emailRecipients.FirstOrDefault(er =>
        (!string.IsNullOrWhiteSpace(recipient.OrganizationNumber) && er.OrganizationNumber == recipient.OrganizationNumber) ||
        (!string.IsNullOrWhiteSpace(recipient.NationalIdentityNumber) && er.NationalIdentityNumber == recipient.NationalIdentityNumber));
    }

    private async Task<List<Recipient>> UpdateRecipientsWithContactPointsAsync(NotificationOrder order)
    {
        var recipientsWithoutEmail = order.Recipients
            .Where(r => !r.AddressInfo.Exists(a => a.AddressType == AddressType.Email))
            .ToList();

        if (recipientsWithoutEmail.Count > 0)
        {
            await _contactPointService.AddEmailContactPoints(recipientsWithoutEmail, order.ResourceId, OrderLifecycleStage.Processing, order.UseStaleContactInformation, order.ResourceAction);
        }

        return order.Recipients;
    }
}
