using System.ComponentModel.DataAnnotations;

namespace Altinn.Notifications.Integrations.Configuration;

/// <summary>
/// Configuration settings for <c>SendConditionClient</c>.
/// </summary>
public class SendConditionSettings
{
    /// <summary>
    /// Gets or sets the timeout in seconds for the Maskinporten HttpClient used by <c>SendConditionClient</c>.
    /// Defaults to 30 seconds, which keeps requests well within the Azure Service Bus message lock window.
    /// Must be between 30 and 300 seconds to ensure reliable operation.
    /// </summary>
    [Range(30, 300, ErrorMessage = "MaskinportenHttpClientTimeoutSeconds must be between 30 and 300.")]
    public int MaskinportenHttpClientTimeoutSeconds { get; set; } = 30;
}
