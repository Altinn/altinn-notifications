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
}
