﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Base class for common properties of notification order requests
/// </summary>
public class NotificationOrderRequestBaseExt
{
    /// <summary>
    /// Gets or sets the send time of the email. Defaults to UtcNow
    /// </summary>
    [JsonPropertyName("requestedSendTime")]
    public DateTime RequestedSendTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the senders reference on the notification
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the list of recipients
    /// </summary>
    [JsonPropertyName("recipients")]
    public List<RecipientExt> Recipients { get; set; } = new List<RecipientExt>();

    /// <summary>
    /// Gets or sets whether notifications generated by this order should ignore KRR reservations
    /// </summary>
    [JsonPropertyName("ignoreReservation")]
    public bool IgnoreReservation { get; set; }

    /// <summary>
    /// Gets or sets the id of the resource that the notification is related to
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Json serialized the <see cref="EmailNotificationOrderRequestExt"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}
