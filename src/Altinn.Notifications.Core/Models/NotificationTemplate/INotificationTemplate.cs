using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.NotificationTemplate;

/// <summary>
/// Represents a base notification template.
/// </summary>
[JsonDerivedType(typeof(EmailTemplate), "email")]
[JsonDerivedType(typeof(SmsTemplate), "sms")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$")]
public interface INotificationTemplate
{
    /// <summary>
    /// Gets a value indicating whether the email body or subject contains any recipient name placeholders.
    /// </summary>
    [JsonIgnore]
    bool HasRecipientNamePlaceholders { get; }

    /// <summary>
    /// Gets a value indicating whether the email body contains any recipient number placeholders.
    /// </summary>
    [JsonIgnore]
    bool HasRecipientNumberPlaceholders { get; }

    /// <summary>
    /// Gets the type of the notification template.
    /// </summary>
    NotificationTemplateType Type { get; }
}
