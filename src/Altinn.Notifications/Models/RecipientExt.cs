using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.Address;

namespace Altinn.Notifications.Models;

/// <summary>
/// Class representing a notification recipient
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class RecipientExt
{
    /// <summary>
    /// Gets or sets the recipient id
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets a list of address points for the recipient
    /// </summary>
    [JsonPropertyName("addressList")]
    public List<IAddressPoint> AddressList { get; set; } = new List<IAddressPoint>();
}