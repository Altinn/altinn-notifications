﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models
{
    /// <summary>
    /// Represents a type that contains all the information needed to deliver either an email or SMS to a contact person identified by an organization number.
    /// </summary>
    public class OrganizationRequestSettingsExt
    {
        /// <summary>
        /// Gets or sets the organization number.
        /// </summary>
        [Required]
        [JsonPropertyName("orgNumber")]
        public required string OrgNumber { get; set; }

        /// <summary>
        /// Gets or sets the resource identifier to which the notification is related, and that recipient eligibility will be evaluated on.
        /// </summary>
        [JsonPropertyName("resourceId")]
        public string? ResourceId { get; set; }

        /// <summary>
        /// Gets or sets the communication channel scheme for the notification.
        /// </summary>
        [Required]
        [JsonPropertyName("channelScheme")]
        public required NotificationChannelExt ChannelScheme { get; set; }

        /// <summary>
        /// Gets or sets the email template settings for the notification.
        /// </summary>
        [JsonPropertyName("emailSettings")]
        public EmailRequestSettingsExt? EmailSettings { get; set; }

        /// <summary>
        /// Gets or sets the SMS template settings for the notification.
        /// </summary>
        [JsonPropertyName("smsSettings")]
        public RecipientSmsSettingsRequestExt? SmsSettings { get; set; }
    }
}
