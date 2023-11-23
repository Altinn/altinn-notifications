using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Interface describing the service responding to service updates from the Notifications Email component
    /// </summary>
    public interface INotificationsEmailServiceUpdateService
    {
        /// <summary>
        /// Method for handling an incoming service update
        /// </summary>
        public Task HandleServiceUpdate(AltinnServiceUpdateSchema schema, string serializedData);
    }
}
