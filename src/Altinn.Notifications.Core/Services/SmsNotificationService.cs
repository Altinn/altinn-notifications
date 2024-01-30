using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of <see cref="ISmsNotificationService"/>
/// </summary>
public class SmsNotificationService : ISmsNotificationService
{
    private readonly IGuidService _guid;
    private readonly IDateTimeService _dateTime;
    private readonly ISmsNotificationRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsNotificationService"/> class.
    /// </summary>
    public SmsNotificationService(
        IGuidService guid,
        IDateTimeService dateTime,
        ISmsNotificationRepository repository)
    {
        _guid = guid;
        _dateTime = dateTime;
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task CreateNotification(Guid orderId, DateTime requestedSendTime, Recipient recipient)
    {
        SmsAddressPoint? addressPoint = recipient.AddressInfo.Find(a => a.AddressType == AddressType.Sms) as SmsAddressPoint;

        await CreateNotificationForRecipient(orderId, requestedSendTime, recipient.RecipientId, addressPoint!.MobileNumber);

    }

    private async Task CreateNotificationForRecipient(Guid orderId, DateTime requestedSendTime, string recipientId, string recipientNumber)
    {
        var smsNotification = new SmsNotification()
        {
            Id = _guid.NewGuid(),
            OrderId = orderId,
            RequestedSendTime = requestedSendTime,
            RecipientNumber = recipientNumber,
            RecipientId = string.IsNullOrEmpty(recipientId) ? null : recipientId,
            SendResult = new(SmsNotificationResultType.New, _dateTime.UtcNow())
        };

        await _repository.AddNotification(smsNotification, requestedSendTime.AddHours(1));
    }
}
