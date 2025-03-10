using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Represents an email template with an associated sending time policy.
/// This class extends <see cref="EmailTemplateExt"/> by adding a policy that determines when the email should be sent.
/// </summary>
public class ScheduledEmailTemplateExt : EmailTemplateExt
{
    /// <summary>
    /// Gets or sets the name of the sender.
    /// </summary>
    /// <value>
    /// If set, this value is used as the sender email display name.
    /// Only applicable if the email sender is set and configured. It cannot be used on its own.
    /// </value>
    [JsonPropertyName("emailSenderName")]
    public string? SenderName { get; set; }

    /// <summary>
    /// Gets or sets the sending time policy for when the email should be sent.
    /// </summary>
    /// <value>
    /// The sending time policy, which determines the schedule for sending the email.
    /// </value>
    [JsonPropertyName("sendingTimePolicy")]
    public SendingTimePolicyExt SendingTimePolicy { get; set; } = SendingTimePolicyExt.WorkingDaysDaytime;
}
