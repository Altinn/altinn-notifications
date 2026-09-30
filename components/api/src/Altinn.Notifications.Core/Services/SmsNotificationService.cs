using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="ISmsNotificationService"/>
/// </summary>
public class SmsNotificationService : ISmsNotificationService
{
    private readonly IGuidService _guidService;
    private readonly IDateTimeService _dateTimeService;
    private readonly ISmsNotificationRepository _repository;
    private readonly ISendSmsPublisher _smsPublisher;
    private readonly IUnitOfWorkRepository _unitOfWorkRepository;
    private readonly ILogger<SmsNotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationService"/> class.
    /// </summary>
    public SmsNotificationService(
        IGuidService guidService,
        IDateTimeService dateTimeService,
        ISmsNotificationRepository repository,
        ISendSmsPublisher smsPublisher,
        IOptions<NotificationConfig> notificationConfig,
        IUnitOfWorkRepository unitOfWorkRepository,
        ILogger<SmsNotificationService> logger)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _repository = repository;
        _smsPublisher = smsPublisher;
        _unitOfWorkRepository = unitOfWorkRepository;
        _logger = logger;

        _ = notificationConfig;
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
    public async Task<bool> SendNotifications(CancellationToken cancellationToken, SendingTimePolicy sendingTimePolicy = SendingTimePolicy.Daytime)
    {
        UnitOfWork unitOfWork;
        try
        {
            unitOfWork = await _unitOfWorkRepository.StartUnitOfWork();
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Failed to start a unit of work for {OperationName}.", nameof(SendNotifications));
            }

            return false;
        }

        try
        {
            Sms? newSmsNotification = await _repository.GetNewNotification(unitOfWork, cancellationToken, sendingTimePolicy);
            if (newSmsNotification is null)
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _smsPublisher.PublishAsync(newSmsNotification, cancellationToken);
            await _unitOfWorkRepository.CommitUnitOfWork(unitOfWork);
            return true;
        }
        catch (Exception e)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "An error occurred while processing {OperationName}.", nameof(SendNotifications));
            }

            try
            {
                await _unitOfWorkRepository.RollbackUnitOfWork(unitOfWork);
            }
            catch (Exception)
            {
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task TerminateExpiredNotifications()
    {
        await _repository.TerminateExpiredNotifications();
    }

    /// <inheritdoc/>
    public async Task UpdateSendStatus(SmsSendOperationResult sendOperationResult)
    {
        await _repository.UpdateSendStatus(
            sendOperationResult.NotificationId,
            sendOperationResult.SendResult,
            sendOperationResult.GatewayReference,
            sendOperationResult.DeliveryReport);
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
