using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.NotificationTemplate
{
    /// <summary>
    /// Base class for a notification template
    /// </summary>
    public interface INotificationTemplate
    {
        /// <summary>
        /// Gets or sets the type for the template
        /// </summary>
        public NotificationTemplateType Type { get; }
    }
}