using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents a model containing all the information needed to deliver either
/// an email or SMS to a specific person identified by their national identity number.
/// </summary>
public class RecipientPerson
{
    /// <summary>
    /// Gets or sets the national identity number of the recipient.
    /// </summary>
    /// <remarks>
    /// Used to identify the person in the Common Contact Register (KRR) to retrieve their registered contact information.
    /// </remarks>
    public required string NationalIdentityNumber { get; set; }

    /// <summary>
    /// Gets or sets an optional resource identifier for authorization and auditing purposes.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Gets or sets the required channel scheme for delivering the notification.
    /// </summary>
    /// <remarks>
    /// Determines which communication channel(s) to use and their priority.
    /// Options include:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannel.Email"/> - Use email only</description></item>
    /// <item><description><see cref="NotificationChannel.Sms"/> - Use SMS only</description></item>
    /// <item><description><see cref="NotificationChannel.EmailPreferred"/> - Try email first, fall back to SMS if email unavailable</description></item>
    /// <item><description><see cref="NotificationChannel.SmsPreferred"/> - Try SMS first, fall back to email if SMS unavailable</description></item>
    /// <item><description><see cref="NotificationChannel.EmailAndSms"/> - Use both email and SMS</description></item>
    /// </list>
    /// </remarks>
    public required NotificationChannel ChannelSchema { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to bypass the recipient's reservation against electronic communication.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, notifications will be sent even if the recipient has registered a reservation
    /// against electronic communication in the Common Contact Register (KRR).
    /// Defaults to <c>false</c>.
    /// </remarks>
    public bool IgnoreReservation { get; set; } = false;

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannel.Email"/>, 
    /// <see cref="NotificationChannel.EmailPreferred"/>, or <see cref="NotificationChannel.EmailAndSms"/>.
    /// Contains email content, subject, sender information, and delivery preferences.
    /// </remarks>
    public EmailSendingOptions? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannel.Sms"/>, 
    /// <see cref="NotificationChannel.SmsPreferred"/>, or <see cref="NotificationChannel.EmailAndSms"/>.
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    public SmsSendingOptions? SmsSettings { get; set; }
}
