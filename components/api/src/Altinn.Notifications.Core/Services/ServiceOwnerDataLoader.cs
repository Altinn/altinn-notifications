using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Loads the sender addresses of the <see cref="IServiceOwnerService"/> once at startup, if sender ownership is enforced for any channel.
/// A failure to load prevents the application from starting.
/// </summary>
public class ServiceOwnerDataLoader(IServiceOwnerService serviceOwnerService, IOptions<NotificationConfig> config) : IHostedService
{
    private readonly NotificationConfig _config = config.Value;
    private readonly IServiceOwnerService _serviceOwnerService = serviceOwnerService;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_config.EnforceEmailSenderOwnership || _config.EnforceSmsSenderOwnership)
        {
            await _serviceOwnerService.LoadSenders(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
