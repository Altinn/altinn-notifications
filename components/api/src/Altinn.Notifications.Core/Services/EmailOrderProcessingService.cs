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
    private readonly INotificationScheduleService _notificationScheduleService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailOrderProcessingService"/> class.
    /// </summary>
    /// <param name="emailNotificationRepository">The email notification repository.</param>
    /// <param name="emailService">The email notification service.</param>
    /// <param name="contactPointService">The contact point service.</param>
    /// <param name="keywordsService">The keywords service.</param>
    /// <param name="notificationScheduleService">The notification schedule service used to compute Daytime expiry.</param>
    public EmailOrderProcessingService(
        IEmailNotificationRepository emailNotificationRepository,
        IEmailNotificationService emailService,
        IContactPointService contactPointService,
        IKeywordsService keywordsService,
        INotificationScheduleService notificationScheduleService)
    {
        _emailNotificationRepository = emailNotificationRepository;
        _emailService = emailService;
        _contactPointService = contactPointService;
        _keywordsService = keywordsService;
        _notificationScheduleService = notificationScheduleService;
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
        var expirationDateTime = GetExpirationDateTime(order);
        var allEmailRecipients = await GetEmailRecipientsAsync(order, recipients);
        var registeredEmailRecipients = await _emailNotificationRepository.GetRecipients(order.Id);

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

            await _emailService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expirationDateTime,
                emailAddresses,
                emailRecipient,
                order.IgnoreReservation ?? false);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessOrderWithoutAddressLookup(NotificationOrder order, List<Recipient> recipients)
    {
        var expirationDateTime = GetExpirationDateTime(order);
        var allEmailRecipients = await GetEmailRecipientsAsync(order, recipients);

        foreach (var recipient in recipients)
        {
            var emailAddresses = recipient.AddressInfo
                .OfType<EmailAddressPoint>()
                .Where(a => !string.IsNullOrWhiteSpace(a.EmailAddress))
                .ToList();

            var matchedEmailRecipient = FindEmailRecipient(allEmailRecipients, recipient);
            var emailRecipient = matchedEmailRecipient ?? new EmailRecipient { IsReserved = recipient.IsReserved };

            await _emailService.CreateNotification(
                order.Id,
                order.RequestedSendTime,
                expirationDateTime,
                emailAddresses,
                emailRecipient,
                order.IgnoreReservation ?? false);
        }
    }

    /// <summary>
    /// Calculates the expiration date and time for an email notification based on the order's email sending time policy.
    /// </summary>
    /// <param name="order">The notification order.</param>
    /// <returns>
    /// The expiration <see cref="DateTime"/> for the email notification.
    /// If <see cref="NotificationOrder.EmailSendingTimePolicy"/> is <see cref="SendingTimePolicy.Daytime"/>, the expiration is determined by the notification schedule service.
    /// Otherwise, it defaults to 48 hours after the requested send time.
    /// </returns>
    private DateTime GetExpirationDateTime(NotificationOrder order)
    {
        return order.EmailSendingTimePolicy switch
        {
            SendingTimePolicy.Daytime => _notificationScheduleService.GetEmailExpirationDateTime(order.RequestedSendTime),
            _ => order.RequestedSendTime.AddHours(48),
        };
    }

    /// <summary>
    /// Determines whether the specified template part requires customization by checking for placeholder keywords.
    /// </summary>
    /// <param name="templatePart">The part of the email template (subject or body) to evaluate.</param>
    /// <returns><c>true</c> if the template part contains placeholders for recipient-specific customization; otherwise, <c>false</c>.</returns>
    private bool RequiresCustomization(string? templatePart)
    {
        return _keywordsService.ContainsRecipientNumberPlaceholder(templatePart) || _keywordsService.ContainsRecipientNamePlaceholder(templatePart);
    }

    /// <summary>
    /// Retrieves a list of recipients for sending emails, replacing keywords in the subject and body with actual values.
    /// </summary>
    /// <param name="order">The notification order containing the email template and recipients.</param>
    /// <param name="recipients">The list of recipients to process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of email recipients with keywords replaced.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the order or its templates are null.</exception>
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

        if (recipientsWithoutEmail.Count > 0)
        {
            await _contactPointService.AddEmailContactPoints(recipientsWithoutEmail, order.ResourceId, OrderLifecycleStage.Processing, order.ResourceAction);
        }

        return order.Recipients;
    }
}
