using System.Text.Json;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class representing an sms
/// </summary>
public class Sms
{
    /// <summary>
    /// Gets or sets the id of the sms.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Gets or sets the sender of the sms message
    /// </summary>
    /// <remarks>
    /// Can be a literal string or a phone number
    /// </remarks>
    public string Sender { get; set; }

    /// <summary>
    /// Gets or sets the recipient of the sms message
    /// </summary>
    public string Recipient { get; set; }

    /// <summary>
    /// Gets or sets the contents of the sms message
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the national identity number.
    /// </summary>
    /// <value>
    /// The national identity number.
    /// </value>
    public string NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets the organization number.
    /// </summary>
    /// <value>
    /// The organization number.
    /// </value>
    public string OrganizationNumber { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sms"/> class.
    /// </summary>
    public Sms(Guid notificationId, string sender, string recipient, string message, string nationalIdentityNumber, string organizationNumber)
    {
        NotificationId = notificationId;
        Recipient = recipient;
        Sender = sender;
        Message = message;
        NationalIdentityNumber = nationalIdentityNumber;
        OrganizationNumber = organizationNumber;
    }

    /// <summary>
    /// Json serializes the <see cref="Sms"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptionsProvider.Options);
    }
}
