namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Class representing an email recipient
/// </summary>
public class EmailRecipient
{
    /// <summary>
    /// Gets or sets the recipient's organisation number
    /// </summary>
    public string? OrganisationNumber { get; set; } = null;

    /// <summary>
    /// Gets or sets the recipient's national identity number
    /// </summary>
    public string? NationalIdentityNumber { get; set; } = null;

    /// <summary>
    /// Gets or sets the toaddress
    /// </summary>
    public string ToAddress { get; set; } = string.Empty;
}
