namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents identifiers for dialogs and transmissions in the Dialogporten API.
/// </summary>
public class DialogportenReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialogportenReference"/> class.
    /// </summary>
    /// <param name="dialogId">The identifier for a specific dialog within Dialogporten.</param>
    /// <param name="transmissionId">The identifier for a specific transmission within Dialogporten.</param>
    public DialogportenReference(string? dialogId = null, string? transmissionId = null)
    {
        DialogId = dialogId;
        TransmissionId = transmissionId;
    }

    /// <summary>
    /// Gets the identifier for a specific dialog within Dialogporten.
    /// </summary>
    public string? DialogId { get; }

    /// <summary>
    /// Gets the identifier for a specific transmission within Dialogporten.
    /// </summary>
    public string? TransmissionId { get; }
}
