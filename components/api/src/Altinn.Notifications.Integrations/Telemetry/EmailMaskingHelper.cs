using System.Linq;

namespace Altinn.Notifications.Integrations.Telemetry;

/// <summary>
/// Provides utility methods for masking personally identifiable information in email addresses
/// before they are emitted as OpenTelemetry metric dimensions.
/// </summary>
internal static class EmailMaskingHelper
{
    /// <summary>
    /// Masks an email address by keeping only the first two characters of the local part
    /// and the full domain, replacing the remainder with asterisks.
    /// Returns an empty string if the input is null, whitespace, or in an invalid format.
    /// </summary>
    /// <param name="email">The email address to mask.</param>
    /// <returns>The masked email address, or an empty string if the input is invalid.</returns>
    internal static string MaskEmailAddress(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1)
        {
            return string.Empty; // invalid format -> do not emit raw value
        }

        var localPart = email[..atIndex];
        var domain = email[(atIndex + 1)..];

        if (localPart.Length <= 2)
        {
            return $"***@{domain}";
        }

        return $"{localPart[..2]}***@{domain}";
    }

    /// <summary>
    /// Redacts known email addresses from a free-form status message string,
    /// replacing each occurrence with the masked version of the email address.
    /// Returns an empty string if <paramref name="message"/> is null or whitespace.
    /// </summary>
    /// <param name="message">The status message that may contain email addresses.</param>
    /// <param name="knownAddresses">The email addresses to redact from the message.</param>
    /// <returns>The message with known addresses redacted.</returns>
    internal static string RedactEmailAddressesFromMessage(string? message, params string[] knownAddresses)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var redacted = message;
        foreach (var address in (knownAddresses ?? []).Where(address => !string.IsNullOrWhiteSpace(address)))
        {
            redacted = redacted.Replace(address, MaskEmailAddress(address), StringComparison.OrdinalIgnoreCase);
        }

        return redacted;
    }
}
