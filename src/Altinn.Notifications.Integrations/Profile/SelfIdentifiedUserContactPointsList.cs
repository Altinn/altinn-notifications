using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Models.ContactPoints;

namespace Altinn.Notifications.Integrations.Profile;

/// <summary>
/// Response model containing a list of self-identified user contact points.
/// </summary>
public class SelfIdentifiedUserContactPointsList
{
    /// <summary>
    /// Gets or sets a list of contact points for self-identified users.
    /// </summary>
    [JsonPropertyName("contactPoints")]
    public List<SelfIdentifiedUserContactPoints> ContactPointsList { get; set; } = [];
}
