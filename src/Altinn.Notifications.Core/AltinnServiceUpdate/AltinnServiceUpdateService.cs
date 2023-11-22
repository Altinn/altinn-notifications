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
                case AltinnService.Unknown:
                    break;
            }

            return Task.CompletedTask;
        }

        private async Task HandleNotificationEmailUpdate(AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (schema)
            {
                case AltinnServiceUpdateSchema.ResourceLimitTimeout:
                    ResourceLimitUpdate? update = JsonSerializer.Deserialize<ResourceLimitUpdate>(serializedData);

                    if (update != null)
                    {
                        await Handle_ResourceLimitTimeout(update);
                    }

                    break;
            }

            await Task.CompletedTask;
        }

        private Task Handle_ResourceLimitTimeout(ResourceLimitUpdate update)
        {
            // call relevant repository method for setting the timeout flag 
        }
    }
}
