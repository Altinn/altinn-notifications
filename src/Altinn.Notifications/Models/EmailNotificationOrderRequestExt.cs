using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

using Microsoft.AspNetCore.Server.IIS.Core;

namespace Altinn.Notifications.Models;

/// <summary>
/// Class representing an email notiication order request
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class EmailNotificationOrderRequestExt
{
    /// <summary>
    /// Gets or sets the from address to use as sender of the email
    /// </summary>
    [JsonPropertyName("fromAddress")]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject of the email 
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body of the email
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type of the email
    /// </summary>
    [JsonPropertyName("content-type")]
    public EmailContentType ContentType { get; set; } = EmailContentType.Plain;

    /// <summary>
    /// Gets or sets the send time of the email. Defaults to UtcNow.        
    /// </summary>
    [JsonPropertyName("sendTime")]
    public DateTime SendTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the senders reference on the notification
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string SendersReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of recipient e-mail addresses
    /// </summary>
    [JsonPropertyName("toAddresses")]
    public List<string>? ToAddresses { get; set; }

    /// <summary>
    /// Gets or sets the list of recipients
    /// </summary>
    [JsonPropertyName("recipients")]
    public List<RecipientExt>? Recipients { get; set; }

    /// <summary>
    /// Json serialized the <see cref="EmailNotificationOrderRequestExt"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}