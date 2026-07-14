namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Configuration for email service administration and error handling.
/// </summary>
public sealed class EmailServiceAdminSettings
{
    /// <summary>
    /// The default retry delay in seconds for handling 5xx errors from Azure Communication Services.
    /// </summary>
    public int IntermittentErrorDelay { get; set; } = 60;

    /// <summary>
    /// Maximum number of attachment blobs downloaded concurrently per composed email.
    /// Controls memory and connection pressure when fetching SAS-referenced files.
    /// </summary>
    public int BlobDownloadConcurrency { get; set; } = 5;

    /// <summary>
    /// The timeout in seconds for downloading SAS-referenced blob attachments.
    /// </summary>
    public int BlobDownloadTimeoutInSeconds { get; set; } = 30;
}
