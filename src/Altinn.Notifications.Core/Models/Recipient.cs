using Altinn.Notifications.Core.Models.Address;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class representing a notification recipient
/// </summary>
public class Recipient
{
    /// <summary>
    /// Gets the recipient's organisation number
    /// </summary>
    public string? OrganisationNumber { get; set; } = null;

    /// <summary>
    /// Gets the recipient's national identity number
    /// </summary>
    public string? NationalIdentityNumber { get; set; } = null;

    /// <summary>
    /// Gets a list of address points for the recipient
    /// </summary>
    public List<IAddressPoint> AddressInfo { get; set; } = new List<IAddressPoint>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Recipient"/> class.
    /// </summary>
    public Recipient(List<IAddressPoint> addressInfo, string? organisationNumber = null, string? nationalIdentityNumber = null)
    {
        OrganisationNumber = organisationNumber;
        NationalIdentityNumber = nationalIdentityNumber;
        AddressInfo = addressInfo;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Recipient"/> class.
    /// </summary>
    public Recipient()
    {
    }
}
