namespace Altinn.Notifications.Email.Integrations.Configuration;

/// <summary>
/// Configuration related to the integration with Azure Communication Services.
/// </summary>
public sealed class CommunicationServicesSettings
{
    /// <summary>
    /// Connection string to the communication services service in Azure.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to use a mock email client for local development and performancetesting.
    /// </summary>
    public bool MockEmailClient { get; set; } = false;
}
