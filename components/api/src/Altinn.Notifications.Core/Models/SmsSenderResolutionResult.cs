namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Represents the result of resolving an effective SMS sender for a recipient.
/// </summary>
/// <param name="Sender">
/// The substituted numeric sender if a rule matched the phone number and had an entry
/// for the given service owner; otherwise the originally configured sender, unchanged.
/// </param>
/// <param name="WasSubstituted">A value indicating whether a substitution was applied.</param>
public sealed record SmsSenderResolutionResult(string Sender, bool WasSubstituted);
