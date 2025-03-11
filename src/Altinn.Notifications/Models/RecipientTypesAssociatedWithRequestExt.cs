using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a container that holds recipient-specific data for creating a notification order.
/// </summary>
public class RecipientTypesAssociatedWithRequestExt
{
    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to send an SMS to a specific phone number.
    /// </summary>
    [JsonPropertyOrder(1)]
    [JsonPropertyName("recipientSms")]
    public RecipientSmsRequestExt? RecipientSms { get; set; }

    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to send an email to a specific address.
    /// </summary>
    [JsonPropertyOrder(2)]
    [JsonPropertyName("recipientEmail")]
    public RecipientEmailRequestExt? RecipientEmail { get; set; }

    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to send an email or SMS to a person identified by a national identity number.
    /// </summary>
    [JsonPropertyOrder(3)]
    [JsonPropertyName("recipientPerson")]
    public RecipientPersonRequestExt? RecipientPerson { get; set; }

    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to deliver notifications to a contact person identified by an organization number.
    /// </summary>
    [JsonPropertyOrder(4)]
    [JsonPropertyName("recipientOrganization")]
    public RecipientOrganizationRequestExt? RecipientOrganization { get; set; }
}
