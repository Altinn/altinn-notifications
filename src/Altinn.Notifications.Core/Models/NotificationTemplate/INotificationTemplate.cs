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
    /// Gets the type of the notification template.
    /// </summary>
    NotificationTemplateType Type { get; }
}
