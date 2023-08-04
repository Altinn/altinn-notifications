using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// A class representing a registered notification order with status information. 
/// </summary>
public class NotificationOrderWithStatus : IBaseNotificationOrder
{
    /// <inheritdoc/>>
    public Guid Id { get; internal set; }

    /// <inheritdoc/>>
    public string? SendersReference { get; internal set; }

    /// <inheritdoc/>>
    public DateTime RequestedSendTime { get; internal set; }

    /// <inheritdoc/>>
    public Creator Creator { get; internal set; } = new(string.Empty);

    /// <inheritdoc/>>
    public DateTime Created { get; internal set; }

    /// <inheritdoc/>>
    public NotificationChannel NotificationChannel { get; internal set; }

    /// <summary>
    /// Gets the processing status of the notication order
    /// </summary>
    public ProcessingStatus ProcessingStatus { get; internal set; } = new();

    /// <summary>
    /// Gets the summary of the notifiications statuses
    /// </summary>
    public Dictionary<NotificationTemplateType, NotificationStatus> NotificationStatuses { get; set; } = new();
}

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class ProcessingStatus
{
    /// <summary>
    /// Gets the status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the description
    /// </summary>
    [JsonPropertyName("description")]
    public string StatusDescription { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the date time of when the status was last updated
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; internal set; }
}

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationStatus
{
    /// <summary>
    /// Gets the number of generated notifications
    /// </summary>    
    [JsonPropertyName("generated")]
    public int Generated { get; internal set; }

    /// <summary>
    /// Gets the number of succeeeded notifications
    /// </summary>
    [JsonPropertyName("succeeded")]
    public int Succeeded { get; internal set; }
}