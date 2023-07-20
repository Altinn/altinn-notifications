namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class describing an email envelope
/// </summary>
public class EmailEnvelope
{
    /// <summary>
    /// The recipient id, org number or person number
    /// </summary>
    public string RecipientId { get; set; }

    /// <summary>
    /// The email
    /// </summary>
    public Email Email { get; set; }
}