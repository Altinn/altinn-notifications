using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Class representing a notification order
/// </summary>
public class NotificationOrder
{
    /// <summary>
    /// Gets the id of the notification order
    /// </summary>
    public string Id { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the senders reference of a notification
    /// </summary>
    public string? SendersReference { get; internal set; }

    /// <summary>
    /// Gets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; internal set; } = new List<INotificationTemplate>();

    /// <summary>
    /// Gets the requested send time for the notification(s)
    /// </summary>
    public DateTime RequestedSendTime { get; internal set; }

    /// <summary>
    /// Gets the preferred notification channel
    /// </summary>
    public NotificationChannel NotificationChannel { get; internal set; }

    /// <summary>
    /// Gets the creator of the notification
    /// </summary>
    public Creator Creator { get; internal set; }

    /// <summary>
    /// Gets the date and time for when the notification order was created
    /// </summary>
    public DateTime Created { get; internal set; }

    /// <summary>
    /// Gets a list of recipients
    /// </summary>
    public List<Recipient> Recipients { get; internal set; } = new List<Recipient>();

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrder"/> class.
    /// </summary>
    public NotificationOrder(string id, string? sendersReference, List<INotificationTemplate> templates, DateTime requestedSendTime, NotificationChannel notificationChannel, Creator creator, DateTime created, List<Recipient> recipients)
    {
        Id = id;
        SendersReference = sendersReference;
        Templates = templates;
        RequestedSendTime = requestedSendTime;
        NotificationChannel = notificationChannel;
        Creator = creator;
        Created = created;
        Recipients = recipients;
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
        return JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });
    }

    /// <summary>
    /// Deserialize a json string into the <see cref="NotificationOrder"/>
    /// </summary>
    public static NotificationOrder? Deserialize(string serializedString)
    {
        return JsonSerializer.Deserialize<NotificationOrder>(
            serializedString,
            new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
    }
}