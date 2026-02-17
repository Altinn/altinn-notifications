using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Represents a notification recipient identified by an external identity.
/// </summary>
/// <remarks>
/// This model supports users identified by external identity URNs, including:
/// <list type="bullet">
/// <item><description>Self-identified users (ID-porten email login)</description></item>
/// <item><description>username-users (legacy login)</description></item>
/// </list>
/// Contact information is resolved via Altinn Profile using the user's external identity.
/// </remarks>
public class RecipientExternalIdentityExt : RecipientBaseExt
{
    /// <summary>
    /// The external identity of the recipient in URN format.
    /// </summary>
    /// <value>
    /// A URN string identifying the user, for example:
    /// <list type="bullet">
    /// <item><description><c>urn:altinn:person:idporten-email:{email-address}</c> for self-identified users</description></item>
    /// <item><description><c>urn:altinn:person:legacy-selfidentified:{username}</c> for username-based users</description></item>
    /// </list>
    /// Used to identify the user in Altinn Profile for contact information retrieval.
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
    /// <item><description><see cref="NotificationChannelExt.Email"/> Email only</description></item>
    /// <item><description><see cref="NotificationChannelExt.Sms"/> SMS only</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailPreferred"/> Email first, SMS as fallback</description></item>
    /// <item><description><see cref="NotificationChannelExt.SmsPreferred"/> SMS first, email as fallback</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailAndSms"/> Both channels simultaneously</description></item>
    /// </list>
    /// </value>
    [Required]
    [JsonPropertyName("channelSchema")]
    [DefaultValue(NotificationChannelExt.Email)]
    public override required NotificationChannelExt ChannelSchema { get; set; } = NotificationChannelExt.Email;
}
