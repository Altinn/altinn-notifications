#nullable enable

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents an SMS recipient with various properties for customization and identification.
/// </summary>
public class SmsRecipient
{
    /// <summary>
    /// Gets or sets the customized body of the SMS after replacing the keywords with actual values.
    /// </summary>
    public string? CustomizedBody { get; set; } = null;

    /// <summary>
    /// Gets or sets a value indicating whether the recipient is reserved from digital communication.
    /// </summary>
    public bool? IsReserved { get; set; }

    /// <summary>
    /// Gets or sets the recipient's mobile number.
    /// </summary>
    public string MobileNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient's national identity number.
    /// </summary>
    public string? NationalIdentityNumber { get; set; } = null;

    /// <summary>
    /// Gets or sets the recipient's organization number.
    /// </summary>
    public string? OrganizationNumber { get; set; } = null;
}
