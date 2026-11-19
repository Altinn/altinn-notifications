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
/// Implementation of <see cref="ISmsNotificationService"/>
/// </summary>
public class SmsNotificationService : ISmsNotificationService
{
    private static readonly ActivitySource _activitySource = new(BackgroundActivitySource.Name);
    private readonly IGuidService _guidService;
    private readonly IDateTimeService _dateTimeService;
    private readonly ISmsNotificationRepository _repository;
    private readonly ISmsCommandPublisher _smsPublisher;
    private readonly IUnitOfWorkRepository _unitOfWorkRepository;
    private readonly ILogger<SmsNotificationService> _logger;
    private readonly ISmsSenderSubstitutionService _senderSubstitutionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationService"/> class.
    /// </summary>
    public SmsNotificationService(
        IGuidService guidService,
        IDateTimeService dateTimeService,
        ISmsNotificationRepository repository,
        ISmsCommandPublisher smsPublisher,
        IUnitOfWorkRepository unitOfWorkRepository,
        ILogger<SmsNotificationService> logger,
        ISmsSenderSubstitutionService senderSubstitutionService)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _repository = repository;
        _smsPublisher = smsPublisher;
        _unitOfWorkRepository = unitOfWorkRepository;
        _logger = logger;
        _senderSubstitutionService = senderSubstitutionService;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SmsNotification>> CreateNotification(Guid orderId, DateTime requestedSendTime, DateTime expiryDateTime, List<SmsAddressPoint> addressPoints, SmsRecipient recipient, bool ignoreReservation = false)
    {
        var notifications = new List<SmsNotification>();

        if (recipient.IsReserved.HasValue && recipient.IsReserved.Value && !ignoreReservation)
        {
            var reservedRecipient = new SmsRecipient
                {
                    IsReserved = recipient.IsReserved,
                    OrganizationNumber = recipient.OrganizationNumber,
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    CustomizedBody = recipient.CustomizedBody,
                    MobileNumber = string.Empty
                };
            notifications.Add(CreateNotificationForRecipient(orderId, requestedSendTime, reservedRecipient, SmsNotificationResultType.Failed_RecipientReserved));
            return Task.FromResult<IReadOnlyList<SmsNotification>>(notifications);
        }

        if (addressPoints.Count == 0)
        {
            notifications.Add(CreateNotificationForRecipient(orderId, requestedSendTime, recipient, SmsNotificationResultType.Failed_RecipientNotIdentified));
            return Task.FromResult<IReadOnlyList<SmsNotification>>(notifications);
        }

        foreach (SmsAddressPoint addressPoint in addressPoints)
        {
            var recipientForAddress = new SmsRecipient
            {
                IsReserved = recipient.IsReserved,
                OrganizationNumber = recipient.OrganizationNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                CustomizedBody = recipient.CustomizedBody,
                MobileNumber = addressPoint.MobileNumber
            };
            notifications.Add(CreateNotificationForRecipient(orderId, requestedSendTime, recipientForAddress, SmsNotificationResultType.New));
        }

        return Task.FromResult<IReadOnlyList<SmsNotification>>(notifications);
    }

    /// <inheritdoc/>
    public async Task<bool> SendNotification(SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime, CancellationToken cancellationToken = default)
    {
        using Activity? activity = _activitySource.StartActivity("SendNotification")?.SetTag("Policy", sendingTimePolicy);
        UnitOfWork unitOfWork;
        try
        {
            unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Failed to start a unit of work for {OperationName}.", nameof(SendNotification));
            }

            return false;
        }

        try
        {
            Sms? newSmsNotification = await _repository.GetNewNotification(unitOfWork, cancellationToken, sendingTimePolicy);
            activity?.SetTag("Count", newSmsNotification == null ? 0 : 1);
            if (newSmsNotification is null)
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await ApplySenderSubstitution(unitOfWork, newSmsNotification);

            await PublishClaimedNotification(unitOfWork, newSmsNotification, cancellationToken);

            await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);

            return true;
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "An error occurred while processing {OperationName}.", nameof(SendNotification));
            }

            try
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(rollbackException, "Failed to roll back the unit of work for {OperationName}.", nameof(SendNotification));
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public Task TerminateExpiredNotifications()
    {
        return _repository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SmsSendOperationResult sendOperationResult)
    {
        UnitOfWork unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        try
        {
            await _repository.UpdateSendStatusAsync(
                unitOfWork,
                sendOperationResult.NotificationId,
                sendOperationResult.SendResult,
                sendOperationResult.GatewayReference,
                sendOperationResult.DeliveryReport);
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
    /// Substitutes the sender for SMS notifications whose recipient phone number matches a
    /// configured substitution rule for the notification's service owner.
    /// </summary>
    /// <param name="unitOfWork">The active unit of work for the current send operation.</param>
    /// <param name="sms">The SMS notification about to be published.</param>
    /// <remarks>
    /// Skips all work when no substitution rules are configured, so the common case (no
    /// substitution in use) adds no per-recipient overhead.
    /// </remarks>
    private async Task ApplySenderSubstitution(UnitOfWork unitOfWork, Sms sms)
    {
        if (!_senderSubstitutionService.HasRules)
        {
            return;
        }

        var smsSenderResolutionResult = _senderSubstitutionService.ResolveSender(sms.Sender, sms.Recipient, sms.Creator);

        if (smsSenderResolutionResult.WasSubstituted)
        {
            sms.Sender = smsSenderResolutionResult.Sender;
            await _repository.PersistSubstitutedSender(unitOfWork, sms.NotificationId, sms.Sender);
        }
    }

    /// <summary>
    /// Publishes a claimed SMS notification. If it exceeds the size limit, records <see cref="SmsNotificationResultType.Failed_ContentTooLarge"/> on the claiming unit of work.
    /// </summary>
    /// <param name="unitOfWork">The unit of work that holds the claim.</param>
    /// <param name="claimedNotification">The claimed SMS notification to publish.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    private async Task PublishClaimedNotification(UnitOfWork unitOfWork, Sms claimedNotification, CancellationToken cancellationToken)
    {
        try
        {
            await _smsPublisher.PublishAsync(claimedNotification, cancellationToken);
        }
        catch (NotificationSizeLimitExceededException sizeLimitExceeded)
        {
            _logger.LogWarning(sizeLimitExceeded, "Notification {NotificationId} is too large to publish for {OperationName}.", claimedNotification.NotificationId, nameof(SendNotification));

            await _repository.UpdateSendStatusAsync(unitOfWork, claimedNotification.NotificationId, SmsNotificationResultType.Failed_ContentTooLarge, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Builds an in-memory SMS notification for a single recipient. Does not persist.
    /// </summary>
    private SmsNotification CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, SmsRecipient recipient, SmsNotificationResultType resultType)
    {
        return new SmsNotification()
        {
            OrderId = orderId,
            Id = _guidService.NewGuid(),
            Recipient = recipient,
            RequestedSendTime = requestedSendTime,
            SendResult = new(resultType, _dateTimeService.UtcNow())
        };
    }
}
