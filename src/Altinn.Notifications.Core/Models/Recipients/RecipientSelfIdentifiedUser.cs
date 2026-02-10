using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a notification recipient who authenticates via ID-porten email login (self-identified user).
/// </summary>
public record RecipientSelfIdentifiedUser
{
    /// <summary>
    /// The external identity of the recipient in URN format.
    /// </summary>
    /// <value>
    /// A URN string in the format <c>urn:altinn:person:idporten-email:{email-address}</c>.
    /// </value>
    public required string ExternalIdentity { get; init; }

    /// <summary>
    /// The channel scheme for delivering the notification.
    /// </summary>
    /// <value>
    /// One of the available <see cref="NotificationChannel"/> values determining the communication channel(s) and priority:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannel.Email"/> — Email only</description></item>
    /// <item><description><see cref="NotificationChannel.Sms"/> — SMS only</description></item>
    /// <item><description><see cref="NotificationChannel.EmailPreferred"/> — Email first, SMS as fallback</description></item>
    /// <item><description><see cref="NotificationChannel.SmsPreferred"/> — SMS first, email as fallback</description></item>
    /// <item><description><see cref="NotificationChannel.EmailAndSms"/> — Both channels simultaneously</description></item>
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
