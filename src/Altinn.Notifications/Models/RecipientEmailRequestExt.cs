﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a request for sending an email notification to a specific email address.
/// </summary>
/// <remarks>
/// This class is used in the API for configuring email notification delivery to a single recipient
/// with specific content and delivery preferences.
/// </remarks>
public class RecipientEmailRequestExt
{
    /// <summary>
    /// Gets or sets the email address of the intended recipient.
    /// </summary>
    /// <remarks>
    /// This is the destination address where the email will be delivered.
    /// </remarks>
    [Required]
    [JsonPropertyOrder(1)]
    [JsonPropertyName("emailAddress")]
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the email message.
    /// </summary>
    /// <remarks>
    /// These settings control how and when the email will be composed and delivered to the recipient.
    /// </remarks>
    [Required]
    [JsonPropertyOrder(2)]
    [JsonPropertyName("emailSettings")]
    public required EmailSendingOptionsRequestExt Settings { get; set; }
}
