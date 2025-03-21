using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Defines a container for specifying the recipient of a notification order.
/// </summary>
/// <remarks>
/// This class serves as a request wrapper that supports multiple targeting methods for notifications,
/// allowing clients to specify exactly one recipient type per request.
/// </remarks>
public class RecipientSpecificationRequestExt
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
    /// will be retrieved from the the Common Contact Register (KRR).
    /// </remarks>
    [JsonPropertyName("recipientPerson")]
    public RecipientPersonRequestExt? RecipientPerson { get; set; }

    /// <summary>
    /// Gets or sets the configuration for delivering a notification to an organization.
    /// </summary>
    /// <remarks>
    /// Use when targeting an organization by its organization number, where contact information
    /// will be retrieved from the Norwegian Central Coordinating Register for Legal Entities (Einingsregisteret).
    /// </remarks>
    [JsonPropertyName("recipientOrganization")]
    public RecipientOrganizationRequestExt? RecipientOrganization { get; set; }
}
