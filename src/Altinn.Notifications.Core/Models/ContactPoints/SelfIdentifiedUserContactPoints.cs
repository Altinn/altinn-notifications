namespace Altinn.Notifications.Core.Models.ContactPoints;

/// <summary>
/// Represents the contact point information for a self-identified user.
/// </summary>
/// <remarks>
/// Self-identified users can be used by people without a Norwegian national identifier and anyone that want to remain anonymous when using services that allows/encurage anonymous reporting.
/// </remarks>
public record SelfIdentifiedUserContactPoints
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
