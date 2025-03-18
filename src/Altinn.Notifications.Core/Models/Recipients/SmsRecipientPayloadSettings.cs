using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Recipients;

/// <summary>
/// Represents configuration settings that are associated with the request model for sending an SMS to a specific recipient.
/// </summary>
public class SmsRecipientPayloadSettings
{
    /// <summary>
    /// Gets or sets either the phone number or the name used as the sender in the SMS message.
    /// </summary>
    public required string Sender { get; set; }

    /// <summary>
    /// Gets or sets the text body of the SMS message.
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Gets or sets the customized body of the SMS after replacing the keywords with actual values.
    /// </summary>
    public string? CustomizedBody { get; set; } = null;

    /// <summary>
    /// Gets or sets the sending time policy, indicating when the SMS should be dispatched.
    /// Defaults to <see cref="SendingTimePolicy.Daytime"/>.
    /// </summary>
    public SendingTimePolicy SendingTimePolicy { get; set; } = SendingTimePolicy.Daytime;
}
