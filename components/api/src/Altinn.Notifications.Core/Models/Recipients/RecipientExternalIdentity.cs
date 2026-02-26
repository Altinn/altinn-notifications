using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

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
public record RecipientExternalIdentity
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
    public required string ExternalIdentity { get; init; }

    /// <summary>
    /// The channel scheme for delivering the notification.
    /// </summary>
    /// <value>
    /// One of the available <see cref="NotificationChannel"/> values determining the communication channel(s) and priority:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannel.Email"/> Email only</description></item>
    /// <item><description><see cref="NotificationChannel.Sms"/> SMS only</description></item>
    /// <item><description><see cref="NotificationChannel.EmailPreferred"/> Email first, SMS as fallback</description></item>
    /// <item><description><see cref="NotificationChannel.SmsPreferred"/> SMS first, email as fallback</description></item>
    /// <item><description><see cref="NotificationChannel.EmailAndSms"/> Both channels simultaneously</description></item>
    /// </list>
    /// </value>
    public required NotificationChannel ChannelSchema { get; init; }

    /// <summary>
    /// An optional resource identifier for authorization and auditing purposes.
    /// </summary>
    /// <value>
    /// A resource URN or identifier, or <c>null</c> if not specified.
    /// </value>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Email-specific configuration for the notification.
    /// </summary>
    /// <value>
    /// An <see cref="EmailSendingOptions"/> object containing email content, subject, sender information,
    /// and delivery preferences. Required when <see cref="ChannelSchema"/> includes email delivery.
    /// </value>
    public EmailSendingOptions? EmailSettings { get; init; }

    /// <summary>
    /// SMS-specific configuration for the notification.
    /// </summary>
    /// <value>
    /// A <see cref="SmsSendingOptions"/> object containing SMS content, sender information,
    /// and delivery preferences. Required when <see cref="ChannelSchema"/> includes SMS delivery.
    /// </value>
    public SmsSendingOptions? SmsSettings { get; init; }
}
