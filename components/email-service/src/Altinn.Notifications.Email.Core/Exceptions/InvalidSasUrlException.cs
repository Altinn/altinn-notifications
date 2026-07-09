namespace Altinn.Notifications.Email.Core.Exceptions;

/// <summary>
/// Thrown when a SAS URL is invalid, expired, or returns a non-successful HTTP response.
/// This is a permanent failure — the message should not be retried.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="InvalidSasUrlException"/> with a safe message that does not include the URL.
/// </remarks>
/// <param name="filename">The filename of the attachment that failed to download.</param>
/// <param name="httpStatus">The HTTP status code returned by the blob storage endpoint.</param>
public class InvalidSasUrlException(string filename, int httpStatus) : Exception($"SAS URL for attachment '{filename}' returned HTTP {httpStatus}. The URL may be expired or invalid.")
{
}
