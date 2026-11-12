using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IServiceOwnerService"/> that keeps the registered senders in memory.
/// As a hosted service, it loads the registered senders once at startup if sender ownership is enforced for any channel.
/// A failure to load prevents the application from starting.
/// </summary>
public class ServiceOwnerService(
    IServiceOwnerRepository repository,
    IOptions<NotificationConfig> config,
    ILogger<ServiceOwnerService> logger) : IServiceOwnerService, IHostedService
{
    private readonly NotificationConfig _config = config.Value;
    private readonly ILogger<ServiceOwnerService> _logger = logger;
    private readonly IServiceOwnerRepository _repository = repository;

    private volatile Dictionary<AddressType, Dictionary<string, HashSet<string>>>? _registeredSenders;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.EnforceEmailSenderOwnership && !_config.EnforceSmsSenderOwnership)
        {
            return;
        }

        await LoadSenders(cancellationToken);
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task LoadSenders(CancellationToken cancellationToken = default)
    {
        var registeredSenders = await _repository.GetRegisteredSenders(cancellationToken);

        _registeredSenders = registeredSenders
            .GroupBy(sender => sender.AddressType)
            .ToDictionary(
                typeGroup => typeGroup.Key,
                typeGroup => typeGroup
                    .GroupBy(sender => sender.ServiceOwner, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        ownerGroup => ownerGroup.Key,
                        ownerGroup => ownerGroup.Select(sender => sender.SenderAddress).ToHashSet(StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public bool IsSenderAllowed(string serviceOwner, AddressType addressType, string? sender)
    {
        var (enforce, defaultSender) = addressType == AddressType.Email
            ? (_config.EnforceEmailSenderOwnership, _config.DefaultEmailFromAddress)
            : (_config.EnforceSmsSenderOwnership, _config.DefaultSmsSenderNumber);

        if (!enforce || string.IsNullOrWhiteSpace(sender) || string.Equals(sender, defaultSender, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var registered = _registeredSenders ?? throw new InvalidOperationException("The registered senders have not been loaded.");

        if (registered.TryGetValue(addressType, out var owners) && owners.TryGetValue(serviceOwner, out var ownerSenders) && ownerSenders.Contains(sender))
        {
            return true;
        }

        var channel = addressType == AddressType.Email ? "Email" : "SMS";
        _logger.LogWarning("Rejected an {Channel} sender that is not registered for the service owner.", channel);

        return false;
    }
}
