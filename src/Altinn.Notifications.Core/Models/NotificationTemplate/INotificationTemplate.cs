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
    /// Indicates whether the notification contains any recipient name placeholders.
    /// </summary>
    [JsonIgnore]
    bool HasRecipientNamePlaceholders { get; }

    /// <summary>
    /// The type of the notification template.
    /// </summary>
    NotificationTemplateType Type { get; }
}
