namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a container that holds recipient-specific data for creating a notification order.
/// </summary>
public class RecipientSpecificationRequest
{
    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to send an email to a specific address.
    /// </summary>
    public RecipientEmail? RecipientEmail { get; set; }

    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to send an SMS to a specific phone number.
    /// </summary>
    public RecipientSms? RecipientSms { get; set; }

    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to send an email or SMS to a person identified by a national identity number.
    /// </summary>
    public RecipientPerson? RecipientPerson { get; set; }

    /// <summary>
    /// Gets or sets an object capturing all the information needed
    /// to deliver notifications to a contact person identified by an organization number.
    /// </summary>
    public RecipientOrganization? RecipientOrganization { get; set; }
}
