using System.Text.Json;

using Altinn.Notifications.Core.Models.Address;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class representing a notification recipient.
/// </summary>
public class Recipient
{
    /// <summary>
    /// Gets or sets the list of address points for the recipient.
    /// </summary>
    public List<IAddressPoint> AddressInfo { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the recipient is reserved from digital communication.
    /// </summary>
    public bool? IsReserved { get; set; }

    /// <summary>
    /// Gets or sets the recipient's national identity number.
    /// </summary>
    public string? NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets the recipient's organization number.
    /// </summary>
    public string? OrganizationNumber { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Recipient"/> class.
    /// </summary>
    public Recipient()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Recipient"/> class.
    /// </summary>
    /// <param name="addressInfo">The list of address points for the recipient.</param>
    /// <param name="organizationNumber">The recipient's organization number.</param>
    /// <param name="nationalIdentityNumber">The recipient's national identity number.</param>
    public Recipient(List<IAddressPoint> addressInfo, string? organizationNumber = null, string? nationalIdentityNumber = null)
    {
        AddressInfo = addressInfo;
        OrganizationNumber = organizationNumber;
        NationalIdentityNumber = nationalIdentityNumber;
    }

    /// <summary>
    /// Creates a deep copy of the recipient object.
    /// </summary>
    /// <returns>A deep copy of the recipient object.</returns>
    internal Recipient DeepCopy()
    {
        string json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<Recipient>(json)!;
    }
}
