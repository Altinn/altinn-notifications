﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Class representing an SMS notiication order request
/// </summary>
/// <remarks>
/// External representation to be used in the API.
/// </remarks>
public class SmsNotificationOrderRequestExt : NotificationOrderRequestBaseExt
{
    /// <summary>
    /// Gets or sets the sender number of the SMS 
    /// </summary>
    [JsonPropertyName("senderNumber")]
    public string SenderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body of the SMS
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
