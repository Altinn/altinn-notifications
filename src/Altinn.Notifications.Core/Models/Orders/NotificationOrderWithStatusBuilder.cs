using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Builder for the <see cref="NotificationOrderWithStatus"/> object
/// </summary>
public class NotificationOrderWithStatusBuilder
{
    private Guid _id;
    private bool _idSet;

    private string? _sendersReference;

    private DateTime _requestedSendTime;
    private bool _requestedSendTimeSet;

    private Creator? _creator;
    private bool _creatorSet;

    private DateTime _created;
    private bool _createdSet;

    private NotificationChannel _notificationChannel;
    private bool _notificationChannelSet;

    private bool _ignoreReservation;

    private ProcessingStatus? _processingStatus;
    private bool _processingStatusSet;

    /// <summary>
    /// Sets the id
    /// </summary>
    public NotificationOrderWithStatusBuilder SetId(Guid id)
    {
        _id = id;
        _idSet = true;
        return this;
    }

    /// <summary>
    /// Sets the senders reference
    /// </summary>
    public NotificationOrderWithStatusBuilder SetSendersReference(string sendersReference)
    {
        _sendersReference = sendersReference;
        return this;
    }

    /// <summary>
    /// Sets the requested send time
    /// </summary>
    public NotificationOrderWithStatusBuilder SetRequestedSendTime(DateTime requestedSendTime)
    {
        _requestedSendTime = requestedSendTime;
        _requestedSendTimeSet = true;
        return this;
    }

    /// <summary>
    /// Sets the creator
    /// </summary>
    public NotificationOrderWithStatusBuilder SetCreator(string creatorName)
    {
        _creator = new(creatorName);
        _creatorSet = true;
        return this;
    }

    /// <summary>
    /// Sets the created date tim
    /// </summary>
    public NotificationOrderWithStatusBuilder SetCreated(DateTime created)
    {
        _created = created;
        _createdSet = true;
        return this;
    }

    /// <summary>
    /// Sts the notificaiton channel
    /// </summary>
    public NotificationOrderWithStatusBuilder SetNotificationChannel(NotificationChannel notificationChannel)
    {
        _notificationChannel = notificationChannel;
        _notificationChannelSet = true;
        return this;
    }

    /// <summary>
    /// Sets the ignore reservation
    /// </summary>
    public NotificationOrderWithStatusBuilder SetIgnoreReservation(bool ignoreReservation)
    {
        _ignoreReservation = ignoreReservation;
        return this;
    }

    /// <summary>
    /// Sets the processing status
    /// </summary>
    public NotificationOrderWithStatusBuilder SetProcessingStatus(ProcessingStatus processingStatus)
    {
        _processingStatus = processingStatus;
        _processingStatusSet = true;
        return this;
    }

    /// <summary>
    /// Constructs a new <see cref="NotificationOrderWithStatus"/> object
    /// </summary>
    public NotificationOrderWithStatus Build()
    {
        if (!_idSet ||
            !_requestedSendTimeSet ||
            !_creatorSet ||
            !_createdSet ||
            !_notificationChannelSet ||
            !_processingStatusSet)
        {
            throw new ArgumentException("Not all required properties are set.");
        }

        var order = new NotificationOrderWithStatus()
        {
            Id = _id,
            SendersReference = _sendersReference,
            RequestedSendTime = _requestedSendTime,
            Creator = _creator!,
            Created = _created,
            NotificationChannel = _notificationChannel,
            IgnoreReservation = _ignoreReservation,
            ProcessingStatus = _processingStatus!,
        };

        return order;
    }
}
