using System.Diagnostics;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Exceptions;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="IEmailNotificationService"/>.
/// </summary>
public class EmailNotificationService(
    IGuidService guidService,
    IDateTimeService dateTimeService,
    IEmailCommandPublisher emailCommandPublisher,
    IEmailNotificationRepository emailNotificationRepository,
    IComposedEmailCommandPublisher composedEmailCommandPublisher,
    IUnitOfWorkRepository unitOfWorkRepository,
    ILogger<EmailNotificationService> logger) : IEmailNotificationService
{
    private static readonly ActivitySource _activitySource = new(BackgroundActivitySource.Name);
    private readonly IGuidService _guidService = guidService;
    private readonly ILogger<EmailNotificationService> _logger = logger;
    private readonly IDateTimeService _dateTimeService = dateTimeService;
    private readonly IUnitOfWorkRepository _unitOfWorkRepository = unitOfWorkRepository;
    private readonly IEmailCommandPublisher _emailCommandPublisher = emailCommandPublisher;
    private readonly IEmailNotificationRepository _emailNotificationRepository = emailNotificationRepository;
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
    public Task TerminateExpiredNotifications()
    {
        return _emailNotificationRepository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public Task<bool> SendNotification(SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Anytime, CancellationToken cancellationToken = default)
    {
        return SendClaimedNotification(
            (unitOfWork, token) => _emailNotificationRepository.GetNewNotificationAsync(unitOfWork, token, sendingTimePolicy),
            (notification, token) => _emailCommandPublisher.PublishAsync(notification, token),
            nameof(SendNotification),
            cancellationToken,
            sendingTimePolicy);
    }

    /// <inheritdoc/>
    public Task<bool> SendComposedNotification(CancellationToken cancellationToken)
    {
        return SendClaimedNotification(
            _emailNotificationRepository.GetNewComposedNotificationAsync,
            _composedEmailCommandPublisher.PublishAsync,
            nameof(SendComposedNotification),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(EmailSendOperationResult sendOperationResult)
    {
        if (sendOperationResult.SendResult == EmailNotificationResultType.Failed_TransientError)
        {
            sendOperationResult.SendResult = EmailNotificationResultType.New;
        }

        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        try
        {
            await _emailNotificationRepository.UpdateSendStatusAsync(
                unitOfWork,
                sendOperationResult.NotificationId,
                (EmailNotificationResultType)sendOperationResult.SendResult!,
                sendOperationResult.OperationId,
                sendOperationResult.DeliveryReport,
                sendOperationResult.TotalAttachmentSizeBytes);
        }
        catch (Exception)
        {
            try
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(rollbackException, "Failed to roll back the unit of work for {OperationName}.", nameof(UpdateSendStatus));
            }

            throw;
        }

        await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
    }

    /// <summary>
    /// Claims, publishes and commits the next pending notification within a single unit of work.
    /// </summary>
    /// <typeparam name="TNotification">The type of email notification to send.</typeparam>
    /// <param name="getNewNotification">Claims the next pending notification on the given unit of work, or returns <see langword="null"/> if none is pending.</param>
    /// <param name="publishNotification">Publishes the claimed notification.</param>
    /// <param name="operationName">The name of the calling operation, used for logging and tracing.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <param name="sendingTimePolicy">The sending time policy the claim is restricted to, added as a tag on the trace activity. <see langword="null"/> when the claim is not policy-specific.</param>
    /// <returns><see langword="true"/> if a notification was claimed and processed; otherwise <see langword="false"/>.</returns>
    private async Task<bool> SendClaimedNotification<TNotification>(
        Func<UnitOfWork, CancellationToken, Task<TNotification?>> getNewNotification,
        Func<TNotification, CancellationToken, Task> publishNotification,
        string operationName,
        CancellationToken cancellationToken,
        SendingTimePolicy? sendingTimePolicy = null)
        where TNotification : Email
    {
        using Activity? activity = _activitySource.StartActivity("SendNotification")?.SetTag("Kind", operationName)?.SetTag("Policy", sendingTimePolicy);
        UnitOfWork unitOfWork;
        try
        {
            unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Failed to start a unit of work for {OperationName}.", operationName);
            }

            return false;
        }

        try
        {
            TNotification? claimedNotification = await getNewNotification(unitOfWork, cancellationToken);
            activity?.SetTag("Count", claimedNotification == null ? 0 : 1);
            if (claimedNotification is null)
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await PublishClaimedNotification(unitOfWork, claimedNotification, publishNotification, operationName, cancellationToken);

            await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

            return true;
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "An error occurred while processing {OperationName}.", operationName);
            }

            try
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(rollbackException, "Failed to roll back the unit of work for {OperationName}.", operationName);
            }

            return false;
        }
    }

    /// <summary>
    /// Publishes a claimed notification. If it exceeds the size limit, records <see cref="EmailNotificationResultType.Failed_ContentTooLarge"/> on the claiming unit of work.
    /// </summary>
    private async Task PublishClaimedNotification<TNotification>(
        UnitOfWork unitOfWork,
        TNotification claimedNotification,
        Func<TNotification, CancellationToken, Task> publishNotification,
        string operationName,
        CancellationToken cancellationToken)
        where TNotification : Email
    {
        try
        {
            await publishNotification(claimedNotification, cancellationToken);
        }
        catch (NotificationSizeLimitExceededException sizeLimitExceeded)
        {
            _logger.LogWarning(sizeLimitExceeded, "Notification {NotificationId} is too large to publish for {OperationName}.", claimedNotification.NotificationId, operationName);

            await _emailNotificationRepository.UpdateSendStatusAsync(unitOfWork, claimedNotification.NotificationId, EmailNotificationResultType.Failed_ContentTooLarge, cancellationToken: cancellationToken);
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
