using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Template for an SMS notification
/// </summary>
public class SmsTemplateExt
{
    /// <summary>
    /// Gets the number from which the SMS is created by the template    
    /// </summary>
    [JsonPropertyName("senderNumber")]
    [JsonPropertyOrder(1)]
    public string SenderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets the body of SMSs created by the template    
    /// </summary>
    [JsonPropertyName("body")]
    [JsonPropertyOrder(2)]
    public string Body { get; set; } = string.Empty;
}
