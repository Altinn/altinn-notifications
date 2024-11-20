using System.Text.Json.Serialization;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents the components of a recipient's name.
/// </summary>
public class RecipientNameComponents
{
    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Gets the full name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the last name (surname).
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Gets the middle name.
    /// </summary>
    public string? MiddleName { get; init; }
}
