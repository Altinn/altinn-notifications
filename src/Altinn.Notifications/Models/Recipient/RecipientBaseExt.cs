﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Notifications.Models.Email;
using Altinn.Notifications.Models.Sms;

namespace Altinn.Notifications.Models.Recipient;

/// <summary>
/// Represents the base class for notification recipients.
/// </summary>
public abstract class RecipientBaseExt
{
    /// <summary>
    /// Gets or sets the required channel scheme for delivering the notification.
    /// </summary>
    /// <remarks>
    /// Determines which communication channel(s) to use and their priority.
    /// Options include:
    /// <list type="bullet">
    /// <item><description><see cref="NotificationChannelExt.Email"/> - Use email only</description></item>
    /// <item><description><see cref="NotificationChannelExt.Sms"/> - Use SMS only</description></item>
    /// <item><description><see cref="NotificationChannelExt.EmailPreferred"/> - Try email first, fall back to SMS if email unavailable</description></item>
    /// <item><description><see cref="NotificationChannelExt.SmsPreferred"/> - Try SMS first, fall back to email if SMS unavailable</description></item>
    /// </list>
    /// </remarks>
    [Required]
    [JsonPropertyName("channelSchema")]
    public abstract required NotificationChannelExt ChannelSchema { get; set; }

    /// <summary>
    /// Gets or sets the email-specific configuration, used when the channel scheme includes email.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannelExt.Email"/> 
    /// or <see cref="NotificationChannelExt.EmailPreferred"/>.
    /// Contains email content, subject, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("emailSettings")]
    public EmailSendingOptionsExt? EmailSettings { get; set; }

    /// <summary>
    /// Gets or sets the SMS-specific configuration, used when the channel scheme includes SMS.
    /// </summary>
    /// <remarks>
    /// Required when <see cref="ChannelSchema"/> is set to <see cref="NotificationChannelExt.Sms"/> 
    /// or <see cref="NotificationChannelExt.SmsPreferred"/>.
    /// Contains SMS content, sender information, and delivery preferences.
    /// </remarks>
    [JsonPropertyName("smsSettings")]
    public SmsSendingOptionsExt? SmsSettings { get; set; }
}
