using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a container that holds recipient information associated with a request to create a notification order.
/// </summary>
public class RecipientTypesAssociatedWithRequestExt
{
    /// <summary>
    /// Gets or sets a type where the recipient is identified by a phone number.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver a text message to a specific mobile number.
    /// </value>
    [JsonPropertyName("recipientSms")]
    public RecipientSmsRequestExt? RecipientSms { get; set; }

    /// <summary>
    /// Gets or sets a type where the recipient is identified by an email address.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver an email to a specific address.
    /// </value>
    [JsonPropertyName("recipientEmail")]
    public RecipientEmailRequestExt? RecipientEmail { get; set; }

    /// <summary>
    /// Gets or sets a type where the recipient is identified by a national identity number.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver an SMS or E-mail to a specific person identified by a national identity number.
    /// </value>
    [JsonPropertyName("recipientPerson")]
    public RecipientPersonRequestExt? RecipientPerson { get; set; }

    /// <summary>
    /// Gets or sets a type where the recipient is identified by an organization number.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver an SMS or E-mail to a specific person identified by an organization number.
    /// </value>
    [JsonPropertyName("recipientOrganization")]
    public RecipientOrganizationRequestExt? RecipientOrganization { get; set; }
}
