using System.Text.Json;

using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Models.AltinnServiceUpdate;
using Altinn.Notifications.Core.Repository.Interfaces;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of the <see cref="INotificationsEmailServiceUpdateService"/> interface
    /// </summary>
    public class NotificationsEmailServiceUpdateService : INotificationsEmailServiceUpdateService
    {
        private readonly IResourceLimitRepository _resourceLimitRepository;
        private readonly ILogger<INotificationsEmailServiceUpdateService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationsEmailServiceUpdateService"/> class.
        /// </summary>
        public NotificationsEmailServiceUpdateService(
            IResourceLimitRepository resourceLimitRepository,
            ILogger<INotificationsEmailServiceUpdateService> logger)
        {
            _resourceLimitRepository = resourceLimitRepository;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task HandleServiceUpdate(AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (schema)
            {
                case AltinnServiceUpdateSchema.ResourceLimitExceeded:
                    bool success = ResourceLimitExceeded.Tryparse(serializedData, out ResourceLimitExceeded update);

                    if (!success)
                    {
                        _logger.LogError("// NotificationsEmailServiceUpdateService // HandleServiceUpdate // Failed to parse message {message} into schema {schema}", serializedData, schema);
                        return;
                    }

                    await HandleResourceLimitExceeded(update);
                    return;
            }
        }

        private async Task HandleResourceLimitExceeded(ResourceLimitExceeded update)
        {
            await _resourceLimitRepository.SetEmailTimeout(update.ResetTime);
        }
    }
}
