namespace Altinn.Notifications.Models;

/// <summary>
/// Represents identifiers for one or more dialogs and/or transmissions in the Dialogporten.
/// </summary>
public class DialogportenAssociationExt
{
    /// <summary>
    /// Gets or sets the identifier which points to a corresponding dialog in Dialogporten.
    /// </summary>
    /// <value>
    /// The identifier that identifies a specific dialog within Dialogporten.
    /// </value>
    public string? DialogId { get; set; }

    /// <summary>
    /// Gets or sets the identifier which points to a corresponding dialog in Dialogporten.
    /// </summary>
    /// <value>
    /// The identifier that identifies a specific transmission within Dialogporten.
    /// </value>
    public string? TransmissionId { get; set; }
}
