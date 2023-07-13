using System.Text.Json;
using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.NotificationTemplate;

namespace Altinn.Notifications.Models;

/// <summary>
/// A class representing a registered notification order. 
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class NotificationOrderExt
{
    /// <summary>
    /// Gets or sets the id of the notification order
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the short name of the creator of the notification order
    /// </summary>
    [JsonPropertyName("creator")]
    public string Creator { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the senders reference of the notification
    /// </summary>
    [JsonPropertyName("sendersReference")]
    public string? SendersReference { get; set; }

    /// <summary>
    /// Gets or sets the requested send time of the notification
    /// </summary>
    [JsonPropertyName("sendTime")]
    public DateTime SendTime { get; set; }

    /// <summary>
    /// Gets or sets the date and time of when the notification order was created
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the preferred notification channel of the notification order
    /// </summary>
    [JsonPropertyName("notificationChannel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationChannel NotificationChannel { get; set; }

    /// <summary>
    /// Gets or sets the list of recipients
    /// </summary>
    [JsonPropertyName("recipients")]
    public List<RecipientExt> Recipients { get; set; } = new List<RecipientExt>();

    /// <summary>
    /// Gets or sets the emailTemplate
    /// </summary>
    [JsonPropertyName("emailTemplate")]
    public EmailTemplateExt? EmailTemplate { get; set; }

    /// <summary>
    /// Json serialized the <see cref="NotificationOrderExt"/>
    /// </summary>
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            Converters = { new JsonStringEnumConverter() },
        });
    }
}