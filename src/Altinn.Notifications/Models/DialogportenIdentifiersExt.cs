namespace Altinn.Notifications.Models;

/// <summary>
/// Represents unique identifiers for dialogs and transmissions within Dialogporten.
/// </summary>
public class DialogportenIdentifiersExt
{
    /// <summary>
    /// Gets or sets the identifier for a specific dialog within Dialogporten.
    /// </summary>
    public string? DialogId { get; set; }

    /// <summary>
    /// Gets or sets the identifier for a specific transmission within Dialogporten.
    /// </summary>
    public string? TransmissionId { get; set; }
}
