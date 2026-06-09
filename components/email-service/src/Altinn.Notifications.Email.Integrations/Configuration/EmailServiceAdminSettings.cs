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
}
