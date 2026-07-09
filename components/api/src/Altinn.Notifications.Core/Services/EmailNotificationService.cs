using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="IEmailNotificationService"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EmailNotificationService"/> class.
/// </remarks>
public class EmailNotificationService(
    IGuidService guidService,
    IDateTimeService dateTimeService,
    IEmailCommandPublisher emailCommandPublisher,
    IOptions<NotificationConfig> notificationConfig,
    IEmailNotificationRepository emailNotificationRepository,
    IComposedEmailCommandPublisher composedEmailCommandPublisher) : IEmailNotificationService
{
    private readonly IGuidService _guidService = guidService;
    private readonly IDateTimeService _dateTimeService = dateTimeService;
    private readonly IEmailCommandPublisher _emailCommandPublisher = emailCommandPublisher;
    private readonly int _emailPublishBatchSize = notificationConfig.Value.EmailPublishBatchSize;
    private readonly IEmailNotificationRepository _emailNotificationRepository = emailNotificationRepository;
    private readonly int _composedEmailPublishBatchSize = notificationConfig.Value.ComposedEmailPublishBatchSize;
    private readonly IComposedEmailCommandPublisher _composedEmailCommandPublisher = composedEmailCommandPublisher;

    /// <inheritdoc/>
    public Task<IReadOnlyList<EmailNotification>> CreateNotification(Guid orderId, DateTime requestedSendTime, List<EmailAddressPoint> emailAddresses, EmailRecipient emailRecipient, bool ignoreReservation = false)
    {
        var notifications = new List<EmailNotification>();

        if (emailRecipient.IsReserved.HasValue && emailRecipient.IsReserved.Value && !ignoreReservation)
        {
            emailRecipient.ToAddress = string.Empty;
            notifications.Add(CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.Failed_RecipientReserved));
            return Task.FromResult<IReadOnlyList<EmailNotification>>(notifications);
        }

        if (emailAddresses.Count == 0)
        {
            notifications.Add(CreateNotificationForRecipient(orderId, requestedSendTime, emailRecipient, EmailNotificationResultType.Failed_RecipientNotIdentified));
            return Task.FromResult<IReadOnlyList<EmailNotification>>(notifications);
        }

        foreach (EmailAddressPoint addressPoint in emailAddresses)
        {
            var recipientForAddress = new EmailRecipient
            {
                IsReserved = emailRecipient.IsReserved,
                OrganizationNumber = emailRecipient.OrganizationNumber,
                NationalIdentityNumber = emailRecipient.NationalIdentityNumber,
                CustomizedBody = emailRecipient.CustomizedBody,
                CustomizedSubject = emailRecipient.CustomizedSubject,
                ToAddress = addressPoint.EmailAddress
            };
            notifications.Add(CreateNotificationForRecipient(orderId, requestedSendTime, recipientForAddress, EmailNotificationResultType.New));
        }

        return Task.FromResult<IReadOnlyList<EmailNotification>>(notifications);
    }

    /// <inheritdoc/>
    public async Task TerminateExpiredNotifications()
    {
        // process hanging notifications that have been set to succeeded, but never reached a final stage
        await _emailNotificationRepository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public async Task SendNotifications(CancellationToken cancellationToken)
    {
        List<Email> claimedNotifications;

        do
        {
            IReadOnlyList<Email> unpublishedNotifications = [];

            try
            {
                unpublishedNotifications =
                    claimedNotifications =
                    await _emailNotificationRepository.GetNewNotificationsAsync(_emailPublishBatchSize, cancellationToken);
                if (claimedNotifications.Count == 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                unpublishedNotifications = await _emailCommandPublisher.PublishAsync(claimedNotifications, cancellationToken);

                await ResetSendStatusToNewAsync(unpublishedNotifications);
            }
            catch (Exception)
            {
                await ResetSendStatusToNewAsync(unpublishedNotifications);

                throw;
            }
        }
        while (claimedNotifications.Count > 0);
    }

    /// <inheritdoc/>
    public async Task SendComposedNotifications(CancellationToken cancellationToken)
    {
        List<ComposedEmail> claimedNotifications;

        do
        {
            claimedNotifications = await _emailNotificationRepository.GetNewComposedNotificationsAsync(_composedEmailPublishBatchSize, cancellationToken);
            if (claimedNotifications.Count == 0)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var unpublishedNotifications = await _composedEmailCommandPublisher.PublishAsync(claimedNotifications, cancellationToken);

            await ResetSendStatusToNewAsync(unpublishedNotifications);
        }
        while (claimedNotifications.Count > 0);
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(EmailSendOperationResult sendOperationResult)
    {
        // set to new to allow new iteration of regular processing if transient error
        if (sendOperationResult.SendResult == EmailNotificationResultType.Failed_TransientError)
        {
            sendOperationResult.SendResult = EmailNotificationResultType.New;
        }

        await _emailNotificationRepository.UpdateSendStatus(
            sendOperationResult.NotificationId,
            (EmailNotificationResultType)sendOperationResult.SendResult!,
            sendOperationResult.OperationId,
            sendOperationResult.DeliveryReport,
            sendOperationResult.EncodedAttachmentsSize);
    }

    /// <summary>
    /// Resets the send status to <see cref="EmailNotificationResultType.New"/> for the given emails.
    /// </summary>
    /// <param name="emails">The collection of emails to reset the send status for.</param>
    private async Task ResetSendStatusToNewAsync(IEnumerable<Email> emails)
    {
        if (emails is null)
        {
            return;
        }

        foreach (var email in emails)
        {
            await _emailNotificationRepository.UpdateSendStatus(email.NotificationId, EmailNotificationResultType.New);
        }
    }

    /// <summary>
    /// Builds an in-memory email notification for a single recipient. Does not persist.
    /// </summary>
    private EmailNotification CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, EmailRecipient recipient, EmailNotificationResultType result)
    {
        return new EmailNotification()
        {
            OrderId = orderId,
            Id = _guidService.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(result, _dateTimeService.UtcNow())
        };
    }
}
