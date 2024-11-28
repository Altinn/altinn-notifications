namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents the components of a person's name.
/// </summary>
public record PersonNameComponents
{
    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Gets the middle name.
    /// </summary>
    public string? MiddleName { get; init; }

    /// <summary>
    /// Gets the surname.
    /// </summary>
    public string? LastName { get; init; }
}
