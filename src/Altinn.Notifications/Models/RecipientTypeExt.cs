using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents a recipient container.
/// </summary>
public class RecipientTypeExt
{
    /// <summary>
    /// Gets or sets a type where the recipient is identified by a phone number.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver a text message to a specific mobile number.
    /// </value>
    [JsonPropertyName("recipientSms")]
    public RecipientSmsExt? RecipientSms { get; set; }

    /// <summary>
    /// Gets or sets a type where the recipient is identified by an email address.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver an email to a specific address.
    /// </value>
    [JsonPropertyName("recipientEmail")]
    public RecipientEmailExt? RecipientEmail { get; set; }

    /// <summary>
    /// Gets or sets a type where the recipient is identified by a national identity number.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver an SMS or E-mail to a specific person identified by a national identity number.
    /// </value>
    public RecipientPersonExt? RecipientNationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets a type where the recipient is identified by an organization number.
    /// </summary>
    /// <value>
    /// An object that contains all the information needed to deliver an SMS or E-mail to a specific person identified by an organization number.
    /// </value>
    public RecipientOrganizationExt? RecipientOrganization { get; set; }
}
