using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Response model containing a list of self-identified user contact points.
/// </summary>
public record SelfIdentifiedUserContactPointsList
{
    /// <summary>
    /// A list containing contact points for self-identified users.
    /// </summary>
    [JsonPropertyName("contactPointsList")]
    public List<SelfIdentifiedUserContactPoints> ContactPointsList { get; init; } = [];
}
