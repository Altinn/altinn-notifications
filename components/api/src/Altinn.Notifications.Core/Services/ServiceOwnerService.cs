using Altinn.Notifications.Core.Enums;
using Altinn.Notifications.Core.Persistence;
using Altinn.Notifications.Core.Services.Interfaces;

namespace Altinn.Notifications.Core.Services;

/// <summary>
/// Implementation of the <see cref="IServiceOwnerService"/> that keeps the registered senders in memory.
/// </summary>
public class ServiceOwnerService(IServiceOwnerRepository repository) : IServiceOwnerService
{
    private readonly IServiceOwnerRepository _repository = repository;

    private volatile Dictionary<(SenderAddressType Type, string Owner), HashSet<string>>? _registeredSenders;

    /// <inheritdoc/>
    public async Task LoadRegisteredSenders(CancellationToken cancellationToken = default)
    {
        var registeredSenders = await _repository.GetRegisteredSenders(cancellationToken);

        var refreshedSenders = new Dictionary<(SenderAddressType, string), HashSet<string>>();
        foreach (var registeredSender in registeredSenders)
        {
            var key = (registeredSender.AddressType, registeredSender.ServiceOwner);
            if (!refreshedSenders.TryGetValue(key, out var set))
            {
                set = [with(StringComparer.Ordinal)];
                refreshedSenders[key] = set;
            }

            set.Add(registeredSender.SenderAddress);
        }

        _registeredSenders = refreshedSenders;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetUnregisteredSenders(string serviceOwner, SenderAddressType addressType, IEnumerable<string> senders)
    {
        var registered = _registeredSenders ?? throw new InvalidOperationException("The registered senders have not been loaded.");

        registered.TryGetValue((addressType, serviceOwner), out var ownerSenders);

        return [.. senders.Distinct(StringComparer.Ordinal).Where(s => ownerSenders == null || !ownerSenders.Contains(s))];
    }
}
