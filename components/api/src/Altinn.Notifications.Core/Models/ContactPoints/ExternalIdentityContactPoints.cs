namespace Altinn.Notifications.Core.Models.ContactPoints;

/// <summary>
/// Represents the contact point information for a user identified by an external identity.
/// </summary>
/// <remarks>
/// External identity users include:
/// <list type="bullet">
/// <item><description>Self-identified users - people without a Norwegian national identifier who authenticate via ID-porten email login</description></item>
/// <item><description>Username-based users - users who authenticate using a username with external identity providers</description></item>
/// </list>
/// Both user types can be used by people who want to remain anonymous when using services that allow/encourage anonymous reporting.
/// </remarks>
public record ExternalIdentityContactPoints
{
    /// <summary>
    /// The verified email address associated with the user.
    /// </summary>
    public required string Email { get; init; } = string.Empty;

    /// <summary>
    /// The external identity of the user in URN format.
    /// </summary>
    public required string ExternalIdentity { get; init; } = string.Empty;

    /// <summary>
    /// The verified mobile phone number associated with the user.
    /// </summary>
    public required string MobileNumber { get; init; } = string.Empty;
}
