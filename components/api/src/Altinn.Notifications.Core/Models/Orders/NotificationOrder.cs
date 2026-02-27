using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Class representing a notification order
/// </summary>
public class NotificationOrder : IBaseNotificationOrder
{
    /// <inheritdoc/>>
    public Guid Id { get; internal set; } = Guid.Empty;

    /// <inheritdoc/>>
    public string? SendersReference { get; internal set; }

    /// <inheritdoc/>>
    public DateTime RequestedSendTime { get; internal set; }

    /// <inheritdoc/>>
    public NotificationChannel NotificationChannel { get; internal set; }

    /// <inheritdoc/>>    
    public bool? IgnoreReservation { get; internal set; }

    /// <inheritdoc/>>
    public string? ResourceId { get; internal set; }

    /// <inheritdoc/>>
    public Uri? ConditionEndpoint { get; set; }

    /// <inheritdoc/>>
    public Creator Creator { get; internal set; }

    /// <inheritdoc/>>
    public DateTime Created { get; internal set; }

    /// <inheritdoc/>
    public SendingTimePolicy? SendingTimePolicy { get; internal set; }

    /// <inheritdoc/>
    public OrderType Type { get; internal set; }

    /// <summary>
    /// Gets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; internal set; } = new List<INotificationTemplate>();

    /// <summary>
    /// Gets a list of recipients
    /// </summary>
    public List<Recipient> Recipients { get; internal set; } = new List<Recipient>();

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrder"/> class.
    /// </summary>
    public NotificationOrder(
        Guid id,
        OrderType type,
        Creator creator,
        DateTime created,
        string? resourceId,
        Uri? conditionEndpoint,
        bool? ignoreReservation,
        string? sendersReference,
        DateTime requestedSendTime,
        List<Recipient> recipients,
        SendingTimePolicy? sendingTimePolicy,
        List<INotificationTemplate> templates,
        NotificationChannel notificationChannel)
    {
        Id = id;
        Type = type;
        Creator = creator;
        Created = created;
        Templates = templates;
        Recipients = recipients;
        ResourceId = resourceId;
        SendersReference = sendersReference;
        RequestedSendTime = requestedSendTime;
        IgnoreReservation = ignoreReservation;
        ConditionEndpoint = conditionEndpoint;
        SendingTimePolicy = sendingTimePolicy;
        NotificationChannel = notificationChannel;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrder"/> class.
    /// </summary>
    internal NotificationOrder()
    {
        Creator = new Creator(string.Empty);
    }

    /// <summary>
    /// Json serializes the <see cref="NotificationOrder"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Deserialize a json string into the <see cref="NotificationOrder"/>
    /// </summary>
    public static NotificationOrder? Deserialize(string serializedString)
    {
        return JsonSerializer.Deserialize<NotificationOrder>(serializedString, JsonSerializerOptionsProvider.Options);
    }

    /// <summary>
    /// Try to parse a json string into a<see cref="NotificationOrder"/>
    /// </summary>
    public static bool TryParse(string input, out NotificationOrder value)
    {
        NotificationOrder? parsedOutput;
        value = new NotificationOrder();

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        try
        {
            parsedOutput = Deserialize(input!);

            value = parsedOutput!;
            return value.Id != Guid.Empty;
        }
        catch
        {
            // try parse, we simply return false if fails
        }

        return false;
    }
}
