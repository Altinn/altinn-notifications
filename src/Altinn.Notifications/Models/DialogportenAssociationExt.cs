namespace Altinn.Notifications.Models;

/// <summary>
/// Represents identifiers for dialogs and transmissions in the Dialogporten.
/// </summary>
public class DialogportenAssociationExt
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
