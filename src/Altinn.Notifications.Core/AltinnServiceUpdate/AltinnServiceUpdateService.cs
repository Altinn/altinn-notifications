using System.Text.Json;

using Altinn.Notifications.Core.AltinnServiceUpdate;

namespace Altinn.Notifications.Core.ServiceUpdate
{
    /// <summary>
    /// Implementation of the <see cref="IAltinnServiceUpdateService"/> interface
    /// </summary>
    public class AltinnServiceUpdateService : IAltinnServiceUpdateService
    {
        /// <inheritdoc/>
        public Task HandleServiceUpdate(AltinnService service, AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (service)
            {
                case AltinnService.Notifications_Email:
                    return HandleNotificationEmailUpdate(schema, serializedData);
            }

            return Task.CompletedTask;
        }

        private Task HandleNotificationEmailUpdate(AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (schema)
            {
                case AltinnServiceUpdateSchema.ResourceLimitTimeout:
                    ResourceLimitUpdate? update = JsonSerializer.Deserialize<ResourceLimitUpdate>(serializedData);

                    if (update != null)
                    {
                        return Handle_ResourceLimitTimeout(update);
                    };
            }

            return Task.CompletedTask;
        }

        private Task Handle_ResourceLimitTimeout(ResourceLimitUpdate update)
        {
            // call relevant repository method for setting the timeout flag 
        }
    }
}
