using System.Web;
using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Integrations;
using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Address;
using Altinn.Notifications.Core.Models.InstantEmailService;
using Altinn.Notifications.Core.Models.Notification;
using Altinn.Notifications.Core.Models.NotificationTemplate;
using Altinn.Notifications.Core.Models.Orders;
using Altinn.Notifications.Core.Models.Recipients;
using Altinn.Notifications.Core.Models.ShortMessageService;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implements the <see cref="IInstantOrderRequestService"/> interface
/// to handle the registration, persistence, and tracking of instant notification orders for SMS and Email delivery.
/// </summary>
public class InstantOrderRequestService : IInstantOrderRequestService
{
    private readonly string _defaultSmsSender;
    private readonly string _defaultEmailFromAddress;
    private readonly IGuidService _guidService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IOrderRepository _orderRepository;
    private readonly IShortMessageServiceClient _shortMessageServiceClient;
    private readonly IInstantEmailServiceClient _instantEmailServiceClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstantOrderRequestService"/> class.
    /// </summary>
    public InstantOrderRequestService(
        IGuidService guidService,
        IDateTimeService dateTimeService,
        IOrderRepository instantOrderRepository,
        IOptions<NotificationConfig> configurationOptions,
        IShortMessageServiceClient shortMessageServiceClient,
        IInstantEmailServiceClient instantEmailServiceClient)
    {
        _guidService = guidService;
        _dateTimeService = dateTimeService;
        _orderRepository = instantOrderRepository;
        _shortMessageServiceClient = shortMessageServiceClient;
        _instantEmailServiceClient = instantEmailServiceClient;
        _defaultSmsSender = configurationOptions.Value.DefaultSmsSenderNumber;
        _defaultEmailFromAddress = configurationOptions.Value.DefaultEmailFromAddress;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> RetrieveTrackingInformation(string creatorName, string idempotencyId, CancellationToken cancellationToken = default)
    {
        return await _orderRepository.RetrieveInstantOrderTrackingInformation(creatorName, idempotencyId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantNotificationOrder instantNotificationOrder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deliveryDetails = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails;
        var messageContent = instantNotificationOrder.InstantNotificationRecipient.ShortMessageDeliveryDetails.ShortMessageContent;
        var senderIdentifier = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender;

        var messagesCount = CalculateNumberOfMessages(messageContent.Message);

        var smsNotification = CreateSmsNotification(instantNotificationOrder, deliveryDetails);

        var notificationOrder = CreateNotificationOrder(instantNotificationOrder, deliveryDetails, messageContent);

        var expirationDateTime = notificationOrder.RequestedSendTime.AddSeconds(deliveryDetails.TimeToLiveInSeconds);

        cancellationToken.ThrowIfCancellationRequested();

        // Create the tracking information for the order.
        var trackingInformation = await _orderRepository.Create(instantNotificationOrder, notificationOrder, smsNotification, expirationDateTime, messagesCount, cancellationToken);
        if (trackingInformation != null)
        {
            _ = Task.Run(
                async () =>
                {
                    var shortMessage = new ShortMessage
                    {
                        Message = messageContent.Message,
                        NotificationId = smsNotification.Id,
                        TimeToLive = deliveryDetails.TimeToLiveInSeconds,
                        Recipient = smsNotification.Recipient.MobileNumber,
                        Sender = senderIdentifier
                    };

                    await _shortMessageServiceClient.SendAsync(shortMessage);
                },
                CancellationToken.None);
        }

        return trackingInformation;
    }

    /// <summary>
    /// Calculates the number of SMS messages required to deliver the specified message,
    /// applying SMS gateway concatenation rules:
    /// - Messages up to 160 characters are sent as a single SMS.
    /// - Longer messages are split into segments of 134 characters each (concatenated SMS).
    /// - The total number of segments is capped at 16, per gateway limitations.
    /// The calculation uses the URL-encoded length of the message to account for special characters.
    /// </summary>
    private static int CalculateNumberOfMessages(string message)
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

    /// <summary>
    /// Creates a <see cref="SmsNotification"/> instance for an instant SMS notification.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="ShortMessageDeliveryDetails"/> containing the recipient's phone number.
    /// </param>
    /// <returns>
    /// A <see cref="SmsNotification"/> object populated for SMS delivery.
    /// </returns>
    private SmsNotification CreateSmsNotification(InstantNotificationOrder instantNotificationOrder, ShortMessageDeliveryDetails deliveryDetails)
    {
        return new SmsNotification
        {
            Id = _guidService.NewGuid(),
            Recipient = new SmsRecipient
            {
                MobileNumber = deliveryDetails.PhoneNumber
            },
            OrderId = instantNotificationOrder.OrderId,
            RequestedSendTime = instantNotificationOrder.Created,
            SendResult = new(SmsNotificationResultType.Sending, _dateTimeService.UtcNow())
        };
    }

    /// <summary>
    /// Creates a <see cref="NotificationOrder"/> instance for an instant SMS notification.
    /// </summary>
    /// <param name="instantNotificationOrder">
    /// The <see cref="InstantNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="ShortMessageDeliveryDetails"/> containing recipient phone number and time-to-live.
    /// </param>
    /// <param name="messageContent">
    /// The <see cref="ShortMessageContent"/> containing the SMS message body and sender information.
    /// </param>
    /// <returns>
    /// A <see cref="NotificationOrder"/> object populated for SMS delivery.
    /// </returns>
    private NotificationOrder CreateNotificationOrder(InstantNotificationOrder instantNotificationOrder, ShortMessageDeliveryDetails deliveryDetails, ShortMessageContent messageContent)
    {
        var senderIdentifier = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender;

        return new NotificationOrder
        {
            ResourceId = null,
            IgnoreReservation = null,
            ConditionEndpoint = null,
            Type = instantNotificationOrder.Type,
            Id = instantNotificationOrder.OrderId,
            Creator = instantNotificationOrder.Creator,
            Created = instantNotificationOrder.Created,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = instantNotificationOrder.Created,
            SendersReference = instantNotificationOrder.SendersReference,
            Recipients = [new([new SmsAddressPoint(deliveryDetails.PhoneNumber)])],
            Templates = [new SmsTemplate(senderIdentifier, messageContent.Message)]
        };
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> PersistInstantSmsNotificationAsync(InstantSmsNotificationOrder instantSmsNotificationOrder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deliveryDetails = instantSmsNotificationOrder.ShortMessageDeliveryDetails;
        var messageContent = instantSmsNotificationOrder.ShortMessageDeliveryDetails.ShortMessageContent;
        var senderIdentifier = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender;

        var messagesCount = CalculateNumberOfMessages(messageContent.Message);

        var smsNotification = CreateSmsNotificationFromSms(instantSmsNotificationOrder, deliveryDetails);

        var notificationOrder = CreateNotificationOrderFromSms(instantSmsNotificationOrder, deliveryDetails, messageContent);

        var expirationDateTime = notificationOrder.RequestedSendTime.AddSeconds(deliveryDetails.TimeToLiveInSeconds);

        cancellationToken.ThrowIfCancellationRequested();

        // Create the tracking information for the order.
        var trackingInformation = await _orderRepository.Create(instantSmsNotificationOrder, notificationOrder, smsNotification, expirationDateTime, messagesCount, cancellationToken);
        if (trackingInformation != null)
        {
            _ = Task.Run(
                async () =>
                {
                    var shortMessage = new ShortMessage
                    {
                        Message = messageContent.Message,
                        NotificationId = smsNotification.Id,
                        TimeToLive = deliveryDetails.TimeToLiveInSeconds,
                        Recipient = smsNotification.Recipient.MobileNumber,
                        Sender = senderIdentifier
                    };

                    await _shortMessageServiceClient.SendAsync(shortMessage);
                },
                CancellationToken.None);
        }

        return trackingInformation;
    }

    /// <inheritdoc/>
    public async Task<InstantNotificationOrderTracking?> PersistInstantEmailNotificationAsync(InstantEmailNotificationOrder instantEmailNotificationOrder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deliveryDetails = instantEmailNotificationOrder.InstantEmailDetails;
        var emailContent = deliveryDetails.EmailContent;
        var senderEmailAddress = string.IsNullOrWhiteSpace(emailContent.FromAddress) ? _defaultEmailFromAddress : emailContent.FromAddress;

        var emailNotification = CreateEmailNotification(instantEmailNotificationOrder, deliveryDetails);

        var notificationOrder = CreateNotificationOrderFromEmail(instantEmailNotificationOrder, deliveryDetails, emailContent);

        cancellationToken.ThrowIfCancellationRequested();

        // Create the tracking information for the order.
        var trackingInformation = await _orderRepository.Create(instantEmailNotificationOrder, notificationOrder, emailNotification, cancellationToken);
        if (trackingInformation != null)
        {
            _ = Task.Run(
                async () =>
                {
                    var instantEmail = new InstantEmail
                    {
                        Subject = emailContent.Subject,
                        Body = emailContent.Body,
                        ContentType = emailContent.ContentType,
                        Sender = senderEmailAddress,
                        Recipient = emailNotification.Recipient.ToAddress,
                        NotificationId = emailNotification.Id
                    };

                    await _instantEmailServiceClient.SendAsync(instantEmail);
                },
                CancellationToken.None);
        }

        return trackingInformation;
    }

    /// <summary>
    /// Creates a <see cref="SmsNotification"/> instance for an instant SMS notification from flattened SMS order.
    /// </summary>
    /// <param name="instantSmsNotificationOrder">
    /// The <see cref="InstantSmsNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="ShortMessageDeliveryDetails"/> containing the recipient's phone number.
    /// </param>
    /// <returns>
    /// A <see cref="SmsNotification"/> object populated for SMS delivery.
    /// </returns>
    private SmsNotification CreateSmsNotificationFromSms(InstantSmsNotificationOrder instantSmsNotificationOrder, ShortMessageDeliveryDetails deliveryDetails)
    {
        return new SmsNotification
        {
            Id = _guidService.NewGuid(),
            Recipient = new SmsRecipient
            {
                MobileNumber = deliveryDetails.PhoneNumber
            },
            OrderId = instantSmsNotificationOrder.OrderId,
            RequestedSendTime = instantSmsNotificationOrder.Created,
            SendResult = new(SmsNotificationResultType.Sending, _dateTimeService.UtcNow())
        };
    }

    /// <summary>
    /// Creates a <see cref="NotificationOrder"/> instance for an instant SMS notification from flattened SMS order.
    /// </summary>
    /// <param name="instantSmsNotificationOrder">
    /// The <see cref="InstantSmsNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="ShortMessageDeliveryDetails"/> containing recipient phone number and time-to-live.
    /// </param>
    /// <param name="messageContent">
    /// The <see cref="ShortMessageContent"/> containing the SMS message body and sender information.
    /// </param>
    /// <returns>
    /// A <see cref="NotificationOrder"/> object populated for SMS delivery.
    /// </returns>
    private NotificationOrder CreateNotificationOrderFromSms(InstantSmsNotificationOrder instantSmsNotificationOrder, ShortMessageDeliveryDetails deliveryDetails, ShortMessageContent messageContent)
    {
        var senderIdentifier = string.IsNullOrWhiteSpace(messageContent.Sender) ? _defaultSmsSender : messageContent.Sender;

        return new NotificationOrder
        {
            ResourceId = null,
            IgnoreReservation = null,
            ConditionEndpoint = null,
            Type = instantSmsNotificationOrder.Type,
            Id = instantSmsNotificationOrder.OrderId,
            Creator = instantSmsNotificationOrder.Creator,
            Created = instantSmsNotificationOrder.Created,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Sms,
            RequestedSendTime = instantSmsNotificationOrder.Created,
            SendersReference = instantSmsNotificationOrder.SendersReference,
            Recipients = [new([new SmsAddressPoint(deliveryDetails.PhoneNumber)])],
            Templates = [new SmsTemplate(senderIdentifier, messageContent.Message)]
        };
    }

    /// <summary>
    /// Creates an <see cref="EmailNotification"/> instance for an instant email notification.
    /// </summary>
    /// <param name="instantEmailNotificationOrder">
    /// The <see cref="InstantEmailNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="InstantEmailDetails"/> containing the recipient's email address.
    /// </param>
    /// <returns>
    /// An <see cref="EmailNotification"/> object populated for email delivery.
    /// </returns>
    private EmailNotification CreateEmailNotification(InstantEmailNotificationOrder instantEmailNotificationOrder, InstantEmailDetails deliveryDetails)
    {
        return new EmailNotification
        {
            Id = _guidService.NewGuid(),
            Recipient = new EmailRecipient
            {
                ToAddress = deliveryDetails.EmailAddress
            },
            OrderId = instantEmailNotificationOrder.OrderId,
            RequestedSendTime = instantEmailNotificationOrder.Created,
            SendResult = new(EmailNotificationResultType.Sending, _dateTimeService.UtcNow())
        };
    }

    /// <summary>
    /// Creates a <see cref="NotificationOrder"/> instance for an instant email notification.
    /// </summary>
    /// <param name="instantEmailNotificationOrder">
    /// The <see cref="InstantEmailNotificationOrder"/> containing core order metadata such as type, ID, creator, and timestamps.
    /// </param>
    /// <param name="deliveryDetails">
    /// The <see cref="InstantEmailDetails"/> containing recipient email address.
    /// </param>
    /// <param name="emailContent">
    /// The <see cref="InstantEmailContent"/> containing the email subject, body, and sender information.
    /// </param>
    /// <returns>
    /// A <see cref="NotificationOrder"/> object populated for email delivery.
    /// </returns>
    private NotificationOrder CreateNotificationOrderFromEmail(InstantEmailNotificationOrder instantEmailNotificationOrder, InstantEmailDetails deliveryDetails, InstantEmailContent emailContent)
    {
        var senderEmailAddress = string.IsNullOrWhiteSpace(emailContent.FromAddress) ? _defaultEmailFromAddress : emailContent.FromAddress;

        return new NotificationOrder
        {
            ResourceId = null,
            IgnoreReservation = null,
            ConditionEndpoint = null,
            Type = instantEmailNotificationOrder.Type,
            Id = instantEmailNotificationOrder.OrderId,
            Creator = instantEmailNotificationOrder.Creator,
            Created = instantEmailNotificationOrder.Created,
            SendingTimePolicy = SendingTimePolicy.Anytime,
            NotificationChannel = NotificationChannel.Email,
            RequestedSendTime = instantEmailNotificationOrder.Created,
            SendersReference = instantEmailNotificationOrder.SendersReference,
            Recipients = [new([new EmailAddressPoint(deliveryDetails.EmailAddress)])],
            Templates = [new EmailTemplate(senderEmailAddress, emailContent.Subject, emailContent.Body, emailContent.ContentType)]
        };
    }
}
