﻿using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Orders;

/// <summary>
/// Class representing a notification order request
/// </summary>
public class NotificationOrderRequest
{
    /// <summary>
    /// Gets the senders reference of a notification
    /// </summary>
    public string? SendersReference { get; internal set; }

    /// <summary>
    /// Gets the templates to create notifications based of
    /// </summary>
    public List<INotificationTemplate> Templates { get; internal set; }

    /// <summary>
    /// Gets the requested send time for the notification(s)
    /// </summary>
    public DateTime RequestedSendTime { get; internal set; }

    /// <summary>
    /// Gets the preferred notification channel
    /// </summary>
    public NotificationChannel NotificationChannel { get; internal set; }

    /// <summary>
    /// Gets a list of recipients
    /// </summary>
    public List<Recipient> Recipients { get; internal set; }

    /// <summary>
    /// Gets the creator of the notification request
    /// </summary>
    public Creator Creator { get; internal set; }

    /// <summary>
    /// Gets or sets whether notifications generated by this order should ignore KRR reservations
    /// </summary>
    public bool IgnoreReservation { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationOrderRequest"/> class.
    /// </summary>
    [JsonConstructor]
    internal NotificationOrderRequest()
    {
        Creator = new Creator(string.Empty);
        Templates = new List<INotificationTemplate>();
        Recipients = new List<Recipient>();
    }

    /// <summary>
    /// Static method to get the builder
    /// </summary>
    public static NotificationOrderRequestBuilder GetBuilder()
    {
        return new NotificationOrderRequestBuilder();
    }
}
