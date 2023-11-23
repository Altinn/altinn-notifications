using Altinn.Notifications.Core.AltinnServiceUpdate;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.Core.ServiceUpdate
{
    /// <summary>
    /// Implementation of the <see cref="IAltinnServiceUpdateService"/> interface
    /// </summary>
    public class AltinnServiceUpdateService : IAltinnServiceUpdateService
    {
        private readonly INotificationsEmailServiceUpdateService _notificationsEmail;
        private readonly ILogger<IAltinnServiceUpdateService> _logger;

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
        public async Task HandleServiceUpdate(AltinnService service, AltinnServiceUpdateSchema schema, string serializedData)
        {
            switch (service)
            {
                case AltinnService.Notifications_Email:
                    await _notificationsEmail.HandleServiceUpdate(schema, serializedData);
                    return;
                case AltinnService.Unknown:
                    _logger.LogInformation("// AltinnServiceUpdateService // HandleServiceUpdate// Received service from unknown service {service}.", service);
                    return;
            }
        }
    }
}
