﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a request for sending an SMS notification to a specific phone number.
/// </summary>
/// <remarks>
/// This class is used in the API for configuring SMS notification delivery to a single recipient with specific content and delivery preferences.
/// </remarks>
public class RecipientSmsRequestExt
{
    /// <summary>
    /// Gets or sets the recipient's phone number in international format.
    /// </summary>
    /// <remarks>
    /// This is the destination number where the SMS will be delivered.
    /// The phone number should include the country code (e.g., +4799999999).
    /// </remarks>
    [Required]
    [JsonPropertyName("phoneNumber")]
    public required string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the configuration options for the SMS message.
    /// </summary>
    /// <remarks>
    /// Contains sender information, message content, and delivery timing preferences.
    /// </remarks>
    [Required]
    [JsonPropertyName("smsSettings")]
    public required SmsSendingOptionsRequestExt Settings { get; set; }
}
