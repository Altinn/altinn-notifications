namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Class representing an email recipient.
/// </summary>
public class EmailRecipient
{
    /// <summary>
    /// Gets or sets the customized body of the email.
    /// </summary>
    public string? CustomizedBody { get; set; } = null;

    /// <summary>
    /// Gets or sets the customized subject of the email.
    /// </summary>
    public string? CustomizedSubject { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether the recipient is reserved from digital communication.
    /// </summary>
    public bool? IsReserved { get; set; }

    /// <summary>
    /// Gets or sets the recipient's national identity number.
    /// </summary>
    public string? NationalIdentityNumber { get; set; } = null;

    /// <summary>
    /// Gets or sets the recipient's organization number.
    /// </summary>
    public string? OrganizationNumber { get; set; } = null;

    /// <summary>
    /// Gets or sets the email address of the recipient.
    /// </summary>
    public string ToAddress { get; set; } = string.Empty;
}
