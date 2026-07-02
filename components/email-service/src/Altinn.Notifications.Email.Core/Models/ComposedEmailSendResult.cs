namespace Altinn.Notifications.Email.Core.Models;

/// <summary>
/// Represents the successful outcome of submitting a composed email to Azure Communication Services.
/// Carries the ACS operation identifier and the total encoded attachment size for downstream status tracking.
/// </summary>
public class ComposedEmailSendResult
{
    /// <summary>
    /// The ACS send operation identifier used to poll for delivery status.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>
    /// Total base64-encoded attachment size in bytes.
    /// </summary>
    public long EncodedAttachmentsSize { get; init; }
}
