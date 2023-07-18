using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.NotificationTemplate;

/// <summary>
/// Template for an email notification
/// </summary>
public class EmailTemplateExt
{
    /// <summary>
    /// Gets the from adress of emails created by the template    
    /// </summary>
    [JsonPropertyName("fromAddress")]
    public string? FromAddress { get; set; }

    /// <summary>
    /// Gets the subject of emails created by the template    
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// Gets the body of emails created by the template    
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>
    /// Gets the content type of emails created by the template
    /// </summary>
    [JsonPropertyName("content-type")]
    public EmailContentType? ContentType { get; set; }
}