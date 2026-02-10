namespace Altinn.Notifications.Core.Models.ContactPoints;

/// <summary>
/// Represents the contact point information for a self-identified user.
/// </summary>
/// <remarks>
/// Self-identified users do not possess a Norwegian national identifier (F- or D-number).
/// Their identity is represented by a stable OIDC subject claim and a verified email address stored in Altinn Profile.
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
