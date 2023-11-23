using System.Text.Json;

using Altinn.Notifications.Core.Repository.Interfaces;

namespace Altinn.Notifications.Core.AltinnServiceUpdate
{
    /// <summary>
    /// Implementation of the <see cref="INotificationsEmailServiceUpdateService"/> interface
    /// </summary>
    public class NotificationsEmailServiceUpdateService : INotificationsEmailServiceUpdateService
    {
        private readonly IResourceLimitRepository _resourceLimitRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationsEmailServiceUpdateService"/> class.
        /// </summary>
        public NotificationsEmailServiceUpdateService(IResourceLimitRepository resourceLimitRepository)
        {
            _resourceLimitRepository = resourceLimitRepository;
        }

        /// <inheritdoc/>
        public async Task HandleServiceUpdate(AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (schema)
            {
                case AltinnServiceUpdateSchema.ResourceLimitExceeded:
                    ResourceLimitExceeded? update = JsonSerializer.Deserialize<ResourceLimitExceeded>(serializedData);

                    if (update != null)
                    {
                        await HandleResourceLimitExceeded(update);
                    }

                    return;
            }
        }

        private async Task HandleResourceLimitExceeded(ResourceLimitExceeded update)
        {
            await _resourceLimitRepository.SetEmailTimeout(DateTime.UtcNow.AddSeconds(update.Timeout));
        }
    }
}
