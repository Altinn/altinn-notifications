using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Core.Models.Address;

/// <summary>
/// Interface describing an address point
/// </summary>
[JsonDerivedType(typeof(EmailAddressPoint), "email")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$descriminator")]
public interface IAddressPoint
{
    /// <summary>
    /// Gets or sets the address type for the address point
    /// </summary>
    public AddressType AddressType { get; }
}