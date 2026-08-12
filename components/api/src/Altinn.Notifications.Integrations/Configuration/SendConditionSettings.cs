namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration settings for <c>SendConditionClient</c>.
/// </summary>
public class SendConditionSettings
{
    /// <summary>
    /// Gets or sets the timeout in seconds for the Maskinporten HttpClient used by <c>SendConditionClient</c>.
    /// Defaults to 30 seconds, which keeps requests well within the Azure Service Bus message lock window.
    /// </summary>
    public int MaskinportenHttpClientTimeoutSeconds { get; set; } = 30;
}
