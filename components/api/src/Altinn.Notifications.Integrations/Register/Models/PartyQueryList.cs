using System.Text.Json.Serialization;

namespace Altinn.Notifications.Integrations.Register.Models;

/// <summary>
/// The list envelope used by the register notifications query endpoint.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
internal sealed record PartyQueryList<T>
{
    /// <summary>
    /// Gets the items in the list.
    /// </summary>
    [JsonPropertyName("data")]
    public required List<T> Data { get; init; }
}
