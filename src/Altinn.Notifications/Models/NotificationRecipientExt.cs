﻿using System.Text.Json.Serialization;

using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Recipient;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a container for specifying the recipient of a notification order.
/// </summary>
/// <remarks>
/// This class serves as a request wrapper that supports multiple targeting methods for notifications,
/// allowing clients to specify exactly one recipient type per notification order request.
/// </remarks>
public class NotificationRecipientExt
{
    /// <summary>
    /// Gets or sets the configuration for delivering a notification directly to an email address.
    /// </summary>
    /// <remarks>
    /// Use when you have the recipient's email address and want to send an email notification directly.
    /// </remarks>
    [JsonPropertyName("recipientEmail")]
    public RecipientEmailExt? RecipientEmail { get; set; }

    /// <summary>
    /// Gets or sets the configuration for delivering a notification directly to a phone number.
    /// </summary>
    /// <remarks>
    /// Use when you have the recipient's phone number and want to send an SMS notification directly.
    /// </remarks>
    [JsonPropertyName("recipientSms")]
    public RecipientSmsExt? RecipientSms { get; set; }

    /// <summary>
    /// Gets or sets the configuration for delivering a notification to a person.
    /// </summary>
    /// <remarks>
    /// Use when targeting an individual by their national identity number, where contact information
    /// will be retrieved from the Common Contact Register (KRR).
    /// </remarks>
    [JsonPropertyName("recipientPerson")]
    public RecipientPersonExt? RecipientPerson { get; set; }

    /// <summary>
    /// Gets or sets the configuration for delivering a notification to an organization.
    /// </summary>
    /// <remarks>
    /// Use when targeting an organization by its organization number, where contact information
    /// will be retrieved from the Norwegian Central Coordinating Register for Legal Entities (Einingsregisteret).
    /// </remarks>
    [JsonPropertyName("recipientOrganization")]
    public RecipientOrganizationExt? RecipientOrganization { get; set; }

    /// <summary>
    /// Gets or sets the configuration for delivering notifications through both email and SMS channels.
    /// </summary>
    /// <remarks>
    /// Use when you have both the recipient's email address and phone number and want to send
    /// notifications through both channels simultaneously.
    /// </remarks>
    [JsonPropertyName("recipientEmailAndSms")]
    public RecipientEmailAndSmsExt? RecipientEmailAndSms { get; set; }
}
