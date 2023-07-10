using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Address;

/// <summary>
/// Interface describing an address point
/// </summary>
public interface IAddressPoint
{
    /// <summary>
    /// Gets or sets the address type for the address point
    /// </summary>
    public AddressType AddressType { get; }
}