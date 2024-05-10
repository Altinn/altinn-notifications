using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Class representing a notification order
/// </summary>
public partial class NotificationOrder : IBaseNotificationOrder
{
    /// <inheritdoc/>>
    public Guid Id { get; init; } = Guid.Empty;

    /// <inheritdoc/>>
    public string? SendersReference { get; init; }

    /// <inheritdoc/>>
    public DateTime RequestedSendTime { get; init; }

    /// <inheritdoc/>>
    public NotificationChannel NotificationChannel { get; init; }

    /// <inheritdoc/>>
    public bool IgnoreReservation { get; init; }

    /// <inheritdoc/>>
    public Creator Creator { get; init; }

    /// <inheritdoc/>>
    public DateTime Created { get; init; }

    /// <summary>
    /// Gets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; init; } = new List<INotificationTemplate>();

    /// <summary>
    /// Gets a list of recipients
    /// </summary>
    public List<Recipient> Recipients { get; init; } = new List<Recipient>();

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrder"/> class.
    /// </summary>
    [JsonConstructor]
    private NotificationOrder()
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

    /// <summary>
    /// Static method to get the builder
    /// </summary>
    public static NotificationOrderBuilder GetBuilder()
    {
        return new NotificationOrderBuilder();
    }
}
