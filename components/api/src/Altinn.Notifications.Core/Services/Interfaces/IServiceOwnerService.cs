using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides service owner information, such as the sender addresses each service owner is allowed to use, from an in-memory copy of the notifications.serviceowner table.
/// </summary>
public interface IServiceOwnerService
{
    /// <summary>
    /// Loads the registered senders of all service owners from the database into memory.
    /// Any previously loaded senders are replaced, and the new data is only used once loading has completed.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the registered senders have been loaded.</returns>
    Task LoadRegisteredSenders(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the senders that are not registered for the service owner.
    /// </summary>
    /// <param name="serviceOwner">The short name of the service owner.</param>
    /// <param name="addressType">The channel the senders are used for.</param>
    /// <param name="senders">The senders to look up.</param>
    /// <returns>The distinct senders that are not registered for the service owner, or an empty list if all are registered.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the registered senders have not been loaded.</exception>
    IReadOnlyList<string> GetUnregisteredSenders(string serviceOwner, SenderAddressType addressType, IEnumerable<string> senders);
}
