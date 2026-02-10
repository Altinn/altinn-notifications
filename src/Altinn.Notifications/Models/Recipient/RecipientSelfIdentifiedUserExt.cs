using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Represents a notification recipient who authenticates via ID-porten email login (self-identified user).
/// </summary>
public class RecipientSelfIdentifiedUserExt : RecipientBaseExt
{
    /// <summary>
    /// The external identity of the recipient in URN format.
    /// </summary>
    /// <value>
    /// A URN string in the format <c>urn:altinn:person:idporten-email:{email-address}</c>,
    /// used to identify the user in Altinn Profile for contact information retrieval.
    /// </value>
    [Required]
    [JsonPropertyName("externalIdentity")]
    public required string ExternalIdentity { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier for authorization and auditing purposes.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    /// <summary>
    /// The channel scheme for delivering the notification.
    /// </summary>
    /// <value>
    /// One of the available <see cref="NotificationChannelExt"/> values determining the communication channel(s) and priority:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannelExt.Email"/> — Email only</description></item>
    /// <item><description><see cref="NotificationChannelExt.Sms"/> — SMS only</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailPreferred"/> — Email first, SMS as fallback</description></item>
    /// <item><description><see cref="NotificationChannelExt.SmsPreferred"/> — SMS first, email as fallback</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailAndSms"/> — Both channels simultaneously</description></item>
    /// </list>
    /// </value>
    [Required]
    [JsonPropertyName("channelSchema")]
    [DefaultValue(NotificationChannelExt.Email)]
    public override required NotificationChannelExt ChannelSchema { get; set; } = NotificationChannelExt.Email;
}
