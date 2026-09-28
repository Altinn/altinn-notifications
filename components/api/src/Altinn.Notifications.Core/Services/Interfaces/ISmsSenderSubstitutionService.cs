using Altinn.Notifications.Core.Models;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Resolves an effective SMS sender identifier, substituting a numeric sender number for
/// an alphanumeric sender when required by local legislation of the recipient's country.
/// </summary>
public interface ISmsSenderSubstitutionService
{
    /// <summary>
    /// Gets a value indicating whether any substitution rules are configured.
    /// </summary>
    /// <remarks>
    /// Callers can use this to skip calling <see cref="ResolveSender"/> entirely for an
    /// entire batch when no rules are configured, avoiding any per-recipient overhead in
    /// the common case where sender substitution is not in use.
    /// </remarks>
    bool HasRules { get; }

    /// <summary>
    /// Returns the sender to use for the given recipient phone number and service owner.
    /// </summary>
    /// <param name="configuredSender">The sender configured on the notification (may be alphanumeric).</param>
    /// <param name="recipientPhoneNumber">The recipient's phone number, used to match substitution rules.</param>
    /// <param name="serviceOwnerShortName">The short name of the creator/service owner.</param>
    /// <returns>
    /// A <see cref="SmsSenderResolutionResult"/> describing the sender to use and whether
    /// a substitution was applied.
    /// </returns>
    SmsSenderResolutionResult ResolveSender(string configuredSender, string recipientPhoneNumber, string serviceOwnerShortName);
}
