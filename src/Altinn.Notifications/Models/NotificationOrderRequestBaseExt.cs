﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Base class for common properties of notification order requests.
/// </summary>
public class NotificationOrderRequestBaseExt : NotificationOrderBaseExt
{
    /// <summary>
    /// Gets or sets the list of recipients.
    /// </summary>
    [Required]
    [JsonPropertyName("recipients")]
    public List<RecipientExt> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets whether notifications generated by this order should ignore KRR reservations.
    /// </summary>
    [JsonPropertyName("ignoreReservation")]
    public bool? IgnoreReservation { get; set; }

    /// <summary>
    /// Gets or sets the ID of the resource that the notification is related to.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }
}
