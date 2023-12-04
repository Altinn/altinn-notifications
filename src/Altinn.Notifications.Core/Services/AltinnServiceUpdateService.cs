using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.Services
{
    /// <summary>
    /// Implementation of the <see cref="IAltinnServiceUpdateService"/> interface
    /// </summary>
    public class AltinnServiceUpdateService : IAltinnServiceUpdateService
    {
        private readonly INotificationsEmailServiceUpdateService _notificationsEmail;
        private readonly ILogger<AltinnServiceUpdateService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AltinnServiceUpdateService"/> class.
        /// </summary>
        public AltinnServiceUpdateService(
            INotificationsEmailServiceUpdateService notificationsEmail,
            ILogger<IAltinnServiceUpdateService> logger)
        {
            _notificationsEmail = notificationsEmail;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task HandleServiceUpdate(string source, AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (source)
            {
                case "platform-notifications-email":
                    await _notificationsEmail.HandleServiceUpdate(schema, serializedData);
                    return;
                default:
                    _logger.LogInformation("// AltinnServiceUpdateService // HandleServiceUpdate// Received update from unknown service {service}.", source);
                    return;
            }
        }
    }
}
