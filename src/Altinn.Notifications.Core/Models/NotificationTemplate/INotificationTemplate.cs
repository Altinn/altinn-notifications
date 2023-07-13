using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.Address;

namespace Altinn.Notifications.Core.Models.NotificationTemplate;

/// <summary>
/// Base class for a notification template
/// </summary>
[JsonDerivedType(typeof(EmailTemplate), "email")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$descriminator")]
public interface INotificationTemplate
{
    /// <summary>
    /// Gets or sets the type for the template
    /// </summary>
    public NotificationTemplateType Type { get; }
}