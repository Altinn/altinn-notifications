﻿using System.Text.Json;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents an SMS message.
/// </summary>
public class Sms
{
    /// <summary>
    /// Gets or sets the ID of the SMS.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the sender of the SMS message.
    /// </summary>
    /// <remarks>
    /// Can be a literal string or a phone number.
    /// </remarks>
    public string Sender { get; set; }

    /// <summary>
    /// Gets or sets the recipient of the SMS message.
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the contents of the SMS message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sms"/> class with the specified parameters.
    /// </summary>
    /// <param name="notificationId">The ID of the SMS.</param>
    /// <param name="sender">The sender of the SMS message.</param>
    /// <param name="recipient">The recipient of the SMS message.</param>
    /// <param name="message">The contents of the SMS message.</param>
    public Sms(Guid notificationId, string sender, string recipient, string message)
    {
        NotificationId = notificationId;
        Recipient = recipient;
        Sender = sender;
        Message = message;
    }

    /// <summary>
    /// Serializes the <see cref="Sms"/> object to a JSON string.
    /// </summary>
    /// <returns>A JSON string representation of the <see cref="Sms"/> object.</returns>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }
}
